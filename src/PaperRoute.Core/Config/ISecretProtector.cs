namespace PaperRoute.Config;

/// <summary>Encrypts secrets stored in the settings file (Windows: DPAPI, tied to the user's login).</summary>
public interface ISecretProtector
{
    /// <summary>Encrypts a secret for storage.</summary>
    /// <param name="secret">Plain secret.</param>
    /// <returns>Text-safe encrypted form.</returns>
    string Protect(string secret);

    /// <summary>Decrypts a stored secret.</summary>
    /// <param name="protectedSecret">Value produced by <see cref="Protect"/>.</param>
    /// <returns>The plain secret.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">It cannot be decrypted here (other user or PC, damaged).</exception>
    string Unprotect(string protectedSecret);
}
