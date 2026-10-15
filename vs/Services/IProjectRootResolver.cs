namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Resolves the project root directory from Visual Studio solution or Git repository.
    /// </summary>
    public interface IProjectRootResolver
    {
        /// <summary>
        /// Returns the project root path with normalized forward slashes.
        /// Git root has higher priority than solution root.
        /// </summary>
        string ResolveProjectRoot();
    }
}
