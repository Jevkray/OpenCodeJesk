using System.Collections.Generic;
using Newtonsoft.Json;

namespace OpenCodeJesk.Models
{
    /// <summary>
    /// Represents an OpenCode session.
    /// </summary>
    public class Session
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("projectID")]
        public string ProjectId { get; set; }

        [JsonProperty("directory")]
        public string Directory { get; set; }

        [JsonProperty("parentID")]
        public string ParentId { get; set; }

        [JsonProperty("summary")]
        public SessionSummary Summary { get; set; }

        [JsonProperty("share")]
        public ShareInfo Share { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("time")]
        public SessionTime Time { get; set; }

        [JsonProperty("revert")]
        public RevertInfo Revert { get; set; }
    }

    public class SessionSummary
    {
        [JsonProperty("additions")]
        public int Additions { get; set; }

        [JsonProperty("deletions")]
        public int Deletions { get; set; }

        [JsonProperty("files")]
        public int Files { get; set; }

        [JsonProperty("diffs")]
        public List<FileDiff> Diffs { get; set; }
    }

    public class FileDiff
    {
        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("before")]
        public string Before { get; set; }

        [JsonProperty("after")]
        public string After { get; set; }

        [JsonProperty("additions")]
        public int Additions { get; set; }

        [JsonProperty("deletions")]
        public int Deletions { get; set; }
    }

    public class ShareInfo
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class SessionTime
    {
        [JsonProperty("created")]
        public long Created { get; set; }

        [JsonProperty("updated")]
        public long Updated { get; set; }

        [JsonProperty("compacting")]
        public long? Compacting { get; set; }
    }

    public class RevertInfo
    {
        [JsonProperty("messageID")]
        public string MessageId { get; set; }

        [JsonProperty("partID")]
        public string PartId { get; set; }

        [JsonProperty("snapshot")]
        public string Snapshot { get; set; }

        [JsonProperty("diff")]
        public string Diff { get; set; }
    }
}
