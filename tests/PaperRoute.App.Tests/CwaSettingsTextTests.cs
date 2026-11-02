using PaperRoute.App;
using PaperRoute.Config;

namespace PaperRoute.App.Tests;

public sealed class CwaSettingsTextTests
{
    [Fact]
    public void Contains_every_value_cwa_needs()
    {
        var config = new AppConfig(Listen: "127.0.0.1:2626", AuthLogin: "cwa", AuthPassword: "Secret123", Recipient: "kindle@paperroute.local");

        var lines = CwaSettingsText.Build(config).Split(Environment.NewLine);

        Assert.Equal(
            [
                "SMTP hostname: host.docker.internal",
                "SMTP port: 2626",
                "Encryption: None",
                "SMTP login: cwa",
                "SMTP password: Secret123",
                "From e-mail: cwa@localhost",
                "eReader e-mail (user profile): kindle@paperroute.local",
            ],
            lines);
    }
}
