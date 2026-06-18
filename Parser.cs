using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace Credis;

public class Parser
{
    public static class ParseRules
    {
        public enum CommandType
        {
            NONE,
            INVALID,
            SET,
            GET,
            DELETE,
            EXISTS,
            INCREMENT,
            DECREMENT,
            EXPIRE,
            TTL,
        }

        /*
            Wire format of a single request:

                    [4 bytes] payload length, big-endian Int32 (the bytes AFTER this prefix)
                    cmd\n                   e.g. set
                    arg1\n                  e.g. the key
                    arg2\n                  e.g. the value, when the command takes one

            Every line is terminated by '\n', including the last one. The server answers
            with exactly one '\n'-terminated line carrying the result of the command.

            Examples:
                    set\nname\nalice\n      -> alice
                    get\nname\n             -> alice   (or NULL when the key is missing)
                    expire\nname\n60\n      -> 1       (0 when the key does not exist)
                    ttl\nname\n             -> 60      (-1 no expiry, -2 missing)
        */

        public static CommandType GetCommandType(string cmd = "")
        {
            switch (cmd.ToLower())
            {
                case "set":
                    return CommandType.SET;
                case "get":
                    return CommandType.GET;
                case "delete":
                    return CommandType.DELETE;
                case "exists":
                    return CommandType.EXISTS;
                case "increment":
                    return CommandType.INCREMENT;
                case "decrement":
                    return CommandType.DECREMENT;
                case "expire":
                    return CommandType.EXPIRE;
                case "ttl":
                    return CommandType.TTL;
                default:
                    return CommandType.INVALID;
            }
        }
    }

    /*
        The reason I have chosen 2 << 12 (~8000) as MAX_BUFFER_SIZE is because an in-memory database is supposed to be FAST.
        It is not a file-system database that requires storing large amounts of data.
        If anybody using an in-memory database requires more than ~8000 characters of data to be cached, they need to reconsider their project requirements.
        But since I am no "Chris Sawyer", I accept this might be a mistake.
        Regardless, for now I will keep it as a hard-limit and maybe later add it as a configurable option.
    */

    private const int MAX_BUFFER_SIZE = 2 << 12;

    private int _edgePtr = 0;
    private int _inputPtr = 0;
    private int _outPtr = 0;
    private int _expectedLength = 0;
    private byte[] _inputBuffer = new byte[0];
    private byte[] _outputBuffer = new byte[0];
    private bool _processingPreviousCommand = false;

    private ParseRules.CommandType _currentCommand = ParseRules.CommandType.NONE;

    private NetworkStream _stream;
    private CancellationToken _cnt;

    private DataStructures ds = DataStructures.Instance;

    public Parser(NetworkStream stream, CancellationToken cnt)
    {
        _cnt = cnt;
        _stream = stream;

        _inputBuffer = new byte[MAX_BUFFER_SIZE];
        _outputBuffer = new byte[MAX_BUFFER_SIZE];
    }

