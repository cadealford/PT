using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PetroTransit.API.Services;

namespace PetroTransit.API.Tests;

public class EmailServiceTests
{
    [Fact]
    public async Task SendEmailAsync_WithoutSmtpConfig_UsesFallback_AndDoesNotCallSender()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var fakeSender = new FakeSmtpSender();
        var service = new EmailService(config, fakeSender, NullLogger<EmailService>.Instance);

        await service.SendEmailAsync("to@example.com", "Subject", "Body");

        Assert.Equal(0, fakeSender.Calls);
    }

    [Fact]
    public async Task SendEmailAsync_WithSmtpConfig_CallsSenderWithExpectedPayload()
    {
        var values = new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "shadow.mxrouting.net",
            ["Smtp:Port"] = "587",
            ["Smtp:EnableSsl"] = "true",
            ["Smtp:Username"] = "petrotransitsender@fonefit.com",
            ["Smtp:Password"] = "secret",
            ["Smtp:FromEmail"] = "petrotransitsender@fonefit.com"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var fakeSender = new FakeSmtpSender();
        var service = new EmailService(config, fakeSender, NullLogger<EmailService>.Instance);

        await service.SendEmailAsync("user@example.com", "Reset", "Hello");

        Assert.Equal(1, fakeSender.Calls);
        Assert.NotNull(fakeSender.LastRequest);
        Assert.Equal("shadow.mxrouting.net", fakeSender.LastRequest!.Host);
        Assert.Equal(587, fakeSender.LastRequest.Port);
        Assert.True(fakeSender.LastRequest.EnableSsl);
        Assert.Equal("petrotransitsender@fonefit.com", fakeSender.LastRequest.Username);
        Assert.Equal("petrotransitsender@fonefit.com", fakeSender.LastRequest.FromEmail);
        Assert.Equal("user@example.com", fakeSender.LastRequest.ToEmail);
        Assert.Equal("Reset", fakeSender.LastRequest.Subject);
        Assert.Equal("Hello", fakeSender.LastRequest.Body);
    }

    private sealed class FakeSmtpSender : ISmtpSender
    {
        public int Calls { get; private set; }
        public SmtpSendRequest? LastRequest { get; private set; }

        public Task SendAsync(SmtpSendRequest request)
        {
            Calls++;
            LastRequest = request;
            return Task.CompletedTask;
        }
    }
}
