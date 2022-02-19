# FileWatcher for Windows
This project is based on:
- https://github.com/Microsoft/vscode-filewatcher-windows
- https://github.com/d2phap/FileWatcherEx

![Nuget](https://img.shields.io/nuget/dt/Stef.FileWatcher?color=%2300a8d6&logo=nuget)


## Features
- Standardize the events of C# `FileSystemWatcher`.
- No false change notifications when a file system item is created, deleted, changed or renamed.
- Support .NET 4.5, .NETStandard (1.3, 2.0 and 2.1), .NET Core 3.1, .NET 5 and .NET 6

## Usage
See Demo project for full details

```cs
using FileWatcherEx;


var _fw = new FileSystemWatcherEx(@"C:\path\to\watch");

// event handlers
_fw.OnRenamed += FW_OnRenamed;
_fw.OnCreated += FW_OnCreated;
_fw.OnDeleted += FW_OnDeleted;
_fw.OnChanged += FW_OnChanged;
_fw.OnError += FW_OnError;

// thread-safe for event handlers
_fw.SynchronizingObject = this;

// start watching
_fw.Start();



void FW_OnRenamed(object sender, FileChangedEvent e)
{
  // do something here
}
...

```

## License
[MIT](LICENSE)