using AmariMusic.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AmariMusic.Services;

public class EmailService(IConfiguration config, ILogger<EmailService> logger)
{
    public async Task SendAdminNotificationAsync(ContactSubmission submission)
    {
        var host = config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning("Email not configured — skipping admin notification for submission {Id}", submission.Id);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                config["Email:FromName"] ?? "Amari Music",
                config["Email:FromAddress"]!));
            message.To.Add(MailboxAddress.Parse(config["Email:AdminNotificationAddress"]!));
            message.Subject = $"New Contact Inquiry from {submission.Name}";

            var baseUrl = config["App:BaseUrl"]?.TrimEnd('/');
            var contactUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? $"/admin/contacts/{submission.Id}"
                : $"{baseUrl}/admin/contacts/{submission.Id}";

            var body = new BodyBuilder
            {
                HtmlBody = $"""
                    <h2>New Contact Inquiry</h2>
                    <table style="border-collapse:collapse;font-family:sans-serif;">
                        <tr><td style="padding:6px 12px;font-weight:bold;">Name</td><td style="padding:6px 12px;">{System.Web.HttpUtility.HtmlEncode(submission.Name)}</td></tr>
                        <tr><td style="padding:6px 12px;font-weight:bold;">Email</td><td style="padding:6px 12px;"><a href="mailto:{System.Web.HttpUtility.HtmlEncode(submission.Email)}">{System.Web.HttpUtility.HtmlEncode(submission.Email)}</a></td></tr>
                        <tr><td style="padding:6px 12px;font-weight:bold;">Phone</td><td style="padding:6px 12px;">{System.Web.HttpUtility.HtmlEncode(submission.Phone ?? "—")}</td></tr>
                        <tr><td style="padding:6px 12px;font-weight:bold;">Submitted</td><td style="padding:6px 12px;">{submission.SubmittedAt:f} UTC</td></tr>
                    </table>
                    <h3>Message</h3>
                    <p style="white-space:pre-wrap;">{System.Web.HttpUtility.HtmlEncode(submission.Message)}</p>
                    <hr/>
                    <p><a href="{System.Web.HttpUtility.HtmlEncode(contactUrl)}">View in admin dashboard</a></p>
                    """
            };
            message.Body = body.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(host, int.Parse(config["Email:SmtpPort"] ?? "587"), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(config["Email:Username"] ?? "", config["Email:Password"] ?? "");
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send admin notification email for submission {Id}", submission.Id);
        }
    }
}
