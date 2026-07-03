using AIStudyHub.Api.DTOs.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AIStudyHub.Api.Tests
{
    public class AuthApiTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<Program> _factory;

        public AuthApiTests(CustomWebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
            _factory = factory;
        }

        private async Task<string> GetOtpFromCacheAsync(string email)
        {
            using var scope = _factory.Services.CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            var cacheKey = $"reg-otp:{email.ToLower()}";
            var cacheEntry = cache.Get(cacheKey);
            Assert.NotNull(cacheEntry);

            var otpProperty = cacheEntry.GetType().GetProperty("Otp");
            Assert.NotNull(otpProperty);
            var otpValue = (string)otpProperty.GetValue(cacheEntry)!;
            Assert.NotEmpty(otpValue);
            return otpValue;
        }

        [Fact]
        public async Task Register_WithValidData_ReturnsAuthResponseAndSuccess()
        {
            // Arrange
            var registerDto = new RegisterRequest(
                Name: "Test User",
                Email: "testuser@example.com",
                Password: "Password123!"
            );

            // Act - Step 1: Send OTP
            var response = await _client.PostAsJsonAsync("/api/auth/register", registerDto);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                Assert.Fail($"Post /api/auth/register failed. Status: {response.StatusCode}, Content: {content}");
            }

            // Act - Step 2: Verify OTP
            var otp = await GetOtpFromCacheAsync(registerDto.Email);
            var verifyDto = new RegisterVerifyRequest(registerDto.Email, otp);
            var verifyResponse = await _client.PostAsJsonAsync("/api/auth/register/verify", verifyDto);
            if (verifyResponse.StatusCode != HttpStatusCode.OK)
            {
                var content = await verifyResponse.Content.ReadAsStringAsync();
                Assert.Fail($"Post /api/auth/register/verify failed. Status: {verifyResponse.StatusCode}, Content: {content}");
            }

            // Assert
            Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

            var authResponse = await verifyResponse.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(authResponse);
            Assert.NotNull(authResponse.User);
            Assert.Equal("Test User", authResponse.User.Name);
            Assert.Equal("testuser@example.com", authResponse.User.Email);
            Assert.Equal("user", authResponse.User.Role);
            Assert.NotEmpty(authResponse.AccessToken);
            Assert.NotEmpty(authResponse.RefreshToken);
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsBadRequest()
        {
            // Arrange
            var registerDto = new RegisterRequest(
                Name: "Unique User 1",
                Email: "duplicate@example.com",
                Password: "Password123!"
            );

            var duplicateDto = new RegisterRequest(
                Name: "Unique User 2",
                Email: "duplicate@example.com",
                Password: "Password123!"
            );

            // Act
            // Register first user and verify them to store them in DB
            var response1 = await _client.PostAsJsonAsync("/api/auth/register", registerDto);
            if (response1.StatusCode != HttpStatusCode.OK)
            {
                var content = await response1.Content.ReadAsStringAsync();
                Assert.Fail($"Post /api/auth/register 1 failed. Status: {response1.StatusCode}, Content: {content}");
            }

            var otp = await GetOtpFromCacheAsync(registerDto.Email);
            var verifyDto = new RegisterVerifyRequest(registerDto.Email, otp);
            var verifyResponse1 = await _client.PostAsJsonAsync("/api/auth/register/verify", verifyDto);
            if (verifyResponse1.StatusCode != HttpStatusCode.OK)
            {
                var content = await verifyResponse1.Content.ReadAsStringAsync();
                Assert.Fail($"Post /api/auth/register/verify 1 failed. Status: {verifyResponse1.StatusCode}, Content: {content}");
            }

            // Try registering duplicate email
            var secondResponse = await _client.PostAsJsonAsync("/api/auth/register", duplicateDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode); // ErrorHandlingMiddleware returns 400 BadRequest for duplicate business logic exceptions
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsAuthResponse()
        {
            // Arrange
            var registerDto = new RegisterRequest(
                Name: "Login User",
                Email: "loginuser@example.com",
                Password: "Password123!"
            );
            
            // Register and verify user
            var response1 = await _client.PostAsJsonAsync("/api/auth/register", registerDto);
            if (response1.StatusCode != HttpStatusCode.OK)
            {
                var content = await response1.Content.ReadAsStringAsync();
                Assert.Fail($"Post /api/auth/register failed. Status: {response1.StatusCode}, Content: {content}");
            }

            var otp = await GetOtpFromCacheAsync(registerDto.Email);
            var verifyDto = new RegisterVerifyRequest(registerDto.Email, otp);
            var verifyResponse1 = await _client.PostAsJsonAsync("/api/auth/register/verify", verifyDto);
            if (verifyResponse1.StatusCode != HttpStatusCode.OK)
            {
                var content = await verifyResponse1.Content.ReadAsStringAsync();
                Assert.Fail($"Post /api/auth/register/verify failed. Status: {verifyResponse1.StatusCode}, Content: {content}");
            }

            var loginDto = new LoginRequest(
                Email: "loginuser@example.com",
                Password: "Password123!"
            );

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(authResponse);
            Assert.NotNull(authResponse.User);
            Assert.Equal("loginuser@example.com", authResponse.User.Email);
            Assert.NotEmpty(authResponse.AccessToken);
            Assert.NotEmpty(authResponse.RefreshToken);
        }

        [Fact]
        public async Task Login_WithWrongPassword_ReturnsUnauthorized()
        {
            // Arrange
            var registerDto = new RegisterRequest(
                Name: "Wrong Pass User",
                Email: "wrongpass@example.com",
                Password: "Password123!"
            );
            
            // Register and verify user
            var response1 = await _client.PostAsJsonAsync("/api/auth/register", registerDto);
            if (response1.StatusCode != HttpStatusCode.OK)
            {
                var content = await response1.Content.ReadAsStringAsync();
                Assert.Fail($"Post /api/auth/register failed. Status: {response1.StatusCode}, Content: {content}");
            }

            var otp = await GetOtpFromCacheAsync(registerDto.Email);
            var verifyDto = new RegisterVerifyRequest(registerDto.Email, otp);
            var verifyResponse1 = await _client.PostAsJsonAsync("/api/auth/register/verify", verifyDto);
            if (verifyResponse1.StatusCode != HttpStatusCode.OK)
            {
                var content = await verifyResponse1.Content.ReadAsStringAsync();
                Assert.Fail($"Post /api/auth/register/verify failed. Status: {verifyResponse1.StatusCode}, Content: {content}");
            }

            var loginDto = new LoginRequest(
                Email: "wrongpass@example.com",
                Password: "WrongPassword!!!"
            );

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
