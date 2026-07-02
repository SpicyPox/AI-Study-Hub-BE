using System;
using AIStudyHub.Api.Models;

namespace AIStudyHub.Api.DTOs.Payment;

public record CheckoutRequest(
    Guid PackageId,
    PurchaseType PurchaseKind,
    PaymentMethod Method,
    string? ReturnUrl = null
);

public record CheckoutResponse(
    string PaymentUrl,
    Guid TransactionId
);

public record PackageDto(
    Guid Id,
    string Name,
    decimal Price,
    long CapacityBytes,
    string? Description,
    string Type,
    int? DurationDays = null
);
