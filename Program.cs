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
    // Define separate sets for images and videos for easier categorization.
    private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".heic", ".arw", ".cr3"
    };

    private static readonly HashSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mov", ".mp4", ".m4v", ".avi", ".mpg"
    };

    // A combined set of all supported media extensions for quick initial filtering.
    // Using a HashSet for efficient lookups.
    private static readonly HashSet<string> MediaExtensions =
        new HashSet<string>(ImageExtensions.Concat(VideoExtensions), StringComparer.OrdinalIgnoreCase);

    // A set of extensions that are likely to contain EXIF metadata.
    private static readonly HashSet<string> ExifSupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".heic", ".tiff", ".arw", ".cr3"
    };

    // A regex to check if a filename already matches our desired "YYYY-MM-DD_HH-mm-ss" format.
    // It also optionally matches copy counters like " (1)".
    private static readonly Regex StandardFileNameRegex =
        new Regex(@"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}( \(\d+\))?$", RegexOptions.Compiled);

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

        // --- Initial User Choice: Consolidate or Organize Directly ---
        // Check if there are any subdirectories in the target path (excluding our own generated ones).
        var initialSubdirectories = System.IO.Directory.GetDirectories(targetDir, "*", SearchOption.TopDirectoryOnly)
            .Where(d => !IsGeneratedFolder(Path.GetFileName(d))) // Exclude our own generated folders
            .ToArray();

        if (initialSubdirectories.Length > 0)
        {
            Console.WriteLine("\nSubfolders detected in the target directory.");
            while (true)
            {
                Console.WriteLine("Please choose an option:");
                Console.WriteLine("  1. Consolidate all media files (move from subfolders to root, then delete empty subfolders).");
                Console.WriteLine("  2. Organize media files directly (scan subfolders but do not flatten or delete them).");
                Console.Write("Enter your choice (1 or 2): ");
                string? choice = ReadUserInputWithExitCheck()?.Trim();

                if (choice == "1")
                {
                    Console.WriteLine("-> Consolidating directory structure...");
                    await FlattenDirectory(targetDir, isDryRun);
                    Console.WriteLine("-> Directory flattening complete.");
                    Console.WriteLine("-> Deleting empty subfolders...");
                    await DeleteEmptySubdirectories(targetDir, isDryRun);
                    Console.WriteLine("-> Empty subfolder deletion complete.");
                    Console.WriteLine("\n✅ Consolidation complete. Please run the program again to organize the files.");
                    return; // Exit the program after consolidation.
                }
                else if (choice == "2")
                {
                    Console.WriteLine("-> Proceeding with direct organization. Subfolders will be scanned.");
                    break;
                }
                Console.WriteLine("Invalid input. Please enter '1', '2', or 'e' to exit.");
            }
        }
        else
        {
            Console.WriteLine("No subfolders detected. Proceeding with organization.");
        }

        // --- User Prompt for Folder Structure ---
        bool separateFolders = false;
        while (true)
        {
            Console.Write("Do you want to separate photos and videos into their own folders? (Y/N): ");
            string? response = ReadUserInputWithExitCheck()?.Trim().ToUpper();
            if (response == "Y")
            {
                separateFolders = true;
                Console.WriteLine("-> Photos and videos will be placed in separate 'photos' and 'videos' folders.");
                break;
            }
            if (response == "N")
            {
                separateFolders = false;
                Console.WriteLine("-> Photos and videos will be organized together by date.");
                break;
            }
            Console.WriteLine("Invalid input. Please enter 'Y', 'N', or 'e' to exit.");
        }

        // --- User Prompt for Renaming Files ---
        bool renameFiles = false;
        while (true)
        {
            Console.Write("Do you want to rename files to 'YYYY-MM-DD_HH-mm-ss' format? (Y/N): ");
            string? response = ReadUserInputWithExitCheck()?.Trim().ToUpper();
            if (response == "Y")
            {
                renameFiles = true;
                Console.WriteLine("-> Files will be renamed based on their 'date taken'.");
                break;
            }
            if (response == "N")
            {
                renameFiles = false;
                Console.WriteLine("-> Original filenames will be preserved.");
                break;
            }
            Console.WriteLine("Invalid input. Please enter 'Y', 'N', or 'e' to exit.");
        }

        Console.WriteLine($"\nScanning for media files in: {targetDir}...");
        try
        {
            // First, collect all files to get a total count for progress reporting.
            var allFiles = GetAllMediaFiles(targetDir);
            Console.WriteLine($"Found {allFiles.Count} media files to process.");

            if (allFiles.Count > 0)
            {
                await ProcessFilesWithProgress(allFiles, targetDir, isDryRun, separateFolders, renameFiles);
                // The final completion message is now more detailed.
                Console.WriteLine($"\r100% processed. ✅ Sorting process completed for {allFiles.Count} files!");
            }
            else Console.WriteLine("✅ Organization complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ An error occurred during organization: {ex.Message}");
        }
    }

    /// <summary>
    /// Recursively finds all media files in a directory, skipping generated folders.
    /// </summary>
    /// <param name="rootDir">The top-level directory to scan.</param>
    /// <returns>A list of full file paths for all supported media files.</returns>
    private static List<string> GetAllMediaFiles(string rootDir)
    {
        var mediaFiles = new List<string>();
        var directoriesToScan = new Stack<string>();
        directoriesToScan.Push(rootDir);

        while (directoriesToScan.Count > 0)
        {
            string currentDir = directoriesToScan.Pop();

            // Add files from the current directory.
            try
            {
                foreach (string filePath in System.IO.Directory.GetFiles(currentDir))
                {
                    if (MediaExtensions.Contains(Path.GetExtension(filePath)))
                    {
                        mediaFiles.Add(filePath);
                    }
                }

                // Add subdirectories to the stack for scanning.
                foreach (string subdirectoryPath in System.IO.Directory.GetDirectories(currentDir))
                {
                    var dirInfo = new DirectoryInfo(subdirectoryPath);
                    // Avoid re-processing our own generated folders.
                    if (!IsGeneratedFolder(dirInfo.Name) && (!int.TryParse(dirInfo.Name, out _) || dirInfo.Name.Length != 4))
                    {
                        directoriesToScan.Push(subdirectoryPath);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"[Permission Error] Could not access directory {currentDir}. Skipping.");
            }
        }
        return mediaFiles;
    }

    /// <summary>
    /// Moves all files from all subdirectories into the root directory.
    /// </summary>
    /// <param name="rootDir">The root directory to move files into.</param>
    /// <param name="isDryRun">If true, no file operations will be performed.</param>
    private static async Task FlattenDirectory(string rootDir, bool isDryRun)
    {
        // Get all files from all subdirectories, excluding our generated ones.
        var filesToMove = System.IO.Directory.GetFiles(rootDir, "*.*", SearchOption.AllDirectories)
            .Where(f =>
            {
                var parentDir = Path.GetDirectoryName(f);
                // Only consider files that are NOT in the root directory itself.
                return parentDir != null && !parentDir.Equals(rootDir, StringComparison.OrdinalIgnoreCase) && !IsGeneratedFolder(new DirectoryInfo(parentDir).Name);
            })
            .ToList();

        if (filesToMove.Count == 0)
        {
            Console.WriteLine("  -> No files found in subdirectories to move.");
            return;
        }

        Console.WriteLine($"Found {filesToMove.Count} files in subdirectories to flatten.");

        int totalFiles = filesToMove.Count;
        var reportedMilestones = new HashSet<int>();

        for (int i = 0; i < totalFiles; i++)
        {
            string filePath = filesToMove[i];
            string fileName = Path.GetFileName(filePath);
            string destinationPath = Path.Combine(rootDir, fileName);

            // If a file with the same name exists, find a unique name.
            if (File.Exists(destinationPath))
            {
                destinationPath = GetUniqueFilePath(rootDir, fileName);
            }

            if (!isDryRun)
            {
                await Task.Run(() => File.Move(filePath, destinationPath));
            }

            // Progress reporting
            int currentPercentage = (int)(((i + 1.0) / totalFiles) * 100);
            int milestone = currentPercentage / 25 * 25;

            if (milestone > 0 && milestone < 100 && !reportedMilestones.Contains(milestone))
            {
                Console.Write($"\r{milestone}% flattened...");
                reportedMilestones.Add(milestone);
            }
        }
        // Clear the line for the next message
        Console.Write(new string(' ', Console.WindowWidth - 1) + "\r");
    }

    /// <summary>
    /// Deletes all empty subdirectories within the root directory, excluding generated folders.
    /// </summary>
    /// <param name="rootDir">The root directory to clean up.</param>
    /// <param name="isDryRun">If true, no directory operations will be performed.</param>
    private static async Task DeleteEmptySubdirectories(string rootDir, bool isDryRun)
    {
        // Get all subdirectories, ordered by path length descending to delete deepest first.
        // This ensures that when a child folder is deleted, its parent might become empty and also be deleted.
        var subdirectories = System.IO.Directory.GetDirectories(rootDir, "*", SearchOption.AllDirectories)
            .Where(d => !IsGeneratedFolder(Path.GetFileName(d))) // Exclude our own generated folders
            .OrderByDescending(d => d.Length) // Process deepest first
            .ToList();

        if (subdirectories.Count == 0)
        {
            Console.WriteLine("  -> No subdirectories found to check for emptiness.");
            return;
        }

        int deletedCount = 0;
        int totalDirs = subdirectories.Count;
        var reportedMilestones = new HashSet<int>();

        for (int i = 0; i < totalDirs; i++)
        {
            string dirPath = subdirectories[i];
            try
            {
                if (!System.IO.Directory.EnumerateFileSystemEntries(dirPath).Any())
                {
                    if (!isDryRun)
                    {
                        await Task.Run(() => System.IO.Directory.Delete(dirPath, false));
                    }
                    deletedCount++;
                }

                // Progress reporting
                int currentPercentage = (int)(((i + 1.0) / totalDirs) * 100);
                int milestone = currentPercentage / 25 * 25;

                if (milestone > 0 && milestone < 100 && !reportedMilestones.Contains(milestone))
                {
                    Console.Write($"\r{milestone}% checked...");
                    reportedMilestones.Add(milestone);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // We can choose to log this if needed, but for a cleaner UI, we'll skip it.
                // Console.WriteLine($"\n  -> [Permission Error] Could not delete directory {dirPath}. Check permissions.");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"  -> [I/O Error] Could not delete directory {dirPath}. Reason: {ex.Message}");
            }
        }
        Console.WriteLine($"  -> Successfully deleted {deletedCount} empty subdirectories.");
        // Clear the line for the next message
        Console.Write(new string(' ', Console.WindowWidth - 1) + "\r");
    }

    /// <summary>
    /// Processes a list of files with intelligent progress reporting.
    /// </summary>
    /// <param name="filesToProcess">The list of file paths to process.</param>
    /// <param name="rootDir">The top-level directory where new year/month folders will be created.</param>
    /// <param name="isDryRun">If true, no file operations will be performed.</param>
    /// <param name="separateFolders">If true, media will be sorted into 'photos' and 'videos' subfolders.</param>
    /// <param name="renameFiles">If true, files will be renamed to a date-based format.</param>
    private static async Task ProcessFilesWithProgress(List<string> filesToProcess, string rootDir, bool isDryRun, bool separateFolders, bool renameFiles)
    {
        int totalFiles = filesToProcess.Count;
        if (totalFiles == 0) return;

        Console.WriteLine("Processing files...");

        // Keep track of which percentage milestones we've already reported.
        var reportedMilestones = new HashSet<int>();

        for (int i = 0; i < totalFiles; i++)
        {
            string filePath = filesToProcess[i];
            // We no longer need detailed per-file logging for the progress view.
            await ProcessFile(filePath, rootDir, isDryRun, separateFolders, renameFiles, shouldLog: false);

            // Calculate current progress and the nearest quarter.
            int currentPercentage = (int)(((i + 1.0) / totalFiles) * 100);
            int milestone = currentPercentage / 25 * 25; // Rounds down to the nearest 25 (0, 25, 50, 75)

            // Report on 25%, 50%, and 75% marks.
            if (milestone > 0 && milestone < 100 && !reportedMilestones.Contains(milestone))
            {
                // Use \r to return to the beginning of the line to create a smooth progress update.
                Console.Write($"\r{milestone}% processed...");
                reportedMilestones.Add(milestone);
            }
        }
        // Clear the line for the final 100% message in Main.
        Console.Write(new string(' ', Console.WindowWidth - 1) + "\r");
    }

    /// <summary>
    /// Processes a single file: gets its creation date and moves it to the correct folder.
    /// </summary>
    /// <param name="filePath">The full path to the file.</param>
    /// <param name="rootDir">The top-level directory for organization.</param>
    /// <param name="isDryRun">If true, no file operations will be performed.</param>
    /// <param name="separateFolders">If true, media will be sorted into 'photos' and 'videos' subfolders.</param>
    /// <param name="renameFiles">If true, files will be renamed to a date-based format.</param>
    /// <param name="shouldLog">If true, console output will be generated for this file.</param>
    private static async Task ProcessFile(string filePath, string rootDir, bool isDryRun, bool separateFolders, bool renameFiles, bool shouldLog)
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

                // Determine the base directory based on user's choice and file type.
                string baseDestinationDir = rootDir;
                if (separateFolders)
                {
                    if (ImageExtensions.Contains(fileExtension))
                    {
                        baseDestinationDir = Path.Combine(rootDir, "photos");
                    }
                    else if (VideoExtensions.Contains(fileExtension))
                    {
                        baseDestinationDir = Path.Combine(rootDir, "videos");
                    }
                }

                // Construct the full path for the destination directory.
                string destinationDir = Path.Combine(baseDestinationDir, yearFolder, monthFolder);

                // --- Advanced Duplicate Handling ---
                var fileInfo = new FileInfo(filePath);
                string originalFileName = fileInfo.Name;
                long originalFileSize = fileInfo.Length;

                // Normalize the filename by removing common duplicate markers like "(1)" or "- 1".
                // This gives us the "base" name to check against if we are not renaming.
                string normalizedFileName;
                if (renameFiles)
                {
                    // Check if the file is already named according to our standard.
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
                    if (StandardFileNameRegex.IsMatch(fileNameWithoutExt))
                    {
                        normalizedFileName = originalFileName; // Keep the original name
                    }
                    else
                    {
                        normalizedFileName = $"{creationTime:yyyy-MM-dd_HH-mm-ss}{fileExtension}";
                    }
                }
                else
                {
                    normalizedFileName = originalFileName;
                }
                string primaryDestinationPath = Path.Combine(destinationDir, normalizedFileName);
                string finalPath = primaryDestinationPath;

                // Check if a file with the normalized name already exists in the target directory.
                if (File.Exists(primaryDestinationPath))
                {
                    var destFileInfo = new FileInfo(primaryDestinationPath);

                    // VIGOROUS CHECK: If file sizes match, it's a true duplicate.
                    if (originalFileSize == destFileInfo.Length)
                    {
                        if (shouldLog)
                        {
                            Console.WriteLine($"  -> True duplicate found for: {originalFileName} (same size as {normalizedFileName})");
                        }
                        string duplicateDir = Path.Combine(rootDir, "duplicates", monthFolder);
                        // We use the original filename in the duplicates folder to preserve its original name.
                        finalPath = GetUniqueFilePath(duplicateDir, originalFileName);
                    }
                    else
                    {
                        if (shouldLog)
                        {
                            Console.WriteLine($"  -> Name collision for: {originalFileName} (different size). Renaming.");
                        }
                        // If we are renaming files, the collision is likely from the same timestamp. We append a copy counter.
                        // If not renaming, we preserve the original name and append a copy counter.
                        finalPath = GetUniqueFilePath(destinationDir, normalizedFileName);
                    }
                }

                // Prepare the destination directory.
                string finalDirectory = Path.GetDirectoryName(finalPath) ?? rootDir;
                if (!isDryRun)
                {
                    System.IO.Directory.CreateDirectory(finalDirectory);
                }

                // Move the file if not in dry run mode.
                if (shouldLog)
                {
                    Console.WriteLine(isDryRun ? $"[Dry Run] Would move {filePath} -> {finalPath}" : $"Moving {filePath} -> {finalPath}");
                }
                if (!isDryRun)
                {
                    await Task.Run(() => File.Move(filePath, finalPath));
                }
            }
        }
        // --- More Specific Error Handling ---
        catch (IOException ex)
        {
            // This error often happens if the file is open in another program.
            if (shouldLog)
            {
                Console.WriteLine($"[I/O Error] Could not process file {filePath}. It may be in use. Details: {ex.Message}");
            }
        }
        catch (UnauthorizedAccessException)
        {
            // This happens if the script doesn't have permission to read/move the file.
            if (shouldLog)
            {
                Console.WriteLine($"[Permission Error] Could not access file {filePath}. Check permissions.");
            }
        }
        catch (Exception ex) // A general catch-all for any other unexpected errors.
        {
            if (shouldLog)
            {
                Console.WriteLine($"[Unexpected Error] Could not process file {filePath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Generates a unique file path in a target directory by appending a counter if a file with the same name exists.
    /// e.g., "image.jpg" -> "image (1).jpg"
    /// e.g., "2023-10-27_15-30-00.jpg" -> "2023-10-27_15-30-00 (1).jpg"
    /// </summary>
    /// <param name="targetDir">The directory where the file should be placed.</param>
    /// <param name="fileName">The original name of the file.</param>
    /// <returns>A unique file path.</returns>
    private static string GetUniqueFilePath(string targetDir, string fileName)
    {
        string destinationPath = Path.Combine(targetDir, fileName);
        int copyCount = 1;
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string fileExt = Path.GetExtension(fileName);

        while (File.Exists(destinationPath))
        {
            destinationPath = Path.Combine(targetDir, $"{fileNameWithoutExt} ({copyCount++}){fileExt}");
        }
        return destinationPath;
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
                        // This logging is now disabled during the main progress view,
                        // but we keep the code here in case we want to re-enable it for debugging.
                        // Console.WriteLine($"  -> Found EXIF Date Taken: {dateTaken}");
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

        // Return the earlier of the two dates. This is a more reliable fallback.
        return creationTime < lastWriteTime ? creationTime : lastWriteTime;
    }

    /// <summary>
    /// Checks if a directory name is one of the folders generated by this application.
    /// </summary>
    /// <param name="dirName">The name of the directory.</param>
    /// <returns>True if it's a generated folder name.</returns>
    private static bool IsGeneratedFolder(string dirName)
    {
        return dirName.Equals("photos", StringComparison.OrdinalIgnoreCase) ||
               dirName.Equals("videos", StringComparison.OrdinalIgnoreCase) ||
               dirName.Equals("duplicates", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads user input from the console and handles a global exit command.
    /// </summary>
    /// <returns>The user's input, or null if input is null.</returns>
    private static string? ReadUserInputWithExitCheck()
    {
        string? input = Console.ReadLine();
        if (input != null && (input.Trim().Equals("e", StringComparison.OrdinalIgnoreCase) || input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("\n👋 Exiting program as requested.");
            Environment.Exit(0); // Gracefully terminate the application.
        }
        return input;
    }
}
