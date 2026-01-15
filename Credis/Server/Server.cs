using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Credis.Utils;

namespace Credis
{
    internal class Server
    {
        private IPAddress _ipAddr;
        private int _port;
        private bool _keepAlive = true;

        public App.Constants.Env Env { get; set; } = App.Constants.Env.TEST;
        public string IpAddress
        {
            get => _ipAddr.ToString();
        }
        public int Port
        {
            get => _port;
        }
        
        public Server(IPAddress ipAddr, int port, bool keepAlive = true)
        {
            if (ipAddr == null)
            {
                throw new ArgumentNullException(App.Constants.ExceptionText.INVALID_IP_ADDRESS);
            }
            if (port == default(int))
            {
                throw new ArgumentNullException(App.Constants.ExceptionText.INVALID_PORT);
            }

            _ipAddr = ipAddr;
            _port = port;
            _keepAlive = keepAlive;
        }

        public Server(string hostName, int port = 6379, bool keepAlive = true)
        {
            if (string.IsNullOrWhiteSpace(hostName))
            {
                throw new ArgumentNullException(App.Constants.ExceptionText.INVALID_HOSTNAME);
            }
            if (port == default(int))
            {
                throw new ArgumentNullException(App.Constants.ExceptionText.INVALID_PORT);
            }

            _ipAddr = Dns.GetHostAddresses(hostName).First(x => x != null);
            _port = port;
            _keepAlive = keepAlive;
        }

        public Server(bool keepAlive = false)
        {
            _ipAddr = Dns.GetHostAddresses(Dns.GetHostName()).First(x => x != null);
            _port = 6397;
            _keepAlive = keepAlive;
        }

        /*
            Public methods 
        */

        public async Task Initialize(CancellationToken cancellationToken)
        {
            if (_ipAddr == null)
            {
                throw new ArgumentNullException(App.Constants.ExceptionText.INVALID_IP_ADDRESS);
            }
            if (_port == default(int))
            {
                throw new ArgumentNullException(App.Constants.ExceptionText.INVALID_PORT);
            }

            try
            {
                IPEndPoint ipEndPoint = new IPEndPoint(_ipAddr, _port);

                using (var listener = new TcpListener(ipEndPoint))
                {
                    listener.Start();

                    while (_keepAlive)
                    {
                        using (var handler = await listener.AcceptTcpClientAsync())
                        {
                            // Use clientHashCode for rate-limiting
                            var clientHashCode = handler.Client.RemoteEndPoint?.GetHashCode();
                            await using (var stream = handler.GetStream())
                            {
                                if (stream.CanRead)
                                {
                                    var outputBuffer = new Memory<byte>();
                                    await new Processor(this).ProcessRequest(stream, outputBuffer, cancellationToken);
                                    await stream.WriteAsync(outputBuffer);
                                }
                            }
                        }
                    }

                    listener.Stop();
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException(ex.Message);
            }
        }
    }
}
