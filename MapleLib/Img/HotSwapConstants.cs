namespace MapleLib.Img
{
    /// <summary>
    /// Constants and default values for hot swap configuration
    /// </summary>
    public static class HotSwapConstants
    {
        /// <summary>
        /// Default: Hot swap is enabled
        /// </summary>
        public const bool DefaultEnabled = true;

        /// <summary>
        /// Default debounce delay in milliseconds before processing file changes
        /// </summary>
        public const int DefaultDebounceMs = 500;

        /// <summary>
        /// Default: Watch version directories for new/deleted versions
        /// </summary>
        public const bool DefaultWatchVersions = true;

        /// <summary>
        /// Default: Watch category directories for .img file changes
        /// </summary>
        public const bool DefaultWatchCategories = true;

        /// <summary>
        /// Default: Automatically invalidate cache when files change
        /// </summary>
        public const bool DefaultAutoInvalidateCache = true;

        /// <summary>
        /// Default: Auto-refresh UI panels when assets change
        /// </summary>
        public const bool DefaultAutoRefreshDisplayedAssets = true;

        /// <summary>
        /// Default: Show confirmation dialog before refreshing placed items
        /// </summary>
        public const bool DefaultConfirmRefreshPlacedItems = true;

        /// <summary>
        /// Default: Do not pause MapSimulator when assets change
        /// </summary>
        public const bool DefaultPauseSimulatorOnAssetChange = false;

        /// <summary>
        /// Default behavior when a deleted asset is in use
        /// </summary>
        public const DeletedAssetBehavior DefaultDeletedAssetBehavior = DeletedAssetBehavior.ShowPlaceholder;
    }
}
