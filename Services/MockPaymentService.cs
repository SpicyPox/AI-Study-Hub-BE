using System;
using System.Threading.Tasks;
using AIStudyHub.Api.Models;
using Microsoft.Extensions.Configuration;

namespace AIStudyHub.Api.Services;

public class MockPaymentService(IConfiguration config) : IPaymentService
{
    public Task<string> CreatePaymentUrlAsync(Transaction transaction, string ipAddress, string returnUrl)
    {
        var appSection = config.GetSection("App");
        var backendUrl = appSection["BaseUrl"] ?? "http://localhost:5143";
        
        // Redirect to a local mock callback that completes the transaction instantly
        var paymentUrl = $"{backendUrl}/api/payments/mock/callback" +
                         $"?transactionId={transaction.Id}" +
                         $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
                         
        return Task.FromResult(paymentUrl);
    }
}
