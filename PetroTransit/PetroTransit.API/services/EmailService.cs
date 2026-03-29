using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace PetroTransit.API.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body);
}

public interface ISmtpSender
{
    Task SendAsync(SmtpSendRequest request);
}

public sealed class SmtpSendRequest
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public bool EnableSsl { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromEmail { get; init; } = string.Empty;
    public string ToEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}

public sealed class SmtpSender : ISmtpSender
{
    public async Task SendAsync(SmtpSendRequest request)
    {
        using var client = new SmtpClient(request.Host, request.Port)
        {
            EnableSsl = request.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            client.Credentials = new NetworkCredential(request.Username, request.Password);
        }

        using var message = new MailMessage(request.FromEmail, request.ToEmail, request.Subject, request.Body);
        await client.SendMailAsync(message);
    }
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ISmtpSender _smtpSender;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ISmtpSender smtpSender, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _smtpSender = smtpSender;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var host = _configuration["Smtp:Host"];
        var port = int.TryParse(_configuration["Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"];
        var enableSsl = bool.TryParse(_configuration["Smtp:EnableSsl"], out var parsedSsl) ? parsedSsl : true;

        // Development-safe fallback when SMTP isn't configured.
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
        {
            _logger.LogInformation(
                "FAKE EMAIL SENT (DEVELOPMENT). To: {ToEmail}. Subject: {Subject}. Body: {Body}",
                toEmail,
                subject,
                body);
            return;
        }

        try
        {
            await _smtpSender.SendAsync(new SmtpSendRequest
            {
                Host = host,
                Port = port,
                EnableSsl = enableSsl,
                Username = username,
                Password = password,
                FromEmail = fromEmail,
                ToEmail = toEmail,
                Subject = subject,
                Body = body
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failure to {ToEmail}", toEmail);
            throw;
        }
    }
}
