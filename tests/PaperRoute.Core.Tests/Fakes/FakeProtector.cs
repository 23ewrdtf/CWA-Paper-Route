using System.Security.Cryptography;
using System.Text;

using PaperRoute.Config;

namespace PaperRoute.Core.Tests.Fakes;

/// <summary>Reversible stand-in for DPAPI: base64 of the reversed text with a marker; can be told to fail.</summary>
internal sealed class FakeProtector : ISecretProtector
{
    public bool FailToUnprotect { get; init; }

    public string Protect(string secret) =>
        "FAKE:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(new string([.. secret.Reverse()])));

    public string Unprotect(string protectedSecret)
    {
        if (FailToUnprotect || !protectedSecret.StartsWith("FAKE:", StringComparison.Ordinal))
        {
            throw new CryptographicException("cannot decrypt on this account");
        }

        return new string([.. Encoding.UTF8.GetString(Convert.FromBase64String(protectedSecret[5..])).Reverse()]);
    }
}
