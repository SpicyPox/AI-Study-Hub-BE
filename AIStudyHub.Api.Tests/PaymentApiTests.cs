using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Auth;
using AIStudyHub.Api.DTOs.Payment;
using AIStudyHub.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AIStudyHub.Api.Tests;

public class PaymentApiTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;
    private Guid _storagePackageId;
    private Guid _subscriptionPackageId;

    public PaymentApiTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        _factory = factory;
        SeedTestData();
    }

    private void SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Clear existing packages if any (due to shared fixture memory)
        db.StoragePackages.RemoveRange(db.StoragePackages);
        db.SubscriptionPackages.RemoveRange(db.SubscriptionPackages);
        db.SaveChanges();

        // Seed active storage package
        var storagePackage = new StoragePackage
        {
            Id = Guid.NewGuid(),
            Name = "Storage Pro 50GB",
            CapacityBytes = 53687091200L, // 50 GB
            Price = 100000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.StoragePackages.Add(storagePackage);
        _storagePackageId = storagePackage.Id;

        // Seed active subscription package
        var subscriptionPackage = new SubscriptionPackage
        {
            Id = Guid.NewGuid(),
            Name = "Sub VIP 30 Days",
            Description = "VIP student access",
            Price = 150000,
            DurationDays = 30,
            AiChatLimit = 1000,
            BaseStorageBytes = 107374182400L, // 100 GB
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.SubscriptionPackages.Add(subscriptionPackage);
        _subscriptionPackageId = subscriptionPackage.Id;

        db.SaveChanges();
    }

    private async Task<string> AuthenticateClientAsync(string email)
    {
        var username = email.Split('@')[0];
        
        // 1. Register
        var registerDto = new RegisterRequest(username, email, "Password123!");
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerDto);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // 2. Fetch OTP from cache
        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        var cacheKey = $"reg-otp:{email.ToLower()}";
        var cacheEntry = cache.Get(cacheKey);
        Assert.NotNull(cacheEntry);

        var otpProperty = cacheEntry.GetType().GetProperty("Otp");
        Assert.NotNull(otpProperty);
        var otpValue = (string)otpProperty.GetValue(cacheEntry)!;

        // 3. Verify OTP
        var verifyDto = new RegisterVerifyRequest(email, otpValue);
        var verifyResponse = await _client.PostAsJsonAsync("/api/auth/register/verify", verifyDto);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var authResponse = await verifyResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        return authResponse.AccessToken;
    }

    [Fact]
    public async Task GetStoragePackages_ReturnsActivePackages()
    {
        // Act
        var response = await _client.GetAsync("/api/packages/storage");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var packages = await response.Content.ReadFromJsonAsync<PackageDto[]>();
        Assert.NotNull(packages);
        Assert.Single(packages);
        Assert.Equal("Storage Pro 50GB", packages[0].Name);
        Assert.Equal(100000, packages[0].Price);
    }

    [Fact]
    public async Task GetSubscriptionPackages_ReturnsActivePackages()
    {
        // Act
        var response = await _client.GetAsync("/api/packages/subscription");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var packages = await response.Content.ReadFromJsonAsync<PackageDto[]>();
        Assert.NotNull(packages);
        Assert.Single(packages);
        Assert.Equal("Sub VIP 30 Days", packages[0].Name);
        Assert.Equal(30, packages[0].DurationDays);
    }

    [Fact]
    public async Task Checkout_MockMethod_CreatesPendingTransactionAndReturnsUrl()
    {
        // Arrange
        var token = await AuthenticateClientAsync("checkoutuser@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var checkoutRequest = new CheckoutRequest(
            PackageId: _storagePackageId,
            PurchaseKind: PurchaseType.storage_package,
            Method: PaymentMethod.stripe, // Mocked to MockPaymentService
            ReturnUrl: "http://localhost:5173/success"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/checkout", checkoutRequest);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Post /api/payments/checkout failed. Status: {response.StatusCode}, Content: {content}");
        }

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var checkoutResponse = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.NotNull(checkoutResponse);
        Assert.NotEmpty(checkoutResponse.PaymentUrl);
        Assert.NotEqual(Guid.Empty, checkoutResponse.TransactionId);

        // Clean authentication header for subsequent tests
        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task MockCallback_UpdatesTransactionToCompleted()
    {
        // Arrange
        var token = await AuthenticateClientAsync("callbackuser@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var checkoutRequest = new CheckoutRequest(
            PackageId: _storagePackageId,
            PurchaseKind: PurchaseType.storage_package,
            Method: PaymentMethod.momo, // Mocked to MockPaymentService
            ReturnUrl: "http://localhost:5173/success"
        );

        var checkoutResponse = await _client.PostAsJsonAsync("/api/payments/checkout", checkoutRequest);
        if (checkoutResponse.StatusCode != HttpStatusCode.OK)
        {
            var content = await checkoutResponse.Content.ReadAsStringAsync();
            Assert.Fail($"Post /api/payments/checkout failed. Status: {checkoutResponse.StatusCode}, Content: {content}");
        }

        var checkoutData = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.NotNull(checkoutData);

        // Act - Call mock callback url
        var callbackResponse = await _client.GetAsync($"/api/payments/mock/callback?transactionId={checkoutData.TransactionId}&returnUrl=http://localhost:5173/success");

        if (callbackResponse.StatusCode != HttpStatusCode.Redirect && callbackResponse.StatusCode != HttpStatusCode.Found)
        {
            var content = await callbackResponse.Content.ReadAsStringAsync();
            Assert.Fail($"MockCallback failed. Status: {callbackResponse.StatusCode}, Content: {content}");
        }

        // Assert - Mock callback redirects to return URL
        Assert.Equal(HttpStatusCode.Found, callbackResponse.StatusCode);
        Assert.StartsWith("http://localhost:5173/success?status=success", callbackResponse.Headers.Location?.ToString());

        // Verify transaction state in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transaction = await db.Transactions.FindAsync(checkoutData.TransactionId);
        Assert.NotNull(transaction);
        Assert.Equal(PaymentStatus.completed, transaction.Status);
        Assert.StartsWith("MOCK-", transaction.TransactionRef);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
