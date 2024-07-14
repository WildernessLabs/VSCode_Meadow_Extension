using System;
using System.Threading.Tasks;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow
{
    static class MeadowConnection
    {
        private static readonly int RETRY_COUNT = 10;
        private static readonly int RETRY_DELAY = 500;
        private static readonly object connectionLock = new();
        private static IMeadowConnection meadowConnection = null;

        internal static async Task<IMeadowConnection> GetSelectedConnection(string port, ILogger logger)
        {
            lock (connectionLock)
            {
                if (meadowConnection != null && meadowConnection.Name == port)
                {
                    return meadowConnection;
                }
                else if (meadowConnection != null)
                {
                    meadowConnection.Dispose();
                    meadowConnection = null;
                }
            }

            for (int retryCount = 0; retryCount <= RETRY_COUNT; retryCount++)
            {
                try
                {
                    var newConnection = new SerialConnection(port, logger);
                    lock (connectionLock)
                    {
                        meadowConnection = newConnection;
                    }
                    return meadowConnection;
                }
                catch
                {
                    if (retryCount == RETRY_COUNT)
                    {
                        throw new Exception($"Cannot create SerialConnection on port: {port}");
                    }

                    await Task.Delay(RETRY_DELAY);
                }
            }

            // This line should never be reached because the loop will either return or throw
            throw new Exception("Unexpected error in GetCurrentConnection");
        }
    }
}