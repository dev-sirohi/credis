using Credis.Utils;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Credis
{
    internal class Processor
    {
        private Server? _serverInstance;

        public Processor(Server serverInstance)
        {
            _serverInstance = serverInstance;
        }

        public Processor() { }

        public async Task ProcessRequest(NetworkStream stream, Memory<byte> outputBuffer, CancellationToken cancellationToken)
        {
            if (!stream.CanRead || stream.Length == 0)
            {
                await GenerateDefaultResponse(outputBuffer, cancellationToken);
                return;
            }

            switch (stream.ReadByte())
            {
                case ((byte)Protocol.Lexicon.Readable.Simple.STRING):
                    await ProcessString(stream, outputBuffer, cancellationToken);
                    break;
                default:
                    var inputBuffer = new Memory<byte>();
                    await stream.ReadExactlyAsync(inputBuffer, cancellationToken);
                    if (inputBuffer.Span.SequenceEqual(App.Converters.GetReadOnlySpan(Protocol.Lexicon.Readable.Value.PING)))
                    {
                        outputBuffer = new Memory<byte>(App.Converters.GetBytes(App.Constants.OutgoingText.PONG));
                        return;
                    }

                    outputBuffer = App.Converters.GetBytes(Protocol.Lexicon.Readable.Error.WRONGTYPE);
                    return;

            }
        }

        public async Task ProcessString(NetworkStream stream, Memory<byte> outputBuffer, CancellationToken cancellationToken)
        {

        }

        public async Task GenerateDefaultResponse(Memory<byte> outputBuffer, CancellationToken cancellationToken)
        {
            if (_serverInstance == null)
            {
                outputBuffer = App.Converters.GetBytes(string.Format(App.Constants.OutgoingText.DEFAULT, DateTime.UtcNow));
            }
            else
            {
                outputBuffer = App.Converters.GetBytes(string.Format(App.Constants.OutgoingText.DEFAULT_WITH_SERVER_CONFIG, DateTime.UtcNow, _serverInstance.IpAddress, _serverInstance.Port));
            }
        }
    }
}
