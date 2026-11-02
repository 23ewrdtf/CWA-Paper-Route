using System.Security.Cryptography;

using PaperRoute.App;

namespace PaperRoute.App.Tests;

public sealed class DpapiProtectorTests
{
    private readonly DpapiProtector _protector = new();

    [Fact]
    public void Round_trips_a_password()
    {
        var stored = _protector.Protect("Kp3xQ9vL2mN8rT5wY7zA1bC4dE6fG0hJ");

        Assert.Equal("Kp3xQ9vL2mN8rT5wY7zA1bC4dE6fG0hJ", _protector.Unprotect(stored));
    }

    [Fact]
    public void Stored_form_does_not_contain_the_password()
    {
        var stored = _protector.Protect("Kp3xQ9vL2mN8rT5wY7zA1bC4dE6fG0hJ");

        Assert.DoesNotContain("Kp3xQ9vL2mN8rT5wY7zA1bC4dE6fG0hJ", stored, StringComparison.Ordinal);
        Assert.NotEqual(stored, _protector.Protect("Kp3xQ9vL2mN8rT5wY7zA1bC4dE6fG0hJ"), StringComparer.Ordinal); // random salt each time
    }

    [Fact]
    public void Tampered_value_cannot_be_decrypted()
    {
        var bytes = Convert.FromBase64String(_protector.Protect("secret-value-123"));
        bytes[^5] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => _protector.Unprotect(Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void Garbage_is_a_cryptographic_error_not_a_crash() =>
        Assert.ThrowsAny<CryptographicException>(() => _protector.Unprotect("not base64 at all!"));
}
