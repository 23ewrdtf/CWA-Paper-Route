using System.Net;

using PaperRoute.Config;
using PaperRoute.Smtp;

namespace PaperRoute.Core.Tests;

public sealed class ClientFilterTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("::ffff:127.0.0.1", true)]
    [InlineData("192.168.1.20", false)]
    [InlineData("172.17.0.2", false)]
    public void Default_filter_allows_only_loopback(string ip, bool allowed) =>
        Assert.Equal(allowed, new ClientFilter(new AppConfig().AllowedClientsOrDefault).IsAllowed(IPAddress.Parse(ip)));

    [Fact]
    public void Cidr_range_is_honoured() =>
        Assert.True(new ClientFilter(["192.168.65.0/24"]).IsAllowed(IPAddress.Parse("192.168.65.254")));

    [Fact]
    public void Invalid_cidr_is_ConfigException() =>
        Assert.Throws<ConfigException>(() => new ClientFilter(["not-a-network"]));
}
