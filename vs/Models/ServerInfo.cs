namespace OpenCodeJesk.Models
{
    /// <summary>
    /// Данные подключения к серверу.
    /// </summary>
    public class ServerInfo
    {
        public string Host { get; set; }

        public int Port { get; set; }

        /// <summary>
        /// Имя пользователя Basic Auth (в opencode по умолчанию "opencode").
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Пароль Basic Auth. Пусто, когда сервер без защиты (по умолчанию у v1).
        /// </summary>
        public string Password { get; set; }

        public bool HasAuth => !string.IsNullOrEmpty(Password);

        /// <summary>
        /// Полный базовый URL (например, http://localhost:4096).
        /// </summary>
        public string BaseUrl => $"http://{Host}:{Port}";

        public ServerInfo()
        {
            Host = "127.0.0.1";
            Port = 4096;
        }

        public ServerInfo(string host, int port)
        {
            Host = host;
            Port = port;
        }
    }
}
