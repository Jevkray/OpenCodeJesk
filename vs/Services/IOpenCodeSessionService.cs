using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Управляет сессиями OpenCode через HTTP API.
    /// </summary>
    public interface IOpenCodeSessionService
    {
        /// <summary>
        /// Список всех сессий для указанного каталога.
        /// </summary>
        Task<List<Models.Session>> ListSessionsAsync(string directory);
        Task<Models.Session> GetSessionAsync(string id, string directory);

        /// <summary>
        /// Время последнего сообщения чата (мс). 0 — сообщений нет, -1 — неизвестно.
        /// </summary>
        Task<long> GetLastMessageTimeAsync(string id, string directory);

        /// <summary>
        /// Создаёт новую сессию для указанного каталога.
        /// </summary>
        Task<Models.Session> CreateSessionAsync(string directory, string title = null);

        /// <summary>
        /// Информация о путях для указанного каталога.
        /// </summary>
        Task<Models.PathInfo> GetPathAsync(string directory);

        /// <summary>
        /// Текущая информация о путях сервера (без фильтра по каталогу).
        /// </summary>
        Task<Models.PathInfo> GetServerPathAsync();

        /// <summary>
        /// Список всех проектов для указанного каталога.
        /// </summary>
        Task<List<Models.ProjectInfo>> ListProjectsAsync(string directory);

        /// <summary>
        /// Текущий проект для указанного каталога.
        /// </summary>
        Task<Models.ProjectInfo> GetCurrentProjectAsync(string directory);
    }
}
