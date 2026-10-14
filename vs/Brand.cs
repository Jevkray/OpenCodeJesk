namespace OpenCodeStudio
{
    public static class Brand
    {
#if JESK
        public const string Name = "OpenCode Jesk";
        public const string Short = "Jesk";
        public const string PackageGuid = "2C99A37C-11F8-4F9F-9A77-D6347B8D3461";
        public const string ToolWindowGuid = "51944608-8631-481B-81DA-43CAD5EC290B";
        public const string CommandSetGuid = "0B7E5D4C-2A19-4F83-9E61-7C3A5B8D24F0";
        public const string MarketplaceUrl = "https://marketplace.visualstudio.com/items?itemName=jevkray.OpenCodeJesk";
#else
        public const string Name = "OpenCode Studio";
        public const string Short = "Studio";
        public const string PackageGuid = "7E4A9C31-2B6D-4F58-9A10-3C8E7B5D1F20";
        public const string ToolWindowGuid = "E3F4A5B6-C7D8-9012-EFAB-CDEF34567890";
        public const string CommandSetGuid = "D91F3A72-6C40-4E8B-B25D-8A47F0C63E15";
        public const string MarketplaceUrl = "https://marketplace.visualstudio.com/items?itemName=jevkray.OpenCodeStudio";
#endif

        /// <summary>Прямая ссылка на форму отзыва текущего расширения в Marketplace.</summary>
        public const string ReviewUrl = MarketplaceUrl + "&ssr=false#review-details";

        /// <summary>Новое расширение, на которое переименован Studio (для окна-уведомления).</summary>
        public const string SuccessorName = "OpenCode Jesk";
        public const string SuccessorUrl = "https://marketplace.visualstudio.com/items?itemName=jevkray.OpenCodeJesk";
        public const string ReleasesUrl = "https://github.com/Jevkray/OpenCodeStudio/releases";
        public const string SupportEndsIso = "2026-10-20";
    }
}
