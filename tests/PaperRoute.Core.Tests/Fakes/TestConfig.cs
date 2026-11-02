using PaperRoute.Config;

namespace PaperRoute.Core.Tests.Fakes;

internal static class TestConfig
{
    public const string AuthPassword = "Test0Password0That0Must0Not0Leak";

    public static AppConfig Instance { get; } = new(AuthPassword: AuthPassword);
}
