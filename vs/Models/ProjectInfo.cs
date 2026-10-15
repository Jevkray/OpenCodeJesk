using Newtonsoft.Json;

namespace OpenCodeJesk.Models
{
    /// <summary>
    /// Project info from GET /project or /project/current.
    /// </summary>
    public class ProjectInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("worktree")]
        public string Worktree { get; set; }

        [JsonProperty("vcsDir")]
        public string VcsDir { get; set; }

        [JsonProperty("vcs")]
        public string Vcs { get; set; }

        [JsonProperty("time")]
        public ProjectTimeInfo Time { get; set; }
    }

    public class ProjectTimeInfo
    {
        [JsonProperty("created")]
        public long Created { get; set; }

        [JsonProperty("initialized")]
        public long? Initialized { get; set; }
    }
}