    public async Task ParseAsync()
    {
        try
        {
            bool closeConnection = false;
            while (!closeConnection)
            {
                _cnt.ThrowIfCancellationRequested();

                int bytesRead = 0;
                if (GetEdgePtr() < MAX_BUFFER_SIZE)
                {
                    bytesRead = await _stream.ReadAsync(_inputBuffer, GetEdgePtr(), MAX_BUFFER_SIZE - GetEdgePtr(), _cnt);
                    if (bytesRead == 0)
                    {
                        if (IsEOF(GetInputPtr(), _inputBuffer, GetEdgePtr()))
                        {
                            closeConnection = true;
                        }
                    }
                    _edgePtr += bytesRead;
                }

                if (_processingPreviousCommand)
                {
                    await ProcessCommandAsync();
                }
                else
                {
                    await ProcessNewCommandAsync();
                }

                if (!_processingPreviousCommand)
                {
                    int outputLength = GetBufferedContentLength(_outputBuffer);
                    await _stream.WriteAsync(_outputBuffer, 0, outputLength, _cnt);
                    ClearBufferedContent(_outputBuffer, 0, outputLength);
                    _outPtr = 0;
                    ClearBufferedContent(_inputBuffer, 0, GetInputPtr());
                    _currentCommand = ParseRules.CommandType.NONE;

                    // Once everything in the buffer has been consumed, rewind both pointers
                    // back to the front. Without this a long-lived connection keeps marching
                    // them forward until a command straddles the end of the buffer.
                    if (GetInputPtr() == GetEdgePtr())
                    {
                        _inputPtr = 0;
                        _edgePtr = 0;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private async Task ProcessNewCommandAsync()
    {
        if (GetEdgePtr() < 4)
        {
            // Not enough data
            return;
        }
        _expectedLength = BinaryPrimitives.ReadInt32BigEndian(_inputBuffer.AsSpan(GetInputPtr(), 4));
        _inputPtr += 4;
        SkipEOL();
        _processingPreviousCommand = true;
        await ProcessCommandAsync();
    }

    private async Task ProcessCommandAsync()
    {
        if (GetEdgePtr() - GetInputPtr() < _expectedLength)
        {
            return;
        }
        if (_currentCommand == ParseRules.CommandType.NONE)
        {
            var cmdLineSb = new StringBuilder();
            if (!GetLine(cmdLineSb))
            {
                cmdLineSb.Clear();
                return;
            }
            string cmd = cmdLineSb.ToString();
            _currentCommand = ParseRules.GetCommandType(cmd);
        }
        if (_currentCommand == ParseRules.CommandType.INVALID)
        {
            throw new Exception("Unsupported command type");
        }
        switch (_currentCommand)
        {
            case ParseRules.CommandType.SET:
                await ProcessSetCommandAsync();
                break;
            case ParseRules.CommandType.GET:
                await ProcessGetCommandAsync();
                break;
            case ParseRules.CommandType.DELETE:
                await ProcessDeleteCommandAsync();
                break;
            case ParseRules.CommandType.EXISTS:
                await ProcessExistsCommandAsync();
                break;
            case ParseRules.CommandType.INCREMENT:
                await ProcessIncrementCommandAsync();
                break;
            case ParseRules.CommandType.DECREMENT:
                await ProcessDecrementCommandAsync();
                break;
            case ParseRules.CommandType.EXPIRE:
                await ProcessExpireCommandAsync();
                break;
            case ParseRules.CommandType.TTL:
                await ProcessTtlCommandAsync();
                break;
        }
    }

    private async Task ProcessSetCommandAsync()
    {
        var keySb = new StringBuilder();
        var valueSb = new StringBuilder();
        if (!GetLine(keySb) || !GetLine(valueSb))
        {
            keySb.Clear();
            valueSb.Clear();
            return;
        }

        _processingPreviousCommand = false;

        string key = keySb.ToString();
        string value = valueSb.ToString();

        string setValue = ds.Set(key, value);
        WriteLine(ref setValue);
    }

    private async Task ProcessGetCommandAsync()
    {
        var keySb = new StringBuilder();
        if (!GetLine(keySb))
        {
            keySb.Clear();
            return;
        }

        _processingPreviousCommand = false;

        string key = keySb.ToString();

        string value = ds.Get(key) ?? "NULL";
        WriteLine(ref value);
    }

    private async Task ProcessDeleteCommandAsync()
    {
        var keySb = new StringBuilder();
        if (!GetLine(keySb))
        {
            keySb.Clear();
            return;
        }

        _processingPreviousCommand = false;

        string key = keySb.ToString();

        string result = ds.Delete(key) ? "OK" : "NULL";
        WriteLine(ref result);
    }

    private async Task ProcessExistsCommandAsync()
    {
        var keySb = new StringBuilder();
        if (!GetLine(keySb))
        {
            keySb.Clear();
            return;
        }

        _processingPreviousCommand = false;

        string key = keySb.ToString();

        string result = ds.Exists(key) ? "1" : "0";
        WriteLine(ref result);
    }

    private async Task ProcessIncrementCommandAsync()
    {
        var keySb = new StringBuilder();
        var valueSb = new StringBuilder();
        if (!GetLine(keySb) || !GetLine(valueSb))
        {
            keySb.Clear();
            valueSb.Clear();
            return;
        }

        _processingPreviousCommand = false;

        string key = keySb.ToString();
        long value = 0;
        Int64.TryParse(valueSb.ToString(), out value);

        string incrementedValue = ds.Increment(key, value).ToString();
        WriteLine(ref incrementedValue);
    }

    private async Task ProcessDecrementCommandAsync()
    {
        var keySb = new StringBuilder();
        var valueSb = new StringBuilder();
        if (!GetLine(keySb) || !GetLine(valueSb))
        {
            keySb.Clear();
            valueSb.Clear();
            return;
        }

        _processingPreviousCommand = false;

        string key = keySb.ToString();
        long value = 0;
        Int64.TryParse(valueSb.ToString(), out value);

        string decrementedValue = ds.Decrement(key, value).ToString();
        WriteLine(ref decrementedValue);
    }

    private async Task ProcessExpireCommandAsync()
    {
        var keySb = new StringBuilder();
        var valueSb = new StringBuilder();
        if (!GetLine(keySb) || !GetLine(valueSb))
        {
            keySb.Clear();
            valueSb.Clear();
            return;
        }

        _processingPreviousCommand = false;

        string key = keySb.ToString();
        long seconds = 0;
        Int64.TryParse(valueSb.ToString(), out seconds);

        string result = ds.Expire(key, seconds) ? "1" : "0";
        WriteLine(ref result);
    }

    private async Task ProcessTtlCommandAsync()
    {
        var keySb = new StringBuilder();
        if (!GetLine(keySb))
        {
            keySb.Clear();
            return;
        }

        _processingPreviousCommand = false;

        string key = keySb.ToString();

        string result = ds.Ttl(key).ToString();
        WriteLine(ref result);
    }

    private bool GetLine(StringBuilder line)
    {
        int _ptrTemp = GetInputPtr();
        while (!IsEOF(_ptrTemp, _inputBuffer, GetEdgePtr()) && !IsEOL(_ptrTemp, _inputBuffer))
        {
            line.Append((char)_inputBuffer[_ptrTemp]);
            _ptrTemp++;
        }
        if (!IsEOF(_ptrTemp, _inputBuffer, GetEdgePtr()) && !IsEOL(_ptrTemp, _inputBuffer))
        {
            return false;
        }
        _inputPtr += _ptrTemp - GetInputPtr();
        SkipEOL();
        return true;
    }

    private void SkipEOL()
    {
        if (_inputBuffer[GetInputPtr()] == '\n')
        {
            _inputPtr++;
        }
    }

    private bool IsEOL(int _ptrTemp, byte[] buffer)
    {
        return buffer[_ptrTemp] == '\n';
    }

    public bool IsEOF(int _ptrTemp, byte[] buffer, int _edgePtrTemp)
    {
        return buffer[_ptrTemp] == '\0' || _ptrTemp > _edgePtrTemp;
    }

    public int GetBufferedContentLength(byte[] buffer)
    {
        int count = 0;
        int ptr = 0;
        while (!IsEOF(ptr, buffer, MAX_BUFFER_SIZE))
        {
            count++;
            ptr++;
        }
        return count;
    }

    public void WriteLine(ref string outputLine)
    {
        foreach (char c in outputLine)
        {
            if (_outPtr >= MAX_BUFFER_SIZE)
            {
                break;
            }
            _outputBuffer[_outPtr] = (byte)c;
            _outPtr++;
        }
        _outputBuffer[_outPtr] = (byte)'\n';
        _outPtr++;
    }

    private void ClearBufferedContent(byte[] buffer, int offset, int length)
    {
        for (int ptr = offset; ptr < length; ptr++)
        {
            buffer[ptr] = (byte)'\0';
        }
    }

    private int GetInputPtr()
    {
        return _inputPtr % MAX_BUFFER_SIZE;
    }

    private int GetEdgePtr()
    {
        return _edgePtr % MAX_BUFFER_SIZE;
    }
}
