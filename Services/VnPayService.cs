using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AIStudyHub.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace AIStudyHub.Api.Services;

public class VnPayService(IConfiguration config) : IPaymentService
{
    private string TmnCode => config["VnPay:TmnCode"] ?? "2QXUI8S2";
    private string HashSecret => config["VnPay:HashSecret"] ?? "YOUR_VNPAY_HASH_SECRET";
    private string BaseUrl => config["VnPay:BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    public Task<string> CreatePaymentUrlAsync(Transaction transaction, string ipAddress, string returnUrl)
    {
        var appSection = config.GetSection("App");
        var backendUrl = appSection["BaseUrl"] ?? "http://localhost:5143";
        
        // Construct the return URL that points to our backend callback
        var internalReturnUrl = $"{backendUrl}/api/payments/vnpay/callback" +
                                $"?returnUrl={Uri.EscapeDataString(returnUrl)}";

        var payParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            { "vnp_Version", "2.1.0" },
            { "vnp_Command", "pay" },
            { "vnp_TmnCode", TmnCode },
            { "vnp_Amount", ((long)(transaction.Amount * 100)).ToString() },
            { "vnp_CreateDate", DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss") }, // Vietnam timezone
            { "vnp_CurrCode", "VND" },
            { "vnp_IpAddr", string.IsNullOrEmpty(ipAddress) || ipAddress == "::1" ? "127.0.0.1" : ipAddress },
            { "vnp_Locale", "vn" },
            { "vnp_OrderInfo", $"Thanh toan don hang {transaction.Id}" },
            { "vnp_OrderType", "other" },
            { "vnp_ReturnUrl", internalReturnUrl },
            { "vnp_TxnRef", transaction.Id.ToString() }
        };

        var rawData = new StringBuilder();
        var query = new StringBuilder();

        foreach (var (key, val) in payParams)
        {
            if (rawData.Length > 0) rawData.Append('&');
            rawData.Append(key).Append('=').Append(Uri.EscapeDataString(val));

            if (query.Length > 0) query.Append('&');
            query.Append(key).Append('=').Append(Uri.EscapeDataString(val));
        }

        var secureHash = HmacSha512(HashSecret, rawData.ToString());
        query.Append("&vnp_SecureHash=").Append(secureHash);

        return Task.FromResult($"{BaseUrl}?{query}");
    }

    public bool ValidateSignature(IQueryCollection query)
    {
        string? secureHash = null;
        var payParams = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, val) in query)
        {
            if (string.IsNullOrEmpty(key)) continue;

            if (key == "vnp_SecureHash")
            {
                secureHash = val.ToString();
            }
            else if (key.StartsWith("vnp_"))
            {
                payParams.Add(key, val.ToString());
            }
        }

        if (string.IsNullOrEmpty(secureHash)) return false;

        var rawData = new StringBuilder();
        foreach (var (key, val) in payParams)
        {
            if (rawData.Length > 0) rawData.Append('&');
            rawData.Append(key).Append('=').Append(Uri.EscapeDataString(val));
        }

        var calculatedHash = HmacSha512(HashSecret, rawData.ToString());
        return string.Equals(calculatedHash, secureHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string HmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hashBytes).ToLower();
    }
}
