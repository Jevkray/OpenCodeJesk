using System;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Следит за соединением с сервером OpenCode и шлёт события при смене состояния.
    /// </summary>
    public interface IConnectionMonitor : IDisposable
    {
        /// <summary>
        /// Запускает периодическую проверку доступности.
        /// </summary>
        void Start();

        /// <summary>
        /// Останавливает периодическую проверку доступности.
        /// </summary>
        void Stop();

        /// <summary>
        /// Событие потери соединения.
        /// </summary>
        event Action ConnectionLost;

        /// <summary>
        /// Событие восстановления соединения.
        /// </summary>
        event Action ConnectionRestored;

        /// <summary>
        /// Доступен ли сервер в данный момент.
        /// </summary>
        bool IsConnected { get; }
    }
}
