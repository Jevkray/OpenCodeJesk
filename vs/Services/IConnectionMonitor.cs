using System;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Monitors the connection to the OpenCode server and fires events on state changes.
    /// </summary>
    public interface IConnectionMonitor : IDisposable
    {
        /// <summary>
        /// Starts periodic health checking.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops periodic health checking.
        /// </summary>
        void Stop();

        /// <summary>
        /// Raised when the connection is lost.
        /// </summary>
        event Action ConnectionLost;

        /// <summary>
        /// Raised when the connection is restored.
        /// </summary>
        event Action ConnectionRestored;

        /// <summary>
        /// Whether the server is currently reachable.
        /// </summary>
        bool IsConnected { get; }
    }
}
