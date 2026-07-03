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

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(
    AppDbContext db,
    PaymentServiceFactory paymentFactory,
    VnPayService vnPayService,
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

            amount = package.Price;
            storageAddedBytes = package.CapacityBytes;
        }
        else if (req.PurchaseKind == PurchaseType.subscription_package)
        {
            var package = await db.SubscriptionPackages.FindAsync(req.PackageId);
            if (package == null || package.IsActive != true)
                return BadRequest(new { message = "Gói đăng ký không tồn tại hoặc đã bị khóa." });

            amount = package.Price;
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

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
