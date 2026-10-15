using System.Globalization;
using System.Reflection;
using System.Resources;

namespace OpenCodeJesk.Resources
{
    /// <summary>
    /// Provides localized strings from the resources assembly.
    /// </summary>
    internal static class StringsHelper
    {
        private static readonly ResourceManager _resourceManager =
            new ResourceManager("OpenCodeJesk.Resources.Strings", Assembly.GetExecutingAssembly());

        public static string GetString(string name)
        {
            return _resourceManager.GetString(name) ?? name;
        }

        public static string GetString(string name, CultureInfo culture)
        {
            return _resourceManager.GetString(name, culture) ?? name;
        }

        // Error messages
        public static string ErrorWebViewInitFailed => GetString("Error.WebViewInitFailed");
        public static string ErrorServerStartFailed => GetString("Error.ServerStartFailed");
        public static string ErrorSessionCreateFailed => GetString("Error.SessionCreateFailed");
        public static string ErrorConnectionLost => GetString("Error.ConnectionLost");
        public static string ErrorSessionListFailed => GetString("Error.SessionListFailed");
        public static string ErrorServerNotConnected => GetString("Error.ServerNotConnected");

        // UI Labels
        public static string UIRetry => GetString("UI.Retry");
        public static string UILoading => GetString("UI.Loading");
        public static string UIConnecting => GetString("UI.Connecting");
        public static string UIReconnecting => GetString("UI.Reconnecting");
    }
}
