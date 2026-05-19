using Microsoft.AspNetCore.Identity.UI.Services;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace RideMate.Infrastructure.Services;

public class EmailService : IEmailSender
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(
        string email,
        string subject,
        string htmlMessage)
    {
        var message = new MimeMessage();
        var senderEmail = _config["EmailSettings:SenderEmail"]
            ?? throw new InvalidOperationException("Email sender address is not configured.");
        var smtpServer = _config["EmailSettings:SmtpServer"]
            ?? throw new InvalidOperationException("SMTP server is not configured.");
        var smtpPassword = _config["EmailSettings:Password"]
            ?? throw new InvalidOperationException("SMTP password is not configured.");
        var smtpPort = _config.GetValue<int?>("EmailSettings:Port")
            ?? throw new InvalidOperationException("SMTP port is not configured.");

        message.From.Add(new MailboxAddress(
            "RideMate",
            senderEmail));

        message.To.Add(MailboxAddress.Parse(email));

        message.Subject = subject;

        message.Body = new TextPart("html")
        {
            Text = htmlMessage
        };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            smtpServer,
            smtpPort,
            true);

        await client.AuthenticateAsync(
            senderEmail,
            smtpPassword);

        await client.SendAsync(message);

        await client.DisconnectAsync(true);
    }
}
