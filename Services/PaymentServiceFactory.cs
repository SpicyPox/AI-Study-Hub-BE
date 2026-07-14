using System;
using AIStudyHub.Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AIStudyHub.Api.Services;

public class PaymentServiceFactory(IServiceProvider serviceProvider)
{
    public IPaymentService GetPaymentService(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.vnpay => serviceProvider.GetRequiredService<VnPayService>(),
            PaymentMethod.stripe => serviceProvider.GetRequiredService<MockPaymentService>(),
            PaymentMethod.momo => serviceProvider.GetRequiredService<MockPaymentService>(),
            PaymentMethod.bank_transfer => serviceProvider.GetRequiredService<PayOSService>(),
            PaymentMethod.payos => serviceProvider.GetRequiredService<PayOSService>(),
            _ => throw new ArgumentException($"Payment method {method} is not supported.")
        };
    }
}
