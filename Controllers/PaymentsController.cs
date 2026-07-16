using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Payment;
using AIStudyHub.Api.Models;
using AIStudyHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PayOS;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(
    AppDbContext db,
    PaymentServiceFactory paymentFactory,
    VnPayService vnPayService,
    PayOSService payOSService,
    IConfiguration config) : ControllerBase
{
    [Authorize]
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(CheckoutRequest req)
    {
        decimal amount = 0;
        long storageAddedBytes = 0;

        if (req.PurchaseKind == PurchaseType.storage_package)
        {
            var package = await db.StoragePackages.FindAsync(req.PackageId);
            if (package == null || package.IsActive != true)
                return BadRequest(new { message = "Gói dung lượng không tồn tại hoặc đã bị khóa." });

            amount = Math.Round(package.Price * 1.1m);
            storageAddedBytes = package.CapacityBytes;
        }
        else if (req.PurchaseKind == PurchaseType.subscription_package)
        {
            var package = await db.SubscriptionPackages.FindAsync(req.PackageId);
            if (package == null || package.IsActive != true)
                return BadRequest(new { message = "Gói đăng ký không tồn tại hoặc đã bị khóa." });

            amount = Math.Round(package.Price * 1.1m);
            storageAddedBytes = package.BaseStorageBytes;
        }
        else
        {
            return BadRequest(new { message = "Loại gói dịch vụ không hợp lệ." });
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = UserId(),
            PackageId = req.PurchaseKind == PurchaseType.storage_package ? req.PackageId : null,
            SubscriptionPackageId = req.PurchaseKind == PurchaseType.subscription_package ? req.PackageId : null,
            PurchaseKind = req.PurchaseKind,
            Amount = amount,
            StorageAddedBytes = storageAddedBytes,
            Status = PaymentStatus.pending,
            Method = req.Method,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();

        var paymentService = paymentFactory.GetPaymentService(req.Method);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        
        // Resolve returnUrl
        var finalReturnUrl = req.ReturnUrl ?? config["VnPay:ReturnUrl"] ?? "http://localhost:5173/payment/callback";

        var paymentUrl = await paymentService.CreatePaymentUrlAsync(transaction, ipAddress, finalReturnUrl);
        await db.SaveChangesAsync(); // Persist changes made to transaction (like TransactionRef/OrderCode)

        return Ok(new CheckoutResponse(paymentUrl, transaction.Id));
    }

    [HttpGet("mock/callback")]
    public async Task<IActionResult> MockCallback([FromQuery] Guid transactionId, [FromQuery] string returnUrl)
    {
        var transaction = await db.Transactions.FindAsync(transactionId);
        if (transaction == null)
            return NotFound("Giao dịch không tồn tại.");

        if (transaction.Status == PaymentStatus.pending)
        {
            transaction.Status = PaymentStatus.completed;
            transaction.TransactionRef = $"MOCK-{Guid.NewGuid()}";
            transaction.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Redirect($"{returnUrl}?status=success&txnId={transactionId}");
    }

    [HttpGet("vnpay/callback")]
    public async Task<IActionResult> VnPayCallback([FromQuery] string returnUrl)
    {
        var isValid = vnPayService.ValidateSignature(Request.Query);
        if (!isValid)
        {
            return Redirect($"{returnUrl}?status=fail&error=InvalidSignature");
        }

        var txnRef = Request.Query["vnp_TxnRef"].ToString();
        if (!Guid.TryParse(txnRef, out Guid txnId))
        {
            return Redirect($"{returnUrl}?status=fail&error=InvalidTxnRef");
        }

        var transaction = await db.Transactions.FindAsync(txnId);
        if (transaction == null)
        {
            return Redirect($"{returnUrl}?status=fail&error=TxnNotFound");
        }

        var responseCode = Request.Query["vnp_ResponseCode"].ToString();
        if (responseCode == "00")
        {
            if (transaction.Status == PaymentStatus.pending)
            {
                transaction.Status = PaymentStatus.completed;
                transaction.TransactionRef = Request.Query["vnp_TransactionNo"].ToString();
                transaction.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return Redirect($"{returnUrl}?status=success&txnId={txnId}");
        }
        else
        {
            if (transaction.Status == PaymentStatus.pending)
            {
                transaction.Status = PaymentStatus.failed;
                transaction.TransactionRef = Request.Query["vnp_TransactionNo"].ToString();
                transaction.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return Redirect($"{returnUrl}?status=fail&error=PaymentFailed&code={responseCode}");
        }
    }

    [HttpGet("vnpay/ipn")]
    public async Task<IActionResult> VnPayIpn()
    {
        var isValid = vnPayService.ValidateSignature(Request.Query);
        if (!isValid)
        {
            return Ok(new { RspCode = "97", Message = "Invalid signature" });
        }

        var txnRef = Request.Query["vnp_TxnRef"].ToString();
        if (!Guid.TryParse(txnRef, out Guid txnId))
        {
            return Ok(new { RspCode = "01", Message = "Order not found" });
        }

        var transaction = await db.Transactions.FindAsync(txnId);
        if (transaction == null)
        {
            return Ok(new { RspCode = "01", Message = "Order not found" });
        }

        var vnpAmount = long.Parse(Request.Query["vnp_Amount"].ToString());
        var expectedAmount = (long)(transaction.Amount * 100);
        if (vnpAmount != expectedAmount)
        {
            return Ok(new { RspCode = "04", Message = "Invalid amount" });
        }

        if (transaction.Status != PaymentStatus.pending)
        {
            return Ok(new { RspCode = "02", Message = "Order already confirmed" });
        }

        var responseCode = Request.Query["vnp_ResponseCode"].ToString();
        if (responseCode == "00")
        {
            transaction.Status = PaymentStatus.completed;
        }
        else
        {
            transaction.Status = PaymentStatus.failed;
        }

        transaction.TransactionRef = Request.Query["vnp_TransactionNo"].ToString();
        transaction.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { RspCode = "00", Message = "Confirm Success" });
    }

    [Authorize]
    [HttpGet("history")]
    public async Task<IActionResult> GetTransactionHistory()
    {
        var userId = UserId();
        var transactions = await db.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new {
                t.Id,
                t.Amount,
                t.Status,
                t.Method,
                t.PurchaseKind,
                t.StorageAddedBytes,
                t.TransactionRef,
                t.CreatedAt
            })
            .ToListAsync();

        return Ok(transactions);
    }

    [HttpGet("payos/callback")]
    public async Task<IActionResult> PayOSCallback([FromQuery] string returnUrl, [FromQuery] long orderCode, [FromQuery] string cancel, [FromQuery] string status)
    {
        try
        {
            var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.TransactionRef == orderCode.ToString());
            if (transaction == null)
            {
                return Redirect($"{returnUrl}?status=fail&error=TxnNotFound");
            }

            if (cancel == "true" || status == "CANCELLED")
            {
                if (transaction.Status == PaymentStatus.pending)
                {
                    transaction.Status = PaymentStatus.failed;
                    transaction.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
                return Redirect($"{returnUrl}?status=fail&error=Cancelled");
            }

            var paymentInfo = await payOSService.GetClient().PaymentRequests.GetAsync(orderCode);
            // BUG: PaymentLinkStatus.Paid.ToString() trả về "Paid" (PascalCase của SDK), không phải
            // "PAID" -> so sánh chuỗi cũ luôn sai, khiến giao dịch thành công vẫn bị coi là thất bại
            // ở đây (dù webhook riêng vẫn xử lý đúng, dẫn đến user thấy "thất bại" nhưng vẫn bị trừ
            // tiền/lên gói). So sánh trực tiếp bằng enum để tránh phụ thuộc cách SDK format chuỗi.
            if (paymentInfo.Status == PaymentLinkStatus.Paid)
            {
                if (transaction.Status == PaymentStatus.pending)
                {
                    // Giữ nguyên TransactionRef = orderCode (không ghi đè bằng paymentInfo.Id):
                    // cả callback lẫn webhook đều tra transaction bằng TransactionRef, nếu đổi giá trị
                    // này thì bên chạy sau (webhook hoặc callback, thứ tự không đảm bảo) sẽ không tìm
                    // thấy giao dịch nữa -> có thể hiện "thất bại" cho một giao dịch thực ra đã thành công.
                    transaction.Status = PaymentStatus.completed;
                    transaction.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
                return Redirect($"{returnUrl}?status=success&txnId={transaction.Id}");
            }
            else
            {
                if (transaction.Status == PaymentStatus.pending)
                {
                    transaction.Status = PaymentStatus.failed;
                    transaction.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
                return Redirect($"{returnUrl}?status=fail&error=PaymentFailed&code={paymentInfo.Status}");
            }
        }
        catch (Exception ex)
        {
            return Redirect($"{returnUrl}?status=fail&error={Uri.EscapeDataString(ex.Message)}");
        }
    }

    [HttpPost("payos/webhook")]
    public async Task<IActionResult> PayOSWebhook([FromBody] Webhook webhookBody)
    {
        try
        {
            var verifiedData = await payOSService.GetClient().Webhooks.VerifyAsync(webhookBody);
            
            var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.TransactionRef == verifiedData.OrderCode.ToString());
            if (transaction != null)
            {
                if (transaction.Status == PaymentStatus.pending)
                {
                    // Không ghi đè TransactionRef — xem chú thích ở PayOSCallback.
                    transaction.Status = PaymentStatus.completed;
                    transaction.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            return Ok(new { RspCode = "00", Message = "Success" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
