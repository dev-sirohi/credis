namespace Credis.Utils
{
    internal static class Protocol
    {
        internal static class Lexicon
        {
            internal static class Readable
            {
                internal static class Value
                {
                    public const char TRUE = 't';
                    public const char FALSE = 'f';

                    public const string TERMINATOR = "\r\n";
                    public const string PING = "PING";
                }

                internal static class Simple
                {
                    public const char STRING = '+';
                    public const char ERROR = '-';
                    public const char INTEGER = ':';
                    public const char NULL = '_';
                    public const char BOOLEAN = '#';
                    public const char DOUBLE = ',';
                    public const char BIG_NUMBER = '(';
                }

                internal static class Aggregate
                {
                    public const char STRING = '$';
                    public const char ARRAY = '*';
                    public const char ERROR = '!';
                    public const char MAP = '%';
                    public const char ATTRIBUTE = '|';
                    public const char SET = '~';
                    public const char PUSH = '>';
                    public const char VERBATIM = '=';
                }

                internal static class Error
                {
                    public const string GENERIC = "ERR";
                    public const string WRONGTYPE = "WRONGTYPE";
                }
            }
        }
    }
}
