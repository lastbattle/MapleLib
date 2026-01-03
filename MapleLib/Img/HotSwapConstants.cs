namespace MapleLib.Img
{
    /// <summary>
    /// Constants and default values for hot swap configuration
    /// </summary>
    public static class HotSwapConstants
    {
        #region HaCreator Settings
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
        #endregion

        #region HaRepacker Settings
        /// <summary>
        /// Master switch for IMG file watching in HaRepacker
        /// </summary>
        public const bool EnableImgFileWatching = true;

        /// <summary>
        /// Debounce delay in milliseconds for HaRepacker file watching
        /// </summary>
        public const int DebounceMs = 500;

        /// <summary>
        /// Show notification bar when external changes are detected
        /// </summary>
        public const bool ShowNotifications = true;

        /// <summary>
        /// Automatically reload files if there are no local unsaved changes
        /// </summary>
        public const bool AutoReloadIfNoChanges = false;

        /// <summary>
        /// Automatically add new .img files to the tree when detected
        /// </summary>
        public const bool AutoAddNewFiles = false;

        /// <summary>
        /// Use MD5 hash for change detection (more accurate but slower)
        /// </summary>
        public const bool TrackContentHash = true;

        /// <summary>
        /// Maximum number of queued notifications
        /// </summary>
        public const int MaxQueuedNotifications = 50;
        #endregion
    }
}
