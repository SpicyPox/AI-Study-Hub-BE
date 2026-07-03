using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace AIStudyHub.Api.Services;

public class EmailService(IConfiguration config, IHostEnvironment env)
{
    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        if (env.IsEnvironment("Testing"))
        {
            // Simulate successful email sending in testing environment
            Console.WriteLine($"[Email Test Stub] To: {toEmail}, Subject: {subject}");
            return;
        }

        var smtpSection = config.GetSection("Smtp");
        var host = smtpSection["Host"] ?? "smtp.gmail.com";
        var port = int.Parse(smtpSection["Port"] ?? "587");
        var enableSsl = bool.Parse(smtpSection["EnableSsl"] ?? "true");
        var username = smtpSection["Username"];
        var password = smtpSection["Password"];
        var fromEmail = smtpSection["FromEmail"] ?? username;
        var fromName = smtpSection["FromName"] ?? "AI Study Hub";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("SMTP credentials are not configured in appsettings.json.");
        }

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = enableSsl
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(fromEmail!, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        mailMessage.To.Add(toEmail);

        await client.SendMailAsync(mailMessage);
    }
}
