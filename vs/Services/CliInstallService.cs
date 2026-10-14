using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCodeStudio.Services
{
    /// <summary>
    /// Установка OpenCode CLI через npm: освобождает shim-файлы, запускает
    /// установку и сбрасывает кэши версий/поддержки web. UI-заботы (прогресс,
    /// таймер, кнопки) остаются в окне.
    /// </summary>
    public sealed class CliInstallService
    {
        private readonly OpenCodeUpdateService _updater = new OpenCodeUpdateService();

        /// <summary>Ставит npm-пакет; возвращает true при успехе.</summary>
        public async Task<bool> InstallAsync(string packageSpec, Action<string> onLine, CancellationToken ct)
        {
            // Просим расширение остановить свои процессы/службу opencode,
            // чтобы npm смог перезаписать shim-файлы в %APPDATA%\npm.
            SpendSettings.RaiseCliInstallStarting();
            try
            {
                await Task.Delay(2000, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Отмену не считаем ошибкой: окно покажет «отменено».
                return false;
            }

            var ok = await _updater.InstallAsync(packageSpec, onLine, ct).ConfigureAwait(false);

            if (!ct.IsCancellationRequested)
            {
                CliCommand.ResetVersionCache();
                CliBinaryLocator.ResetWebSupportCache();
            }
            return ok;
        }
    }
}
