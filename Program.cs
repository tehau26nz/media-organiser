using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
// Import the necessary libraries for reading EXIF metadata.
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

// The main class for our application.
partial class Program
{
    // --- Configuration ---
    // A set of file extensions (case-insensitive) that the script should process.
    // Using a HashSet for efficient lookups.
    private static readonly HashSet<string> MediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".heic", // Images
        ".mov", ".mp4", ".m4v", ".avi", ".mpg"     // Videos
    };

    // A set of extensions that are likely to contain EXIF metadata.
    private static readonly HashSet<string> ExifSupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".heic", ".tiff"
    };

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    /// <param name="args">Command-line arguments. We expect the target directory as the first argument.</param>
    static async Task Main(string[] args)
    {
        // --- Argument Parsing ---
        // Check for the target directory and the optional --dry-run flag.
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("❌ Please provide a target directory to scan.");
            Console.WriteLine("Usage: dotnet run \"/path/to/your/photos\" [--dry-run]");
            return;
        }

        string targetDir = args[0];
        // A "dry run" will only report what it would do, without moving any files.
        bool isDryRun = args.Contains("--dry-run");

        // Ensure the provided directory exists.
        if (!System.IO.Directory.Exists(targetDir))
        {
            Console.WriteLine($"❌ Error: The directory '{targetDir}' does not exist.");
            return;
        }

        if (isDryRun)
        {
            Console.WriteLine("--- DRY RUN MODE ENABLED: No files will be moved. ---");
        }

        Console.WriteLine($"Starting to organize media files in: {targetDir}");
        try
        {
            // The 'root' directory is the same as the target directory.
            // This is where the new Year/Month folders will be created.
            await ProcessDirectory(targetDir, targetDir, isDryRun);
            Console.WriteLine("✅ Organization complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ An error occurred during organization: {ex.Message}");
        }
    }

    /// <summary>
    /// Recursively processes a directory, moving files and stepping into subdirectories.
    /// </summary>
    /// <param name="currentDir">The directory currently being processed.</param>
    /// <param name="rootDir">The top-level directory where new year/month folders will be created.</param>
    /// <param name="isDryRun">If true, no file operations will be performed.</param>
    private static async Task ProcessDirectory(string currentDir, string rootDir, bool isDryRun)
    {
        // Process all files in the current directory.
        foreach (string filePath in System.IO.Directory.GetFiles(currentDir))
        {
            await ProcessFile(filePath, rootDir, isDryRun);
        }

        // Process all subdirectories in the current directory.
        foreach (string subdirectoryPath in System.IO.Directory.GetDirectories(currentDir))
        {
            var dirInfo = new DirectoryInfo(subdirectoryPath);
            // Avoid getting into an infinite loop by not re-processing the folders we create.
            // This checks if the folder name is a 4-digit number (like a year).
            if (!int.TryParse(dirInfo.Name, out _) || dirInfo.Name.Length != 4)
            {
                await ProcessDirectory(subdirectoryPath, rootDir, isDryRun);
            }
        }
    }

    /// <summary>
    /// Processes a single file: gets its creation date and moves it to the correct folder.
    /// </summary>
    /// <param name="filePath">The full path to the file.</param>
    /// <param name="rootDir">The top-level directory for organization.</param>
    /// <param name="isDryRun">If true, no file operations will be performed.</param>
    private static async Task ProcessFile(string filePath, string rootDir, bool isDryRun)
    {
        try
        {
            string fileExtension = Path.GetExtension(filePath);

            // Check if the file's extension is in our list of media types.
            if (MediaExtensions.Contains(fileExtension))
            {
                // --- Get File Date (Prioritize EXIF) ---
                DateTime creationTime = GetMediaDate(filePath);

                // Format the year and month. The "D2" format specifier ensures a two-digit month.
                string year = creationTime.Year.ToString();
                string month = creationTime.Month.ToString("D2");

                // Create the destination folder names.
                string yearFolder = year;
                string monthFolder = $"{year}-{month}";

                // Construct the full path for the destination directory.
                // e.g., /path/to/root/2023/2023-09
                string destinationDir = Path.Combine(rootDir, yearFolder, monthFolder);

                // --- Advanced Duplicate Handling ---
                var fileInfo = new FileInfo(filePath);
                string originalFileName = fileInfo.Name;
                long originalFileSize = fileInfo.Length;

                // Normalize the filename by removing common duplicate markers like "(1)" or "- 1".
                // This gives us the "base" name to check against.
                string baseName = Regex.Replace(Path.GetFileNameWithoutExtension(originalFileName), @"\s*[\(-]\s*\d+\s*\)?$", "").Trim();
                string normalizedFileName = $"{baseName}{fileExtension}";

                string primaryDestinationPath = Path.Combine(destinationDir, normalizedFileName);
                string finalPath = primaryDestinationPath;

                // Check if a file with the normalized name already exists in the target directory.
                if (File.Exists(primaryDestinationPath))
                {
                    var destFileInfo = new FileInfo(primaryDestinationPath);

                    // VIGOROUS CHECK: If file sizes match, it's a true duplicate.
                    if (originalFileSize == destFileInfo.Length)
                    {
                        Console.WriteLine($"  -> True duplicate found for: {originalFileName} (same size as {normalizedFileName})");
                        string duplicateDir = Path.Combine(rootDir, "duplicates", monthFolder);
                        // We use the original filename in the duplicates folder to preserve its name.
                        finalPath = GetUniqueFilePath(duplicateDir, originalFileName);
                    }
                    else
                    {
                        // Not a true duplicate (different size), but a name collision.
                        // Move the current file to the primary folder but with a unique name.
                        Console.WriteLine($"  -> Name collision for: {originalFileName} (different size). Renaming.");
                        finalPath = GetUniqueFilePath(destinationDir, originalFileName);
                    }
                }

                // Prepare the destination directory.
                string finalDirectory = Path.GetDirectoryName(finalPath) ?? rootDir;
                if (!isDryRun)
                {
                    System.IO.Directory.CreateDirectory(finalDirectory);
                }

                // Move the file if not in dry run mode.
                Console.WriteLine(isDryRun ? $"[Dry Run] Would move {filePath} -> {finalPath}" : $"Moving {filePath} -> {finalPath}");
                if (!isDryRun)
                {
                    File.Move(filePath, finalPath);
                }
            }
        }
        // --- More Specific Error Handling ---
        catch (IOException ex)
        {
            // This error often happens if the file is open in another program.
            Console.WriteLine($"[I/O Error] Could not process file {filePath}. It may be in use. Details: {ex.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            // This happens if the script doesn't have permission to read/move the file.
            Console.WriteLine($"[Permission Error] Could not access file {filePath}. Check permissions.");
        }
        catch (Exception ex) // A general catch-all for any other unexpected errors.
        {
            Console.WriteLine($"[Unexpected Error] Could not process file {filePath}: {ex.Message}");
        }
        // The 'await Task.CompletedTask' is just to make the method async as good practice,
        // even though our file operations here are synchronous.
        await Task.CompletedTask;
    }

    /// <summary>
    /// Generates a unique file path in a target directory by appending a counter if a file with the same name exists.
    /// e.g., "image.jpg" -> "image_copy_1.jpg"
    /// </summary>
    /// <param name="targetDir">The directory where the file should be placed.</param>
    /// <param name="fileName">The original name of the file.</param>
    /// <returns>A unique file path.</returns>
    private static string GetUniqueFilePath(string targetDir, string fileName)
    {
        string newFilePath = Path.Combine(targetDir, fileName);
        if (File.Exists(newFilePath))
        {
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string fileExt = Path.GetExtension(fileName);
            int copyCount = 1;
            do
            {
                newFilePath = Path.Combine(targetDir, $"{fileNameWithoutExt}_copy_{copyCount++}{fileExt}");
            } while (File.Exists(newFilePath));
        }
        return newFilePath;
    }

    /// <summary>
    /// Gets the date of a media file, prioritizing EXIF "Date Taken" over file system creation time.
    /// </summary>
    /// <param name="filePath">The path to the media file.</param>
    /// <returns>The best-effort DateTime for when the media was created.</returns>
    private static DateTime GetMediaDate(string filePath)
    {
        // First, check if the file type supports EXIF data.
        if (ExifSupportedExtensions.Contains(Path.GetExtension(filePath)))
        {
            try
            {
                // Use the MetadataExtractor library to read all metadata from the image file.
                var directories = ImageMetadataReader.ReadMetadata(filePath);

                // Find the specific directory containing the EXIF "SubIFD" data, which holds the original date/time.
                var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

                // The "Date/Time Original" tag (ID 36867) is the most reliable "Date Taken" field.
                if (subIfdDirectory != null && subIfdDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dateTaken))
                {
                    // A sanity check for dates that are clearly wrong (e.g., '0001-01-01')
                    if (dateTaken.Year > 1)
                    {
                        Console.WriteLine($"  -> Found EXIF Date Taken: {dateTaken}");
                        return dateTaken; // Return the EXIF date if found.
                    }
                }
            }
            catch (Exception ex)
            {
                // If the library fails to read the file (e.g., corrupted file), log it and fall back.
                Console.WriteLine($"  -> Warning: Could not read EXIF data from {Path.GetFileName(filePath)}. Reason: {ex.Message}");
            }
        }

        // --- Fallback for non-EXIF files (like videos) or if EXIF fails ---
        // The "Creation Time" can be misleading (e.g., when a file is copied).
        // The "Last Write Time" is often the original creation date for videos.
        // By taking the *earlier* of the two, we get a more reliable date.
        var creationTime = File.GetCreationTime(filePath);
        var lastWriteTime = File.GetLastWriteTime(filePath);

        // Return the earlier of the two dates.
        return creationTime < lastWriteTime ? creationTime : lastWriteTime;
    }
}
