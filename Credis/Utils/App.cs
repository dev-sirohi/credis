namespace Credis.Utils;

internal static class App
{
    internal static class Constants
    {
        internal enum Env
        {
            TEST,
            PROD
        }

        internal static class Terminal
        {
            internal static class Text
            {
                public const string WELCOME            = "";
                public const string LOOP_BASIC         = "Proceed to initialization? (Y/N/Exit/Change Config)";
                public const string SERVER_INITIALIZED = "Server initialized at: ";
                public const string INVALID_INPUT      = "";
            }

            internal static class Error
            {
                public const string ARGUMENT_NULL_EX      = "";
                public const string ARGUMENT_OUT_OF_RANGE = "";
                public const string APP_EX                = "";
                public const string ARGUMENT_EX           = "";
            }
        }

        internal static class ExceptionText
        {
            public const string INVALID_IP_ADDRESS = "Invalid Ip address";
            public const string INVALID_PORT       = "Invalid port";
            public const string INVALID_HOSTNAME   = "Invalid hostname";

            public const string BUFFER_SIZE_OUT_OF_BOUNDS =
                "Buffer size out of bounds. Max supported buffer size: {0}B";

            public const string CANCELLATION_REQUESTED =
                "{0}:Process terminated. Cancellation requested. Server shutting down.";

            public const string APPLICATION_ERROR = "Unknown application error. Please try again after some time.";
        }

        internal static class OutgoingText
        {
            public const string PONG                       = "PONG";
            public const string DEFAULT                    = "{0}";
            public const string DEFAULT_WITH_SERVER_CONFIG = "{0}::Connection live at {1}:{2}";
        }
    }

    internal static class Converters
    {
        public static byte GetByte(char c)
        {
            return GlobalVariables.Encoding.GetBytes(new char[1] { c })[0];
        }

        public static byte[] GetBytes(char[] charArray)
        {
            return GlobalVariables.Encoding.GetBytes(charArray);
        }

        public static byte[] GetBytes(string s)
        {
            return GlobalVariables.Encoding.GetBytes(s);
        }

        public static Memory<byte> GetMemoryBytes(byte[] byteArray)
        {
            return new Memory<byte>(byteArray);
        }

        public static ReadOnlySpan<byte> GetReadOnlySpan(byte[] byteArray)
        {
            return new ReadOnlySpan<byte>(byteArray);
        }

        public static ReadOnlySpan<byte> GetReadOnlySpan(string s)
        {
            return new ReadOnlySpan<byte>(GetBytes(s));
        }
    }
}