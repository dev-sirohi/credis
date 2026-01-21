using System.Net;
using System.Net.Sockets;
using Credis.Utils;

namespace Credis;

internal class Server
{
    private readonly CancellationToken _cancellationToken = CancellationToken.None;
    private readonly IPAddress         _ipAddr;

    public Server(
        IPAddress ipAddr,
        int port,
        CancellationToken cancellationToken)
    {
        if (ipAddr == null) throw new ArgumentNullException(App.Constants.ExceptionText.INVALID_IP_ADDRESS);
        if (port   == default) throw new ArgumentNullException(App.Constants.ExceptionText.INVALID_PORT);

        _ipAddr            = ipAddr;
        Port               = port;
        _cancellationToken = cancellationToken;
    }

    public Server(
        string hostName,
        int port,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hostName))
            throw new ArgumentNullException(App.Constants.ExceptionText.INVALID_HOSTNAME);
        if (port == default) throw new ArgumentNullException(App.Constants.ExceptionText.INVALID_PORT);

        _ipAddr            = Dns.GetHostAddresses(hostName).First(x => x != null);
        Port               = port;
        _cancellationToken = cancellationToken;
    }

    public Server(CancellationToken cancellationToken)
    {
        _ipAddr            = Dns.GetHostAddresses(Dns.GetHostName()).First(x => x != null);
        Port               = 6397;
        _cancellationToken = cancellationToken;
    }

    public App.Constants.Env Env { get; set; } = App.Constants.Env.TEST;

    public string IpAddress => _ipAddr.ToString();

    public int Port { get; }

    /*
        Public methods
    */

    public async Task InitializeAsync()
    {
        if (_ipAddr == null) throw new ArgumentNullException(App.Constants.ExceptionText.INVALID_IP_ADDRESS);
        if (Port    == default) throw new ArgumentNullException(App.Constants.ExceptionText.INVALID_PORT);

        try
        {
            var ipEndPoint = new IPEndPoint(_ipAddr, Port);

            using (var listener = new TcpListener(ipEndPoint))
            {
                listener.Start(); // Starts queueing requests

                while (true)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    using (var client = await listener.AcceptTcpClientAsync(_cancellationToken))
                    {
                        _ = HandleClientRequestAsync(client);
                    }
                }
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == _cancellationToken)
        {
            Console.WriteLine(App.Constants.ExceptionText.CANCELLATION_REQUESTED, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _ = Logger.Instance.WriteAsync(ex);
        }
    }

    /*
        Private methods
    */

    private async Task HandleClientRequestAsync(TcpClient client)
    {
        await using (var stream = client.GetStream())
        {
            var outputBuffer = new Memory<byte>(new byte[GlobalVariables.MaxBufferSize]);
            await new Processor(this, stream, outputBuffer, _cancellationToken).InitializeAsync();
            await stream.WriteAsync(outputBuffer, _cancellationToken);
        }
    }
}