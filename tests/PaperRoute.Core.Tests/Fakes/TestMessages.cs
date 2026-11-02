using System.Text;

using MimeKit;

namespace PaperRoute.Core.Tests.Fakes;

internal static class TestMessages
{
    /// <summary>A message shaped like CWA's: plain-text body plus the given attachments.</summary>
    public static MimeMessage Books(params (string Name, int Bytes)[] attachments) =>
        Build([.. attachments.Select(a => (a.Name, BookBytes.For(a.Name, a.Bytes)))]);

    /// <summary>A CWA-shaped message whose attachment has exactly the given bytes.</summary>
    public static MimeMessage Raw(string name, byte[] content) => Build([(name, content)]);

    private static MimeMessage Build((string Name, byte[] Content)[] attachments)
    {
        var builder = new BodyBuilder { TextBody = "This e-mail has been sent via Calibre-Web." };
        foreach (var (name, content) in attachments)
        {
            builder.Attachments.Add(name, content);
        }

        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("cwa@localhost"));
        message.To.Add(MailboxAddress.Parse("kindle@paperroute.local"));
        message.Subject = "Send to eReader";
        message.Body = builder.ToMessageBody();
        return RoundTrip(message);
    }

    public static MimeMessage Book(string name, int bytes = 1024) => Books((name, bytes));

    public static MimeMessage BodyOnly() => Books();

    /// <summary>Serializes and re-parses, so tests see what a real SMTP session would deliver.</summary>
    public static MimeMessage RoundTrip(MimeMessage message)
    {
        using var stream = new MemoryStream();
        message.WriteTo(stream);
        stream.Position = 0;
        return MimeMessage.Load(stream);
    }

    public static MimeMessage Parse(string raw)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        return MimeMessage.Load(stream);
    }
}
