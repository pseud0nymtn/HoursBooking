using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace HoursBooking.App.Services;

public sealed class AppSingleInstanceManager : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly IPrimaryInstanceHost _host;
    private Task? _listenerTask;

    private AppSingleInstanceManager(IPrimaryInstanceHost host)
    {
        _host = host;
    }

    public static AppSingleInstanceManager? TryCreatePrimary(string appId)
    {
        var instanceKey = BuildInstanceKey(appId);
        var host = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? TryCreatePipeHost(instanceKey)
            : TryCreateSocketHost(instanceKey);

        return host is null ? null : new AppSingleInstanceManager(host);
    }

    public static bool TrySignalPrimaryInstance(string appId)
    {
        var instanceKey = BuildInstanceKey(appId);
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? TrySignalPipePrimary(instanceKey)
            : TrySignalSocketPrimary(instanceKey);
    }

    public void StartListening(Action onActivationRequested)
    {
        if (_listenerTask is not null)
        {
            return;
        }

        _listenerTask = Task.Run(() => _host.ListenAsync(onActivationRequested, _cancellationTokenSource.Token));
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        try
        {
            _listenerTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }
        finally
        {
            _host.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }

    private static IPrimaryInstanceHost? TryCreatePipeHost(string instanceKey)
    {
        try
        {
            return new PipePrimaryInstanceHost(instanceKey);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool TrySignalPipePrimary(string instanceKey)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", instanceKey, PipeDirection.Out);
                client.Connect(200);
                using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: false)
                {
                    AutoFlush = true
                };
                writer.WriteLine("ACTIVATE");
                return true;
            }
            catch (TimeoutException)
            {
            }
            catch (IOException)
            {
            }

            Thread.Sleep(100);
        }

        return false;
    }

    private static IPrimaryInstanceHost? TryCreateSocketHost(string instanceKey)
    {
        var socketPath = BuildSocketPath(instanceKey);
        var endpoint = new UnixDomainSocketEndPoint(socketPath);
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        try
        {
            if (File.Exists(socketPath) && !CanConnectSocket(endpoint))
            {
                File.Delete(socketPath);
            }

            listener.Bind(endpoint);
            listener.Listen(1);
            return new SocketPrimaryInstanceHost(listener, socketPath);
        }
        catch (SocketException)
        {
            listener.Dispose();
            return null;
        }
        catch
        {
            listener.Dispose();
            throw;
        }
    }

    private static bool TrySignalSocketPrimary(string instanceKey)
    {
        var socketPath = BuildSocketPath(instanceKey);
        if (!File.Exists(socketPath))
        {
            return false;
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                client.Connect(new UnixDomainSocketEndPoint(socketPath));
                var payload = Encoding.UTF8.GetBytes("ACTIVATE\n");
                client.Send(payload);
                return true;
            }
            catch (SocketException)
            {
            }
            catch (IOException)
            {
            }

            Thread.Sleep(100);
        }

        return false;
    }

    private static bool CanConnectSocket(EndPoint endpoint)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Connect(endpoint);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static string BuildSocketPath(string instanceKey)
    {
        var baseDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.GetTempPath();
        }

        return Path.Combine(baseDirectory, $"{instanceKey}.sock");
    }

    private static string BuildInstanceKey(string appId)
    {
        var userName = Environment.UserName;
        var raw = $"{appId}_{userName}";
        var builder = new StringBuilder(raw.Length);
        foreach (var character in raw)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }

    private interface IPrimaryInstanceHost : IDisposable
    {
        Task ListenAsync(Action onActivationRequested, CancellationToken cancellationToken);
    }

    private sealed class PipePrimaryInstanceHost : IPrimaryInstanceHost
    {
        private readonly Mutex _mutex;
        private readonly string _pipeName;

        public PipePrimaryInstanceHost(string pipeName)
        {
            _pipeName = pipeName;
            _mutex = new Mutex(true, pipeName, out var createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();
                throw new IOException("Primary instance already exists.");
            }
        }

        public async Task ListenAsync(Action onActivationRequested, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(cancellationToken);
                    using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                    var message = await reader.ReadLineAsync(cancellationToken);
                    if (string.Equals(message, "ACTIVATE", StringComparison.Ordinal))
                    {
                        onActivationRequested();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                }
            }
        }

        public void Dispose()
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }

    private sealed class SocketPrimaryInstanceHost : IPrimaryInstanceHost
    {
        private readonly Socket _listener;
        private readonly string _socketPath;

        public SocketPrimaryInstanceHost(Socket listener, string socketPath)
        {
            _listener = listener;
            _socketPath = socketPath;
        }

        public async Task ListenAsync(Action onActivationRequested, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var client = await _listener.AcceptAsync(cancellationToken);
                    using var stream = new NetworkStream(client, ownsSocket: false);
                    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    var message = await reader.ReadLineAsync(cancellationToken);
                    if (string.Equals(message, "ACTIVATE", StringComparison.Ordinal))
                    {
                        onActivationRequested();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException)
                {
                }
                catch (IOException)
                {
                }
            }
        }

        public void Dispose()
        {
            _listener.Dispose();
            if (File.Exists(_socketPath))
            {
                File.Delete(_socketPath);
            }
        }
    }
}
