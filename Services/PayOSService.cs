using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIStudyHub.Api.Models;
using Microsoft.Extensions.Configuration;
using PayOS;
using PayOS.Models.V2.PaymentRequests;

namespace AIStudyHub.Api.Services;

public class PayOSService(IConfiguration config) : IPaymentService
{
    private readonly PayOSClient _payOS = new(
        config["PayOS:ClientId"] ?? "",
        config["PayOS:ApiKey"] ?? "",
        config["PayOS:ChecksumKey"] ?? ""
    );

    public async Task<string> CreatePaymentUrlAsync(Transaction transaction, string ipAddress, string returnUrl)
    {
        var appSection = config.GetSection("App");
        var backendUrl = appSection["BaseUrl"] ?? "http://localhost:5143";

        // PayOS callback redirect will go to our backend controller, which then redirects to frontend
        var internalReturnUrl = $"{backendUrl}/api/payments/payos/callback" +
                                $"?returnUrl={Uri.EscapeDataString(returnUrl)}";
        var internalCancelUrl = $"{backendUrl}/api/payments/payos/callback" +
                                $"?returnUrl={Uri.EscapeDataString(returnUrl)}";

        // Generates a unique numeric order code (must be int/long and fit in Javascript's MAX_SAFE_INTEGER 9007199254740991)
        long orderCode;
        if (long.TryParse(transaction.TransactionRef, out var code))
        {
            orderCode = code;
        }
        else
        {
            // Timestamp in ms (13 digits) + 2 random digits (total 15 digits, less than 16 digits MAX_SAFE_INTEGER)
            orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100 + Random.Shared.Next(0, 99);
            transaction.TransactionRef = orderCode.ToString();
        }

        var items = new List<PaymentLinkItem>
        {
            new()
            {
                Name = transaction.PurchaseKind == PurchaseType.storage_package ? "Mua Dung Luong Storage AI Study Hub" : "Goi Dang Ky AI Study Hub",
                Quantity = 1,
                Price = (int)transaction.Amount
            }
        };

        var paymentRequest = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = (int)transaction.Amount,
            Description = $"DH {transaction.Id.ToString()[..8]}",
            Items = items,
            CancelUrl = internalCancelUrl,
            ReturnUrl = internalReturnUrl
        };

        var createPaymentResult = await _payOS.PaymentRequests.CreateAsync(paymentRequest);
        return createPaymentResult.CheckoutUrl;
    }

    public PayOSClient GetClient() => _payOS;
}
