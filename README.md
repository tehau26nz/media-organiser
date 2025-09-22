# Media Organizer

A simple yet powerful C# console application to organize your photos and videos into a clean, date-based folder structure (`YYYY/YYYY-MM`). It intelligently reads metadata to find the true "date taken" and handles duplicates gracefully.

## Features

- **Date-Based Organization:** Moves files into `YYYY/YYYY-MM` folders.
- **Smart Date Detection:** Prioritizes EXIF "Date Taken" metadata for accurate sorting. Falls back to file creation date for videos and other files without EXIF data.
- **Advanced Duplicate Handling:**
  - Detects true duplicates (same name and size) and moves them to a separate `duplicates` folder.
  - Resolves name collisions for non-duplicate files by renaming them.
- **Broad File Support:** Works with common image (`.jpg`, `.jpeg`, `.png`, `.gif`, `.heic`) and video (`.mov`, `.mp4`, `.m4v`, `.avi`, `.mpg`) formats.
- **Safe Dry-Run Mode:** Use the `--dry-run` flag to see what changes will be made without moving any files.
- **Recursive Scanning:** Processes all files in the target directory and its subdirectories.

## Requirements

- .NET 9.0 SDK or later.

## How to Use in Visual Studio Code

1.  **Clone the Repository:**
    Clone this project from GitHub to your local machine.

2.  **Open in VS Code:**
    Open the `MediaOrganizer` folder in Visual Studio Code.

3.  **Open the Terminal:**
    Use the shortcut `Ctrl+` \` (Control + backtick) or go to `Terminal > New Terminal` in the menu.

4.  **Run the Program:**
    The program requires you to provide the path to the directory containing your media files.

    To run the organizer, use the `dotnet run` command followed by the path to your media folder.

    ```bash
    # Example for Windows
    dotnet run "C:\Users\YourUser\Pictures\Unsorted"

    # Example for macOS/Linux
    dotnet run "/home/youruser/pictures/unsorted"
    ```

5.  **Using Dry-Run Mode (Recommended for first use):**
    To see what the script _would_ do without actually moving any files, add the `--dry-run` flag. This is a safe way to preview the changes.

    ```bash
    dotnet run "/path/to/your/photos" --dry-run
    ```
