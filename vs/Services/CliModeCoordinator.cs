using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Часть окна, нужная координатору CLI-режима (без прямых ссылок на окно).
    /// </summary>
    public interface ICliModeHost
    {
        /// <summary>Панель, в которую кладётся терминал (корневой Grid окна).</summary>
        Panel TerminalPanel { get; }
        /// <summary>Текущий адрес документа WebView2.</summary>
        string CurrentUrl { get; }
        /// <summary>Адрес последней навигации (может быть ещё не завершён).</summary>
        string PendingUrl { get; }
        SpendSettings GetSpendSettings();
    }

    /// <summary>
    /// CLI-режим панели: собственный терминал с OpenCode, встроенный в ту же
    /// панель вместо веб-клиента. Управляет созданием/пересозданием страницы.
    /// </summary>
    public sealed class CliModeCoordinator : IDisposable
    {
        private readonly ICliModeHost _host;
        private CliTerminalControl _terminal;
        private string _settingsKey;
        private bool _isDisposed;

        public CliModeCoordinator(ICliModeHost host)
        {
            _host = host;
        }

        /// <summary>Отпечаток настроек терминала: меняется — страницу пересоздаём.</summary>
        private string TerminalSettingsKey()
        {
            var s = _host.GetSpendSettings();
            return string.Join("|",
                s?.TerminalFontFamily ?? "Consolas",
                (s?.TerminalFontSize ?? 13).ToString(),
                (s?.TerminalCursorBlink ?? true).ToString(),
                (s?.TerminalScrollback ?? 5000).ToString(),
                s?.CliArguments ?? "");
        }

        /// <summary>Убирает терминал из панели (возврат к веб-панели).</summary>
        public void Hide()
        {
            if (_terminal == null) return;
            _host.TerminalPanel.Children.Remove(_terminal);
            _terminal.Dispose();
            _terminal = null;
        }

        /// <summary>
        /// Показывает терминал для текущей сессии. Возвращает актуальные
        /// (session, directory), которые нужно сохранить в окне.
        /// </summary>
        public async Task<Tuple<string, string>> ShowAsync(
            ServerController controller, string directory, string session, string projectRoot)
        {
            if (_isDisposed) return Tuple.Create(session, directory);
            if (controller?.State != ConnectionState.Connected || string.IsNullOrEmpty(projectRoot))
                return Tuple.Create(session, directory);

            // Настройки терминала (шрифт, размер, курсор, прокрутка, аргументы)
            // применяются при пересоздании страницы — пересоздаём при их изменении.
            var terminalKey = TerminalSettingsKey();
            if (_terminal != null && _settingsKey != terminalKey)
            {
                _host.TerminalPanel.Children.Remove(_terminal);
                _terminal.Dispose();
                _terminal = null;
            }
            _settingsKey = terminalKey;

            // Создаём терминал только когда панель уже видима: WebView2, созданный
            // внутри скрытого контейнера, не отрисовывается и не выполняет скрипты.
            if (_terminal == null)
            {
                _terminal = new CliTerminalControl();
                _host.TerminalPanel.Children.Add(_terminal);
            }
            _terminal.Visibility = Visibility.Visible;

            var fromUrl = SessionUrlParser.Parse(
                _host.PendingUrl ?? _host.CurrentUrl,
                controller.ServerService?.ServerInfo?.BaseUrl);
            if (fromUrl != null) { directory = fromUrl.Item1 ?? directory; session = fromUrl.Item2; }
            if (string.IsNullOrEmpty(directory)) directory = Path.GetFullPath(projectRoot);

            // Сессия могла остаться от другой версии opencode (например, v2 -> v1):
            // на текущем сервере её нет, и CLI падает с "Session not found".
            // Проверяем существование; если её нет — создаём новую ниже.
            if (!string.IsNullOrEmpty(session))
            {
                try
                {
                    var existing = await controller.SessionService.GetSessionAsync(session, directory);
                    if (existing == null) session = null;
                }
                catch (Exception ex)
                {
                    Log.Info("CLI session lookup: " + ex.Message);
                    session = null;
                }
            }

            // Общий сервер: чат создаётся один раз и передаётся в терминал, чтобы
            // в обоих режимах была одна и та же активная сессия.
            if (string.IsNullOrEmpty(session))
            {
                try
                {
                    var created = await controller.SessionService.CreateSessionAsync(directory, Brand.Name);
                    session = created?.Id;
                    if (!string.IsNullOrEmpty(created?.Directory)) directory = created.Directory;
                }
                catch (Exception ex) { Log.Info("CLI session create: " + ex.Message); }
            }

            await _terminal.ActivateAsync(controller.ServerService, directory, session);
            return Tuple.Create(session, directory);
        }

        /// <summary>Останавливает процесс терминала, не удаляя страницу.</summary>
        public void Stop() => _terminal?.Stop();

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _terminal?.Dispose();
            _terminal = null;
        }
    }
}
