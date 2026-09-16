using System.Net;
using System.Net.Mail;
using Gas365.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Gas365.Infrastructure;

public sealed class AccountMessages(IConfiguration configuration, IHostEnvironment environment) : IAccountMessages
{
    public async Task SendAsync(string email, string subject, string path, CancellationToken cancellationToken)
    {
        var host = configuration["Mail:Host"] ?? throw new AccessException(503, "Account email delivery is not configured.");
        var origin = configuration["Mail:PublicOrigin"] ?? throw new AccessException(503, "Account email delivery is not configured.");
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || (!environment.IsDevelopment() && uri.Scheme != "https"))
            throw new AccessException(503, "A secure application origin is required.");
        using var client = new SmtpClient(host, configuration.GetValue("Mail:Port", 587))
        { EnableSsl = !environment.IsDevelopment() || configuration.GetValue("Mail:EnableSsl", false) };
        if (configuration["Mail:Username"] is string username)
            client.Credentials = new NetworkCredential(username, configuration["Mail:Password"]);
        using var message = new MailMessage(configuration["Mail:From"] ?? "accounts@gas365.example", email, subject,
            $"{subject}\n\nOpen this link within one hour:\n{origin.TrimEnd('/')}{path}\n\nIf you did not request this, ignore this email.");
        await client.SendMailAsync(message, cancellationToken);
    }
}
