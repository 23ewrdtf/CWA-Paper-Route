using System.Globalization;
using System.Net.Sockets;

using PaperRoute.Config;
using PaperRoute.Delivery;
using PaperRoute.Logging;

using SmtpServer;
using SmtpServer.Authentication;
using SmtpServer.Net;
using SmtpServer.Storage;

using SmtpServerProvider = SmtpServer.ComponentModel.ServiceProvider;

namespace PaperRoute.Smtp;

/// <summary>The local SMTP server: starts, restarts with new settings, and reports deliveries and refusals.</summary>
public sealed class SmtpHost : IAsyncDisposable
{
    /// <summary>Most SMTP sessions served at the same time.</summary>
    public const int MaxSessions = 4;

    private const long BytesPerMegabyte = 1024 * 1024;

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);

    private readonly IDeviceLocator _locator;
    private readonly IActivityLog _log;
    private readonly Func<AppConfig, IBookConverter?> _converters;
    private readonly SmtpTimeouts _timeouts;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);

    // One gate for all listeners: during a port change the old and new listener must not copy at once.
    private readonly Lock _deliveryGate = new();
    private Listener? _current;

    /// <summary>Initializes a new instance of the <see cref="SmtpHost"/> class.</summary>
    /// <param name="config">Initial settings.</param>
    /// <param name="locator">Finds connected readers.</param>
    /// <param name="log">Activity log for deliveries and security events.</param>
    /// <param name="converters">Creates the EPUB converter for given settings; <see langword="null"/> means no conversion.</param>
    /// <param name="timeouts">SMTP time limits; <see cref="SmtpTimeouts.Default"/> when <see langword="null"/>.</param>
    public SmtpHost(AppConfig config, IDeviceLocator locator, IActivityLog log, Func<AppConfig, IBookConverter?>? converters = null, SmtpTimeouts? timeouts = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(log);
        Config = config;
        _locator = locator;
        _log = log;
        _converters = converters ?? (_ => null);
        _timeouts = timeouts ?? SmtpTimeouts.Default;
    }

    /// <summary>Raised after every received message with its outcome.</summary>
    public event EventHandler<DeliveredEventArgs>? Delivered;

    /// <summary>Raised when a connection, login or message is refused for security reasons.</summary>
    public event EventHandler<SecurityWarningEventArgs>? SecurityWarning;

    /// <summary>
    /// The library's own hard size limit: a backstop above <see cref="AppConfig.MaxMessageMB"/> so that
    /// slightly-too-large books get Paper Route's 552 text, while huge messages are cut off while
    /// streaming instead of being buffered in memory.
    /// </summary>
    /// <param name="maxMessageMB">Configured limit in megabytes.</param>
    /// <returns>Hard limit in bytes: configured limit plus 10% (at least 1 MB).</returns>
    public static long HardLimitBytes(int maxMessageMB)
    {
        var limit = maxMessageMB * BytesPerMegabyte;
        return limit + Math.Max(BytesPerMegabyte, limit / 10);
    }

    /// <summary>Gets the settings the listener is running with.</summary>
    public AppConfig Config { get; private set; }

    /// <summary>Starts listening with <see cref="Config"/>.</summary>
    /// <returns>Whether the listener started, and why not.</returns>
    public async Task<StartResult> StartAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_current is not null)
            {
                return new StartResult(true, null);
            }

            var (listener, result) = await TryStartAsync(Config).ConfigureAwait(false);
            _current = listener;
            return result;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <summary>Switches to new settings. On failure the previous listener keeps running unchanged.</summary>
    /// <param name="newConfig">Settings to apply.</param>
    /// <returns>Whether the new settings are active, and why not.</returns>
    public async Task<StartResult> RestartAsync(AppConfig newConfig)
    {
        ArgumentNullException.ThrowIfNull(newConfig);
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            var samePort = _current is not null
                && string.Equals(newConfig.Listen, Config.Listen, StringComparison.OrdinalIgnoreCase);
            if (samePort)
            {
                // The port is ours already: stop first, and bring the old listener back if the new one fails.
                await _current!.DisposeAsync().ConfigureAwait(false);
                _current = null;
            }

            var (listener, result) = await TryStartAsync(newConfig).ConfigureAwait(false);
            if (!result.Ok)
            {
                if (samePort)
                {
                    (_current, _) = await TryStartAsync(Config).ConfigureAwait(false);
                }

                return result;
            }

            if (_current is not null)
            {
                await _current.DisposeAsync().ConfigureAwait(false);
            }

            _current = listener;
            Config = newConfig;
            return result;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_current is not null)
            {
                await _current.DisposeAsync().ConfigureAwait(false);
                _current = null;
            }
        }
        finally
        {
            _lifecycle.Release();
        }

        _lifecycle.Dispose();
    }

    private static string PortError(SocketException e, int port) => e.SocketErrorCode switch
    {
        SocketError.AddressAlreadyInUse => string.Create(CultureInfo.InvariantCulture, $"Port {port} is already in use by another program"),
        SocketError.AccessDenied => string.Create(CultureInfo.InvariantCulture, $"Port {port} is reserved by Windows or another program - choose another port"),
        _ => string.Create(CultureInfo.InvariantCulture, $"Could not listen on port {port}: {e.Message}"),
    };

    private void Note(string text) =>
        _log.Write(string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {text}"));

    private void Warn(string text)
    {
        _log.Write(string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} SECURITY {text}"));
        SecurityWarning?.Invoke(this, new SecurityWarningEventArgs(text));
    }

    private async Task<(Listener? Listener, StartResult Result)> TryStartAsync(AppConfig config)
    {
        System.Net.IPEndPoint endpoint;
        ClientFilter clients;
        try
        {
            endpoint = AppConfig.ParseListen(config.Listen);
            clients = new ClientFilter(config.AllowedClientsOrDefault);
        }
        catch (ConfigException e)
        {
            return (null, new StartResult(false, e.Message));
        }

        var guard = new SessionGuard(config, clients, Warn, Note);
        var delivery = new DeliveryService(_locator, config, _log, _converters(config), _deliveryGate);
        var options = new SmtpServerOptionsBuilder()
            .ServerName("Paper Route")
            .Endpoint(builder => builder
                .Endpoint(endpoint)
                .AllowUnsecureAuthentication(true)
                .AuthenticationRequired(false)
                .SessionTimeout(_timeouts.Session))
            // PaperRouteMailboxFilter (declared SIZE) and PaperRouteMessageStore (actual size) enforce MaxMessageMB
            // with our text; the library's Strict limit above it is the memory backstop.
            .MaxMessageSize(checked((int)HardLimitBytes(config.MaxMessageMB)), MaxMessageSizeHandling.Strict)
            .CommandWaitTimeout(_timeouts.Command)
            .Build();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listeners = new EndpointListenerFactory();
        listeners.EndpointStarted += (_, _) => started.TrySetResult();

        var services = new SmtpServerProvider();
        services.Add(listeners);
        services.Add((IUserAuthenticator)new PaperRouteUserAuthenticator(guard));
        services.Add((IMailboxFilter)new PaperRouteMailboxFilter(guard));
        services.Add((IMessageStore)new PaperRouteMessageStore(guard, delivery, outcome => Delivered?.Invoke(this, new DeliveredEventArgs(outcome))));

        var listener = new Listener(new SmtpServer.SmtpServer(options, services));
        var running = listener.Start();
        var first = await Task.WhenAny(started.Task, running, Task.Delay(StartTimeout)).ConfigureAwait(false);
        if (first == started.Task)
        {
            return (listener, new StartResult(true, null));
        }

        await listener.DisposeAsync().ConfigureAwait(false);
        var error = running.Exception?.GetBaseException() switch
        {
            SocketException e => PortError(e, endpoint.Port),
            { } e => e.Message,
            null => string.Create(CultureInfo.InvariantCulture, $"Could not start listening on port {endpoint.Port}"),
        };
        return (null, new StartResult(false, error));
    }

    /// <summary>One running SmtpServer instance and its session counter.</summary>
    private sealed class Listener : IAsyncDisposable
    {
        private const string CountedKey = "PaperRoute:Counted";

        private readonly SmtpServer.SmtpServer _server;
        private readonly CancellationTokenSource _stop = new();
        private Task _running = Task.CompletedTask;
        private int _sessions;

        public Listener(SmtpServer.SmtpServer server)
        {
            _server = server;
            _server.SessionCreated += (_, e) =>
            {
                e.Context.Properties[CountedKey] = true;
                if (Interlocked.Increment(ref _sessions) > MaxSessions)
                {
                    e.Context.Properties[SessionGuard.OverLimitKey] = true;
                }
            };
            _server.SessionCompleted += (_, e) => Release(e.Context);
            _server.SessionFaulted += (_, e) => Release(e.Context);
            _server.SessionCancelled += (_, e) => Release(e.Context);
        }

        public Task Start() => _running = _server.StartAsync(_stop.Token);

        // Only sessions that were counted are released, so the counter cannot drift.
        private void Release(ISessionContext context)
        {
            if (context.Properties.Remove(CountedKey))
            {
                Interlocked.Decrement(ref _sessions);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync().ConfigureAwait(false);
            try
            {
                await _running.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
            catch (SocketException)
            {
                // The listener never started; nothing to stop.
            }

            _stop.Dispose();
        }
    }
}
