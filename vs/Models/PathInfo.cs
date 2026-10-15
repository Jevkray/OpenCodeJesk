using Newtonsoft.Json;

namespace OpenCodeJesk.Models
{
    /// <summary>
    /// Информация о путях, возвращаемая GET /path.
    /// </summary>
    public class PathInfo
    {
        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("config")]
        public string Config { get; set; }

        [JsonProperty("worktree")]
        public string Worktree { get; set; }

        [JsonProperty("directory")]
        public string Directory { get; set; }
    }
}
