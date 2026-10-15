using System;
using System.IO;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace OpenCodeJesk.Services
{
    public class ProjectRootResolver : IProjectRootResolver
    {
        private readonly IServiceProvider _serviceProvider;

        public ProjectRootResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public string ResolveProjectRoot()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string solutionDir = GetSolutionDirectory();
            var mode = SpendSettings.Current?.StartDirectory ?? StartDirectoryMode.GitRoot;

            if (mode == StartDirectoryMode.LastUsed)
            {
                var last = ReadRememberedRoot();
                if (!string.IsNullOrEmpty(last) && Directory.Exists(last))
                    return last;
            }
            else if (mode == StartDirectoryMode.SolutionDir && !string.IsNullOrEmpty(solutionDir))
            {
                return Remember(NormalizePath(solutionDir));
            }
            else if (mode == StartDirectoryMode.ProjectDir)
            {
                var projectDir = FindProjectDirectory(solutionDir);
                if (!string.IsNullOrEmpty(projectDir))
                    return Remember(NormalizePath(projectDir));
            }

            // Корень Git из каталога решения (высший приоритет)
            string gitRoot = FindGitRoot(solutionDir);
            if (!string.IsNullOrEmpty(gitRoot))
                return Remember(NormalizePath(gitRoot));

            // Иначе — каталог решения
            if (!string.IsNullOrEmpty(solutionDir))
                return Remember(NormalizePath(solutionDir));

            // Корень Git из каталога профиля пользователя
            gitRoot = FindGitRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            if (!string.IsNullOrEmpty(gitRoot))
                return Remember(NormalizePath(gitRoot));

            // Решение не открыто. Переиспользуем последний известный проект,
            // чтобы история чатов не «сбрасывалась» при закрытии решения.
            var remembered = ReadRememberedRoot();
            if (!string.IsNullOrEmpty(remembered) && Directory.Exists(remembered))
                return remembered;

            // В крайнем случае — каталог «Документы»
            return NormalizePath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        }

        /// <summary>Каталог первого найденного .csproj в решении.</summary>
        private static string FindProjectDirectory(string solutionDir)
        {
            if (string.IsNullOrEmpty(solutionDir) || !Directory.Exists(solutionDir)) return null;
            try
            {
                var projects = Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories);
                if (projects.Length > 0) return Path.GetDirectoryName(projects[0]);
            }
            catch { }
            return null;
        }

        private static string Remember(string root)
        {
            try
            {
                if (!string.IsNullOrEmpty(root))
                {
                    Directory.CreateDirectory(OpenCodeEnvironment.StudioDir);
                    File.WriteAllText(LastRootPath, root);
                }
            }
            catch { }
            return root;
        }

        private static string ReadRememberedRoot()
        {
            try
            {
                return File.Exists(LastRootPath) ? File.ReadAllText(LastRootPath).Trim() : null;
            }
            catch { return null; }
        }

        private static string LastRootPath => Path.Combine(
            OpenCodeEnvironment.StudioDir, "last-project.txt");

        private string GetSolutionDirectory()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var svc = _serviceProvider;

                var dte = (svc?.GetService(typeof(DTE)) as DTE2) ??
                    Package.GetGlobalService(typeof(DTE)) as DTE2;
                if (dte?.Solution != null && !string.IsNullOrEmpty(dte.Solution.FullName))
                {
                    return Path.GetDirectoryName(dte.Solution.FullName);
                }
            }
            catch
            {
                // DTE может быть недоступен (например, в тестах)
            }

            return null;
        }

        private static string FindGitRoot(string startDir)
        {
            if (string.IsNullOrEmpty(startDir))
                return null;

            string current = startDir;

            while (!string.IsNullOrEmpty(current))
            {
                string gitPath = Path.Combine(current, ".git");
                if (Directory.Exists(gitPath) || File.Exists(gitPath))
                {
                    return current;
                }

                string parent = Path.GetDirectoryName(current);
                if (parent == current)
                    break; // Reached root
                current = parent;
            }

            return null;
        }

        /// <summary>
        /// Приводит путь к прямым слэшам и делает его полным.
        /// </summary>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            string fullPath = Path.GetFullPath(path);
            return fullPath.Replace('\\', '/');
        }
    }
}
