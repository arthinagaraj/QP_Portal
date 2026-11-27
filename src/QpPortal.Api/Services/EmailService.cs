using System.Net;
using System.Net.Mail;

namespace QpPortal.Api.Services;

public class EmailService(IConfiguration config)
{
    public async Task SendAsync(string to, string toName, string subject, string html)
    {
        var s = config.GetSection("Smtp");

        using var msg = new MailMessage
        {
            From = new MailAddress(s["From"] ?? "noreply@qp.local"),
            Subject = subject,
            Body = html,
            IsBodyHtml = true
        };
        msg.To.Add(new MailAddress(to, toName));

        using var client = new SmtpClient(s["Host"] ?? "localhost", s.GetValue<int?>("Port") ?? 25)
        {
            EnableSsl = s.GetValue<bool>("UseStartTls")
        };
        var user = s["User"];
        if (!string.IsNullOrEmpty(user))
            client.Credentials = new NetworkCredential(user, s["Password"]);

        await client.SendMailAsync(msg);
    }
}