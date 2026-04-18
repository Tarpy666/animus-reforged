using AnimusReforged.Models.Altair;

namespace AnimusReforged.Utilities
{
    /// <summary>
    /// Centralized class for managing all file paths in the application.
    /// </summary>
    public static class FilePaths
    {
        // Game executable paths
        public static readonly IReadOnlyDictionary<ExecutableType, string> AltairExecutables = new Dictionary<ExecutableType, string>
        {
            [ExecutableType.DX9] = AbsolutePath.GetFullPath("AssassinsCreed_Dx9.exe"),
            [ExecutableType.DX10] = AbsolutePath.GetFullPath("AssassinsCreed_Dx10.exe"),
            [ExecutableType.Game] = AbsolutePath.GetFullPath("AssassinsCreed_Game.exe")
        };

        // Directory paths
        public static readonly string DownloadsDirectory = AbsolutePath.GetFullPath("downloads");
        public static readonly string ScriptsDirectory = AbsolutePath.GetFullPath("scripts");
        public static readonly string ModsDirectory = AbsolutePath.GetFullPath("mods");
        public static readonly string ConfigDirectory = AbsolutePath.GetFullPath("config");
        public static readonly string LogsDirectory = AbsolutePath.GetFullPath("logs");

        // Specific file paths
        public static readonly string LogFile = Path.Combine(LogsDirectory, "altair.log");
        public static readonly string SettingsFile = Path.Combine(ConfigDirectory, "config.json");
        public static readonly string AltairFixSettingsFile = Path.Combine(ScriptsDirectory, "AltairFix.ini");
        public static readonly string EaglePatchSettingsFile = Path.Combine(ScriptsDirectory, "EaglePatchAC1.ini");

        // UMod specific paths
        public static readonly string UModLocation = AbsolutePath.GetFullPath("uMod");
        public static readonly string UModExecutable = Path.Combine(UModLocation, "uMod.exe");
        public static readonly string UModAppdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "uMod");
        public static readonly string UModConfig = Path.Combine(UModAppdata, "uMod_DX9.txt");
        public static readonly string UModTemplates = Path.Combine(UModLocation, "templates");
        public static readonly string UModStatusFile = Path.Combine(UModLocation, "Status.txt");
        public static readonly string UModSaveFiles = Path.Combine(UModLocation, "uMod_SaveFiles.txt");

        // Mod file paths
        public static readonly string OverhaulDirectory = Path.Combine(ModsDirectory, "Overhaul");
        public static readonly string OverhaulTpfFile = Path.Combine(OverhaulDirectory, "Overhaul.tpf");
        public static readonly string AsiLoader = AbsolutePath.GetFullPath("dinput8.dll");
    }
}