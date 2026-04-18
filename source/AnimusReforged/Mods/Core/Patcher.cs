using System.Text;
using AnimusReforged.Logging;

namespace AnimusReforged.Mods.Core;

/// <summary>
/// A utility class for applying binary patches to executable files.
/// Provides methods for creating backups, finding byte sequences, and applying various patches.
/// </summary>
public abstract class Patcher
{
    // 4GB Patch Constants
    private const int PE_POINTER_OFFSET = 0x3C;
    private const ushort IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x20;

    /// <summary>
    /// Creates a backup of the specified file with a .bak extension.
    /// </summary>
    /// <param name="filePath">The path to the file to back up</param>
    /// <returns>The path to the created backup file</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist</exception>
    /// <exception cref="IOException">Thrown when there's an issue creating the backup</exception>
    public static string CreateBackup(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File to backup does not exist: {filePath}");
        }

        string? directory = Path.GetDirectoryName(filePath);
        string fileName = Path.GetFileName(filePath);
        string originalExtension = Path.GetExtension(filePath);

        string backupFileName = $"{fileName}.bak";
        string backupPath = Path.Combine(directory ?? "", backupFileName);

        Logger.Info<Patcher>($"Creating backup of '{filePath}' to '{backupPath}'");
        File.Copy(filePath, backupPath, overwrite: true);

        Logger.Info<Patcher>($"Backup created successfully at '{backupPath}'");
        return backupPath;
    }
    
    /// <summary>
    /// Applies the Large Address Aware patch to a PE (Portable Executable) file.
    /// This enables 32-bit executables to use up to 4GB of RAM instead of the default 2GB.
    /// </summary>
    /// <param name="gamePath">The path to the executable file to patch</param>
    /// <exception cref="FileNotFoundException">Thrown when the specified executable does not exist</exception>
    /// <exception cref="Exception">Thrown when the file is not a valid PE file</exception>
    public static void LargeAddressAwarePatch(string gamePath)
    {
        if (!File.Exists(gamePath))
        {
            Logger.Error<Patcher>($"Game executable does not exist: {gamePath}");
            throw new FileNotFoundException($"Game executable does not exist: {gamePath}");
        }

        Logger.Info<Patcher>($"Applying 4GB Large Address Aware patch to '{gamePath}'");

        // Create a backup before patching
        string backupPath = CreateBackup(gamePath);
        Logger.Info<Patcher>($"Created backup at '{backupPath}' before applying patch");

        using FileStream stream = new FileStream(gamePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
        using BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);

        // Locate PE Header
        stream.Seek(PE_POINTER_OFFSET, SeekOrigin.Begin);
        int peHeaderAddress = reader.ReadInt32();
        Logger.Debug<Patcher>($"PE Header Address: 0x{peHeaderAddress:X}");

        stream.Seek(peHeaderAddress, SeekOrigin.Begin);
        uint peSignature = reader.ReadUInt32(); // "PE\0\0"
        Logger.Debug<Patcher>($"PE Signature: 0x{peSignature:X}");

        if (peSignature != 0x4550) // "PE\0\0" in little-endian
        {
            throw new Exception("Not a valid PE file (Invalid PE Signature)");
        }

        Logger.Debug<Patcher>("Valid PE file detected");

        // Navigate to File Header Characteristics
        stream.Seek(4, SeekOrigin.Current); // Skip Machine (2) + NumberOfSections (2) = 4
        stream.Seek(12, SeekOrigin.Current); // TimeDateStamp (4) + PointerToSymbolTable (4) + NumberOfSymbols (4) = 12
        stream.Seek(2, SeekOrigin.Current); // SizeOfOptionalHeader = 2
        long characteristicsPosition = stream.Position;
        ushort characteristics = reader.ReadUInt16();
        Logger.Debug<Patcher>($"Current FileHeader Characteristics: 0x{characteristics:X4}");

        if ((characteristics & IMAGE_FILE_LARGE_ADDRESS_AWARE) == 0)
        {
            characteristics |= IMAGE_FILE_LARGE_ADDRESS_AWARE;
            stream.Seek(characteristicsPosition, SeekOrigin.Begin);
            writer.Write(characteristics);
            Logger.Debug<Patcher>($"Updated FileHeader Characteristics: 0x{characteristics:X4}");
            Logger.Info<Patcher>("Large Address Aware applied successfully");
        }
        else
        {
            Logger.Warning<Patcher>("File is already Large Address Aware - no patch needed");
        }
    }
}