using System;
using System.Threading.Tasks;
using AIStudyHub.Api.Models;
using Microsoft.Extensions.Configuration;

namespace AIStudyHub.Api.Services;

public class MockPaymentService(IConfiguration config) : IPaymentService
{
    public Task<string> CreatePaymentUrlAsync(Transaction transaction, string ipAddress, string returnUrl)
    {
        if (transaction.Method == PaymentMethod.bank_transfer)
        {
            var amount = (long)Math.Round(transaction.Amount * 1.1m); // Add VAT 10% to match frontend calculation
            var addInfo = Uri.EscapeDataString($"AIStudyHub {transaction.Id.ToString()[..8].ToUpper()}");
            var paymentUrl = $"https://img.vietqr.io/image/TCB-19076609349015-compact2.png?amount={amount}&addInfo={addInfo}";
            return Task.FromResult(paymentUrl);
        }

        var appSection = config.GetSection("App");
        var backendUrl = appSection["BaseUrl"] ?? "http://localhost:5143";
        
        // Redirect to a local mock callback that completes the transaction instantly
        var mockUrl = $"{backendUrl}/api/payments/mock/callback" +
                      $"?transactionId={transaction.Id}" +
                      $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
                         
        return Task.FromResult(mockUrl);
    }
}
