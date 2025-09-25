# Media Organizer

A simple yet powerful C# console application to organize your photos and videos into a clean, date-based folder structure (`YYYY/YYYY-MM`). It intelligently reads metadata to find the true "date taken" and handles duplicates gracefully.

## Features

- **Directory Flattening:** Optionally move all files from subdirectories into the main folder before organizing.
- **Date-Based Organization:** Moves files into `YYYY/YYYY-MM` folders.
- **Flexible Folder Structure:** Interactively choose to keep photos and videos together or separate them into `photos` and `videos` subfolders.
- **Optional File Renaming:** Choose to rename your files to a standardized `YYYY-MM-DD_HH-mm-ss` format for clean, sortable filenames, or keep the original names.
- **Smart Date Detection:** Prioritizes EXIF "Date Taken" metadata for accurate sorting. Falls back to file creation date for videos and other files without EXIF data.
- **Advanced Duplicate Handling:**
  - Detects true duplicates (same name and size) and moves them to a separate `duplicates` folder.
  - Resolves name collisions for non-duplicate files by renaming them.
- **Broad File Support:** Works with common image (`.jpg`, `.jpeg`, `.png`, `.gif`, `.heic`, `.arw`, `.cr3`) and video (`.mov`, `.mp4`, `.m4v`, `.avi`, `.mpg`) formats.
- **Safe Dry-Run Mode:** Use the `--dry-run` flag to see what changes will be made without moving any files.
- **Recursive Scanning:** Processes all files in the target directory and its subdirectories.

## Requirements

- .NET 9.0 SDK or later.

## How to Use in Visual Studio Code

1. **Clone the Repository:**
   Clone this project from GitHub to your local machine.

2. **Open in VS Code:**
   Open the `MediaOrganizer` folder in Visual Studio Code.

3. **Open the Terminal:**
   Use the shortcut `Ctrl+` \` (Control + backtick) or go to `Terminal > New Terminal` in the menu.

4. **Run the Program:**
   The program requires you to provide the path to the directory containing your media files.

   To run the organizer, use the `dotnet run` command followed by the path to your media folder.

   ```bash
   # Example for Windows
   dotnet run "C:\Users\YourUser\Pictures\Unsorted"

   # Example for macOS/Linux
   dotnet run "/home/youruser/pictures/unsorted"
   ```

   You will then be prompted with a few questions to customize the organization process:

   - **Initial Choice (if subfolders are found):**
     - **1. Consolidate all media files:** Moves all files from subfolders into the main directory, then deletes the now-empty subfolders.
     - **2. Organize media files directly:** Scans subfolders for media but does not flatten the directory structure.
   - **Separate photos and videos? (Y/N):** Choose if you want media sorted into `photos` and `videos` subdirectories.
   - **Rename files? (Y/N):** Choose if you want files renamed to the `YYYY-MM-DD_HH-mm-ss` format.

5. **Using Dry-Run Mode (Recommended for first use):**
   To see what the script _would_ do without actually moving any files, add the `--dry-run` flag. This is a safe way to preview the changes.

   ```bash
   dotnet run "/path/to/your/photos" --dry-run
   ```
