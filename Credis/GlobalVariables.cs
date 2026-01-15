using System;
using System.Collections.Generic;
using System.Text;

namespace Credis
{
    internal static class GlobalVariables
    {
        public static bool IsInitialized { get; private set; }
        public static Encoding Encoding { get; private set; } = Encoding.UTF8;
        public const int MaxBufferSize = 8192;

        public static void Initialize(Encoding encoding)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("Global variables cannot be re-initialized once app has started");
            }

            IsInitialized = true;
            Encoding = encoding;
        }
    }
}
