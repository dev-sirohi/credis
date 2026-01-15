using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using Credis.Utils;

namespace Credis
{
    internal class Server
    {
        private IPAddress _ipAddr;
        private int _port;
        private CancellationToken _cancellationToken = CancellationToken.None;

        public App.Constants.Env Env { get; set; } = App.Constants.Env.TEST;
        public string IpAddress
        {
            get => _ipAddr.ToString();
        }
        public int Port
        {
            get => _port;
        }

        public Server(
            IPAddress ipAddr,
            int port,
            CancellationToken cancellationToken)
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
            _cancellationToken = cancellationToken;
        }

        public Server(
            string hostName,
            int port,
            CancellationToken cancellationToken)
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
            _cancellationToken = cancellationToken;
        }

        public Server(CancellationToken cancellationToken)
        {
            _ipAddr = Dns.GetHostAddresses(Dns.GetHostName()).First(x => x != null);
            _port = 6397;
            _cancellationToken = cancellationToken;
        }

        /*
            Public methods 
        */

        public async Task InitializeAsync()
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
                Console.WriteLine(string.Format(App.Constants.ExceptionText.CANCELLATION_REQUESTED, DateTime.UtcNow));
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
}
