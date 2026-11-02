using System.Security.Cryptography;
using System.Text;

using PaperRoute.Config;

namespace PaperRoute.App;

/// <summary>
/// Encrypts the stored SMTP password with Windows DPAPI for the current user: only this Windows account on
/// this PC can read it, so copies of the settings file (backups, sync, attachments) don't reveal it.
/// </summary>
internal sealed class DpapiProtector : ISecretProtector
{
    // App-specific extra key so other programs using DPAPI for the same user can't trivially reuse the value.
    private static readonly byte[] Entropy = "PaperRoute/config/authPassword/v1"u8.ToArray();

    /// <inheritdoc/>
    public string Protect(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser));
    }

    /// <inheritdoc/>
    public string Unprotect(string protectedSecret)
    {
        ArgumentNullException.ThrowIfNull(protectedSecret);
        byte[] data;
        try
        {
            data = Convert.FromBase64String(protectedSecret);
        }
        catch (FormatException e)
        {
            throw new CryptographicException("The stored password is damaged", e);
        }

        return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser));
    }
}
