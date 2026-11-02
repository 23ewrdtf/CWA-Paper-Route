using System.Security.Cryptography;
using System.Text;

using SmtpServer;
using SmtpServer.Authentication;

namespace PaperRoute.Smtp;

/// <summary>Accepts only the login and password from the Paper Route settings.</summary>
internal sealed class PaperRouteUserAuthenticator(SessionGuard guard) : IUserAuthenticator
{
    public Task<bool> AuthenticateAsync(ISessionContext context, string user, string password, CancellationToken cancellationToken)
    {
        guard.EnsureClientAllowed(context);
        // Compare both, always, so timing does not reveal which one was wrong.
        var loginOk = Matches(user, guard.Config.AuthLogin);
        var passwordOk = Matches(password, guard.Config.AuthPassword);
        if (loginOk && passwordOk)
        {
            return Task.FromResult(true);
        }

        throw guard.FailedLogin(context);
    }

    private static bool Matches(string? given, string expected) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given ?? string.Empty), Encoding.UTF8.GetBytes(expected));
}
