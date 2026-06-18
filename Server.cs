using System.Net;
using System.Net.Sockets;

namespace Credis;

public class Server
{
    public enum ServerStatus
    {
        INITIALIZING,
        ACTIVE,
        SHUTTING_DOWN,
        FAULTED,
        CANCELLED,
    }

    private const string LOCALHOST = "127.0.0.1";
    private const int DEFAULT_PORT = 5050;

    public class ServerConfig
    {
        public string IpAddress { get; set; } = LOCALHOST;
        public int Port { get; set; } = DEFAULT_PORT;
    }

    private ServerStatus _serverStatus;
    private string _faultMsg = string.Empty;
    private CancellationToken _cnt;
    private TcpListener? _tcpListener;
    private IPAddress _ipAddr;
    private int _port = DEFAULT_PORT;

    public Server(ServerConfig config)
    {
        /*
            Expand config into Server options. DO NOT use ServerConfig as a local variable.
        */

        _ipAddr = GetIpAddr();
        _port = config.Port;
    }

    public async Task StartAsync(CancellationToken cnt)
    {
        _cnt = cnt;
        _serverStatus = ServerStatus.INITIALIZING;
        try
        {
            _cnt.ThrowIfCancellationRequested();

            // Create a TCP Listener
            _ipAddr = GetIpAddr();
            _tcpListener = new TcpListener(_ipAddr, _port);
            _tcpListener.Start();
            _serverStatus = ServerStatus.ACTIVE;
            while (true)
            {
                _cnt.ThrowIfCancellationRequested();
                var tcpClient = await _tcpListener.AcceptTcpClientAsync(_cnt);
                if (tcpClient.Connected)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var networkStream = tcpClient.GetStream();
                            var parser = new Parser(networkStream, _cnt);
                            await parser.ParseAsync();
                        }
                        finally
                        {
                            tcpClient.Close();
                        }
                    });
                }
            }
        }
        catch (OperationCanceledException opEx)
        {
            _serverStatus = ServerStatus.CANCELLED;
            _faultMsg = opEx.Message;
        }
        catch (Exception ex)
        {
            _serverStatus = ServerStatus.FAULTED;
            _faultMsg = ex.Message;
        }
        finally
        {
            _tcpListener?.Stop();
            _tcpListener?.Dispose();
        }
    }

    /* Helper methods */

    public ServerStatus GetStatus()
    {
        return _serverStatus;
    }

    public string GetFaultMessage()
    {
        return _faultMsg;
    }

    public IPAddress GetIpAddr(string ipAddrStr = LOCALHOST)
    {
        var ipAddr = IPAddress.Parse(ipAddrStr);
        return ipAddr;
    }
}
