using MailKit.Net.Smtp;

namespace EHub.Infrastructure.Services.Email;

public interface ISmtpClientFactory
{
    ISmtpClient Create();
}

public sealed class SmtpClientFactory : ISmtpClientFactory
{
    public ISmtpClient Create() => new SmtpClient();
}
