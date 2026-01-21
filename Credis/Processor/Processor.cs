using System.Net.Sockets;
using Credis.Utils;

namespace Credis;

internal sealed class Processor
{
    private readonly CancellationToken _cancellationToken;
    private          Memory<byte>      _outputBuffer;
    private readonly Server?           _serverInstance;
    private readonly NetworkStream     _stream;

    public Processor(
        Server serverInstance,
        NetworkStream stream,
        Memory<byte> outputBuffer,
        CancellationToken cancellationToken)
    {
        _serverInstance    = serverInstance;
        _stream            = stream;
        _outputBuffer      = outputBuffer;
        _cancellationToken = cancellationToken;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (!_stream.CanRead || _stream.Length == 0)
            {
                GenerateDefaultResponse(_outputBuffer);

                return;
            }

            if (_stream.Length > GlobalVariables.MaxBufferSize)
            {
                _outputBuffer =
                    App.Converters.GetBytes(
                        $"{Protocol.Lexicon.Readable.Error.GENERIC}{string.Format(App.Constants.ExceptionText.BUFFER_SIZE_OUT_OF_BOUNDS, GlobalVariables.MaxBufferSize)}");

                return;
            }

            switch (_stream.ReadByte())
            {
                case (byte)Protocol.Lexicon.Readable.Simple.STRING:
                    await new SimpleProcessor(_stream, _outputBuffer, _cancellationToken).ProcessSimpleString();

                    break;
                default:
                    var inputBuffer = new Memory<byte>();
                    await _stream.ReadExactlyAsync(inputBuffer, _cancellationToken);
                    if (inputBuffer.Span.SequenceEqual(
                            App.Converters.GetReadOnlySpan(Protocol.Lexicon.Readable.Value.PING)))
                    {
                        _outputBuffer = new Memory<byte>(App.Converters.GetBytes(App.Constants.OutgoingText.PONG));

                        return;
                    }

                    _outputBuffer = App.Converters.GetBytes(Protocol.Lexicon.Readable.Error.WRONGTYPE);

                    return;
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == _cancellationToken)
        {
            _outputBuffer =
                App.Converters.GetBytes(string.Format(App.Constants.ExceptionText.CANCELLATION_REQUESTED,
                    DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _             = Logger.Instance.WriteAsync(ex);
            _outputBuffer = App.Converters.GetBytes(App.Constants.ExceptionText.APPLICATION_ERROR);
        }
    }

    private void GenerateDefaultResponse(Memory<byte> outputBuffer)
    {
        if (_serverInstance == null)
            outputBuffer = App.Converters.GetBytes(string.Format(App.Constants.OutgoingText.DEFAULT, DateTime.UtcNow));
        else
            outputBuffer = App.Converters.GetBytes(string.Format(App.Constants.OutgoingText.DEFAULT_WITH_SERVER_CONFIG,
                DateTime.UtcNow, _serverInstance.IpAddress, _serverInstance.Port));
    }

    public sealed class SimpleProcessor
    {
        private readonly CancellationToken _cancellationToken = CancellationToken.None;
        private          Memory<byte>      _outputBuffer;
        private          NetworkStream     _stream;

        public SimpleProcessor(
            NetworkStream stream,
            Memory<byte> outputBuffer,
            CancellationToken cancellationToken)
        {
            _stream            = stream;
            _outputBuffer      = outputBuffer;
            _cancellationToken = cancellationToken;
        }

        public async Task ProcessSimpleString()
        {
            _cancellationToken.ThrowIfCancellationRequested();

            var lastFourBytes = new MemoryStream();
        }
    }
}