using System;
using System.Collections.Generic;
using System.IO;

namespace OpenCodeStudio.Services
{
    /// <summary>Тип смыслового блока changelog.</summary>
    public enum ChangelogBlockKind
    {
        Title,
        Version,
        Category,
        Bullet,
        Paragraph
    }

    /// <summary>Один разобранный блок changelog.</summary>
    public sealed class ChangelogBlock
    {
        public ChangelogBlockKind Kind;
        public string Text;
    }

    /// <summary>
    /// Читает встроенный ресурс "Changelog.md" и разбирает его Markdown на
    /// блоки для рендера во вкладке Updates.
    /// </summary>
    public static class ChangelogService
    {
        /// <summary>Возвращает текст встроенного CHANGELOG.md или "" при ошибке.</summary>
        public static string ReadAll()
        {
            try
            {
                using (var stream = typeof(ChangelogService).Assembly.GetManifestResourceStream("Changelog.md"))
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
            catch { return ""; }
        }

        /// <summary>
        /// Построчный разбор: строку "Changelog" и шапку до первой версии
        /// пропускаем; "## " — версия, "### " — категория, "- " — пункт,
        /// остальные непустые строки — абзац. Inline **жирный** сохраняется.
        /// </summary>
        public static List<ChangelogBlock> Parse(string markdown)
        {
            var blocks = new List<ChangelogBlock>();
            if (string.IsNullOrEmpty(markdown)) return blocks;

            var started = false;
            foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;

                // Сноски-ссылки вида "[1.2.0]: https://…" не показываем.
                if (line.StartsWith("[") && line.Contains("]: ")) continue;

                if (line.StartsWith("### "))
                {
                    if (!started) continue; // шапка до первой версии
                    blocks.Add(new ChangelogBlock { Kind = ChangelogBlockKind.Category, Text = line.Substring(4).Trim() });
                }
                else if (line.StartsWith("## "))
                {
                    started = true;
                    blocks.Add(new ChangelogBlock { Kind = ChangelogBlockKind.Version, Text = line.Substring(3).Trim() });
                }
                else if (line.StartsWith("- "))
                {
                    if (!started) continue;
                    blocks.Add(new ChangelogBlock { Kind = ChangelogBlockKind.Bullet, Text = line.Substring(2).Trim() });
                }
                else if (line.StartsWith("# "))
                {
                    // Заголовок "Changelog" — часть шапки, пропускаем.
                    if (started) continue;
                    var text = line.Substring(2).Trim();
                    if (!string.Equals(text, "Changelog", StringComparison.Ordinal))
                        blocks.Add(new ChangelogBlock { Kind = ChangelogBlockKind.Title, Text = text });
                }
                else if (started)
                {
                    blocks.Add(new ChangelogBlock { Kind = ChangelogBlockKind.Paragraph, Text = line });
                }
            }
            return blocks;
        }
    }
}
