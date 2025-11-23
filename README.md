# MapleLib (.NET core 10.0)

MapleStory File and Protocol Toolkit: A library for parsing, modifying, and creating MapleStory client files and server communication protocols.
A general-purpose MapleStory library, adapted for [Harepacker-resurrected](https://github.com/lastbattle/Harepacker-resurrected).

https://github.com/hadeutscher/MapleLib

 - MapleLib2 by haha01haha01; 
 - based on MapleLib by Snow; 
 - based on WzLib by JonyLeeson; 
 - based on information from Fiel\Koolk.

## Overview

MapleLib is a .NET library designed to work with MapleStory's file formats and network protocols. It provides tools for reading, editing, and writing game assets stored in proprietary formats, as well as handling client-server packet structures. This library powers projects like HaRepacker-resurrected [HaCreator and HaRepacker] for GUI-based editing.

## Features

- Support for parsing and modifying .wz archive files, which contain game data like maps, items, and UI elements.
- Handling of compressed data streams.
- Encrypting and decrypting (.ms)
- Packet reading and writing for MapleStory's network communication.
- Compatibility with various MapleStory versions through version-specific handling. (beta v1 -> v260 ++)
- Integration with external tools for advanced editing, such as map creation and asset extraction.
- Support for loading hotfix files like Data.wz and raw .img files.
- Handling of list files (List.wz) for pre-Big Bang versions.

## Installation

1. Clone the repository:
   ```
   git clone https://github.com/lastbattle/MapleLib.git
   ```
2. Open the solution in Visual Studio or use the .NET CLI.
3. Restore NuGet packages and build the project targeting .NET 8.0 or compatible framework.

No NuGet package is currently available; build from source for use in your projects.

## Usage

MapleLib provides classes under namespaces like `MapleLib.WzLib` for file handling. Below are examples for common operations.

### WzFileManager

For managing multiple .wz files, especially in a MapleStory directory context, use `WzFileManager`.

```csharp
using MapleLib.WzLib;

// Initialize WzFileManager with base directory
string baseDirectory = @"C:\Nexon\MapleStory"; // Example path
bool isStandalone = false; // Set to true if loading single file without directory structure
WzFileManager manager = new WzFileManager(baseDirectory, isStandalone);
manager.BuildWzFileList(); // Scans and builds list of available .wz files
```

### Opening .wz Files

.wz files are the primary archives in MapleStory. You can load them directly or via `WzFileManager`.

Using direct `WzFile`:

```csharp
using MapleLib.WzLib;
using System;

string filePath = "Base.wz"; // Path to your .wz file
WzMapleVersion version = WzMapleVersion.BMS; // Adjust based on region/version, e.g., BMS, GMS, EMS, CUSTOM

WzFile wzFile = new WzFile(filePath, version);
try
{
    wzFile.ParseWzFile();
    // Access the root directory
    WzDirectory root = wzFile.WzDirectory;
    // Example: Get a child directory and image
    WzDirectory folder = root.GetDirectory("SomeFolder");
    WzImage image = folder.GetImage("SomeImage.img");
}
catch (Exception ex)
{
    Console.WriteLine($"Error loading .wz file: {ex.Message}");
}
finally
{
    wzFile.Dispose(); // Always dispose to free resources
}
```

Using `WzFileManager`:

```csharp
// Assuming manager is initialized as above
string filePath = "Map.wz";
WzMapleVersion version = WzMapleVersion.BMS;
WzFile f = manager.LoadWzFile(filePath, version);
if (f != null)
{
    // Use f as above
    // Optionally load related files, e.g., for Map.wz, load Map001.wz etc. if applicable
    string[] relatedFiles = Directory.GetFiles(Path.GetDirectoryName(filePath), "Map*.wz");
    foreach (string related in relatedFiles)
    {
        if (related != filePath)
            manager.LoadWzFile(related, version);
    }
}
```

For versions requiring key brute-forcing (WzMapleVersion.GENERATE), implement a key finder or use tools like HaRepacker's brute-force form.

### Opening .ms Files

.ms file is an encrypted layer wrapping .wz file, implemented after version 220+ of MapleStory. Load them using `WzMsFile` and convert to `WzFile`.

```csharp
using MapleLib.WzLib.MSFile;
using System.IO;

string filePath = "Map.ms"; // Path to your .ms file
using (FileStream fileStream = File.OpenRead(filePath))
{
    MemoryStream memoryStream = new MemoryStream();
    fileStream.CopyTo(memoryStream);
    memoryStream.Position = 0;

    string msFileName = Path.GetFileName(filePath);
    WzMsFile msFile = new WzMsFile(memoryStream, msFileName, filePath, true);
    msFile.ReadEntries();

    WzFile wzFile = msFile.LoadAsWzFile();

    // Now use wzFile as a standard WzFile
    // Optionally load into manager
    // WzFileManager.LoadWzFile(msFileName, wzFile);
}
```

### Opening Other Files

#### Raw .img Files or Data.wz hotfixes file

```csharp
string filePath = "Patch.img"; // or Data.wz hotfix
WzMapleVersion version = WzMapleVersion.BMS;

WzImage img = manager.LoadDataWzHotfixFile(filePath, version);
if (img != null)
{
    // Parse and use the image
    img.ParseImage();
}
```

#### List.wz Files (Pre-Big Bang)

If `WzTool.IsListFile(filePath)`, open with a specialized editor (e.g., ListEditor in HaRepacker).

## Additional Examples

### Traversing WzDirectories

After loading a WzFile, you can iterate through its directories and images to access or cache content.

```csharp
// Assuming wzFile is loaded, e.g., from NPC.wz
// npcWzDirs could be a list of WzDirectory instances, such as from multiple loaded files or subdirectories
List<WzDirectory> npcWzDirs = new List<WzDirectory> { wzFile.WzDirectory }; // Example: single root directory

foreach (WzDirectory npcWzDir in npcWzDirs)
{
    foreach (WzImage npcImage in npcWzDir.WzImages)
    {
        string npcId = npcImage.Name.Replace(".img", "");
    }
}
```

For nested directories, recursively traverse `npcWzDir.WzDirectories`.

### Working with WzImage

Once you have a WzImage (e.g., from a WzDirectory), parse it to access its properties.

```csharp
// Assuming image is obtained, e.g., WzImage image = ...;
image.ParseImage(); // Parse the image if not already parsed to load properties

// Access properties by name (top-level)
WzImageProperty prop = image["propertyName"]; // Using indexer, if available, or via list

// Or iterate through all properties
foreach (WzImageProperty subProp in image.WzProperties)
{
    string propName = subProp.Name;
    // Check type and handle accordingly, e.g., if (subProp is WzStringProperty) { string value = ((WzStringProperty)subProp).Value; }
}

// Access nested properties using path
WzImageProperty nestedProp = image.GetFromPath("folder/subfolder/property"); // Using GetFromPath method

// Example: Extracting a canvas property for minimap
WzCanvasProperty minimap = (WzCanvasProperty)image.GetFromPath("miniMap/canvas");
if (minimap != null)
{
    Bitmap minimapBitmap = minimap.GetLinkedWzCanvasBitmap();
    // Use the bitmap, e.g., display in a PictureBox or save
    minimapBitmap.Save("minimap.png", ImageFormat.Png);
}
else
{
    // Fallback, e.g., empty bitmap
    Bitmap empty = new Bitmap(1, 1);
}
```

Note: Ensure the image is parsed before accessing properties. Paths in GetFromPath use '/' as separator for nested structures.

### Extracting Assets from .wz

```csharp
// Continuing from loaded WzFile and image
WzCanvasProperty canvasProp = (WzCanvasProperty)image.GetFromPath("miniMap/canvas"); // Example path to a canvas property
if (canvasProp != null)
{
    Bitmap bitmap = canvasProp.GetLinkedWzCanvasBitmap();
    bitmap.Save("extracted.png", ImageFormat.Png);
}
```

### Packet Handling for Protocols

MapleLib also supports network packets.

```csharp
using MapleLib.PacketLib;

PacketReader reader = new PacketReader(receivedBytes); // From network stream
short opcode = reader.ReadShort();
string message = reader.ReadMapleString();
// Process based on opcode
```

For writing packets:

```csharp
PacketWriter writer = new PacketWriter();
writer.WriteShort(0x01); // Opcode
writer.WriteMapleString("Hello Maple");
// Send writer.ToArray() over network
```

## Dependencies
 - .NET 10.0 runtime
 - [spine-runtime 2.1.25 (ported to .net 7.0)](https://github.com/EsotericSoftware/spine-runtimes)
 - [lz4net 1.0.15.93+](https://github.com/MiloszKrajewski/lz4net)
 - [MonoGame.Framework.DesktopGL 3.8.2.1105+](https://www.nuget.org/packages/MonoGame.Framework.DesktopGL)
 - [NAudio 2.2.0+](https://www.nuget.org/packages/NAudio)
 - [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/)
 - [SharpDX 4.2.0](https://www.nuget.org/packages/SharpDX)

## Contributing

Contributions are welcome! Fork the repository, make changes, and submit a pull request. 

Older pre Big-Bang version of MapleStory will always be the priority, with some focus on improving compatibility with newer MapleStory versions or adding features for unsupported file types.

## License

MIT License - See LICENSE file for details.
[https://raw.githubusercontent.com/lastbattle/MapleLib/main/LICENSE](License)

