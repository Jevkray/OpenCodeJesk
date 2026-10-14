using System;
using System.Threading.Tasks;

namespace OpenCodeStudio.Services
{
    /// <summary>
    /// Снимок установленного окружения OpenCode: пути CLI v1/v2, Desktop и
    /// результат проверки поддержки web. Данные без UI — рендер остаётся в окне.
    /// </summary>
    public sealed class CliVersionAvailability
    {
        /// <summary>Путь к бинарю v1 или null.</summary>
        public string V1Path { get; set; }

        /// <summary>Путь к бинарю v2 или null.</summary>
        public string V2Path { get; set; }

        /// <summary>Путь к приложению OpenCode Desktop или null.</summary>
        public string DesktopAppPath { get; set; }

        /// <summary>Бинарь, у которого проверяли поддержку web (v1 ?? v2).</summary>
        public string WebBinaryPath { get; set; }

        /// <summary>Поддерживает ли найденный CLI подкоманду web.</summary>
        public bool WebSupported { get; set; }

        /// <summary>Установлена ли ветка v1.</summary>
        public bool V1Available => !string.IsNullOrEmpty(V1Path);

        /// <summary>Установлена ли ветка v2.</summary>
        public bool V2Available => !string.IsNullOrEmpty(V2Path);
    }

    /// <summary>
    /// Детекция версий и сбор данных для статуса установки. Один проход
    /// определяет v1/v2/desktop/web без дублирования поиска. UI-рендер строк
    /// статуса остаётся в вызове; ошибка отдельной проверки логируется и не
    /// валит остальные.
    /// </summary>
    public sealed class CliVersionStatusService
    {
        /// <summary>
        /// Собирает снимок окружения. Каждая проверка изолирована: при
        /// исключении пишем в лог и оставляем соответствующее поле пустым.
        /// </summary>
        public async Task<CliVersionAvailability> DetectAsync()
        {
            var result = new CliVersionAvailability();

            try
            {
                result.V1Path = await CliBinaryLocator.FindV1PathAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error("Version detection failed (v1)", ex);
            }

            try
            {
                result.V2Path = await CliBinaryLocator.FindV2PathAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error("Version detection failed (v2)", ex);
            }

            try
            {
                result.DesktopAppPath = CliBinaryLocator.FindDesktopAppPath();
            }
            catch (Exception ex)
            {
                Log.Error("Version detection failed (desktop)", ex);
            }

            result.WebBinaryPath = result.V1Path ?? result.V2Path;
            try
            {
                result.WebSupported = await CliBinaryLocator.SupportsWebAsync(result.WebBinaryPath)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error("Version detection failed (web)", ex);
            }

            return result;
        }
    }
}
