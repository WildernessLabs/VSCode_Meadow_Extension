using Meadow.Hcom;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace Meadow
{
    static class MeadowConnection
    {
        static readonly int RETRY_COUNT = 10;
        static readonly int RETRY_DELAY = 500;

        internal static IMeadowConnection GetCurrentConnection(string port, ILogger logger)
        {
            if (meadowConnection != null &&
                meadowConnection.Name == port)
            {
                return meadowConnection;
            }
            else if (meadowConnection != null)
            {
                meadowConnection.Dispose();
                meadowConnection = null;
            }

            var retryCount = 0;

        get_serial_connection:
            try
            {
                meadowConnection = new SerialConnection(port, logger);
            }
            catch
            {
                retryCount++;
                if (retryCount > RETRY_COUNT)
                {
                    throw new Exception($"Cannot create SerialConnection on port: {port}");
                }
                Thread.Sleep(RETRY_DELAY);
                goto get_serial_connection;
            }

            return meadowConnection;
        }

        private static IMeadowConnection meadowConnection = null;
    }
}