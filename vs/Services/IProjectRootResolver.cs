namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Определяет корневой каталог проекта по решению Visual Studio или Git-репозиторию.
    /// </summary>
    public interface IProjectRootResolver
    {
        /// <summary>
        /// Возвращает корневой путь проекта с нормализованными прямыми слэшами.
        /// Корень Git имеет приоритет над корнем решения.
        /// </summary>
        string ResolveProjectRoot();
    }
}
