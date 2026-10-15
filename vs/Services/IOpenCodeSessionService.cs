using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Manages OpenCode sessions via the HTTP API.
    /// </summary>
    public interface IOpenCodeSessionService
    {
        /// <summary>
        /// Lists all sessions for the given directory.
        /// </summary>
        Task<List<Models.Session>> ListSessionsAsync(string directory);
        Task<Models.Session> GetSessionAsync(string id, string directory);

        /// <summary>
        /// Время последнего сообщения чата (мс). 0 — сообщений нет, -1 — неизвестно.
        /// </summary>
        Task<long> GetLastMessageTimeAsync(string id, string directory);

        /// <summary>
        /// Creates a new session for the given directory.
        /// </summary>
        Task<Models.Session> CreateSessionAsync(string directory, string title = null);

        /// <summary>
        /// Gets path information for the given directory.
        /// </summary>
        Task<Models.PathInfo> GetPathAsync(string directory);

        /// <summary>
        /// Gets the server's current path info (without directory filter).
        /// </summary>
        Task<Models.PathInfo> GetServerPathAsync();

        /// <summary>
        /// Lists all projects for the given directory.
        /// </summary>
        Task<List<Models.ProjectInfo>> ListProjectsAsync(string directory);

        /// <summary>
        /// Gets the current project for the given directory.
        /// </summary>
        Task<Models.ProjectInfo> GetCurrentProjectAsync(string directory);
    }
}
