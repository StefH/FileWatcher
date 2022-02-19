# FileWatcher
This project is based on:
- https://github.com/Microsoft/vscode-filewatcher-windows
- https://github.com/d2phap/FileWatcherEx

## NuGet
[![NuGet Badge](https://buildstats.info/nuget/Stef.FileWatcher)](https://www.nuget.org/packages/Stef.FileWatcher)

## Features
- Standardize the events of C# `FileSystemWatcher`.
- No false change notifications when a file system item is created, deleted, changed or renamed.
- Supports .NET 4.5, .NETStandard (1.3, 2.0 and 2.1), .NET Core 3.1, .NET 5 and .NET 6

## Usage
See Demo project for full details.

``` c#
using Stef.FileWatcher;

var path = @"C:\temp\fs";

Console.WriteLine("Watching {0}", path);

var fileWatcher = new FileWatcher(path)
{
    IncludeSubdirectories = true
};
fileWatcher.OnCreated += OnX;
fileWatcher.OnDeleted += OnX;
fileWatcher.OnChanged += OnX;
fileWatcher.OnRenamed += OnX;

// start watching
fileWatcher.Start();

Console.WriteLine("Press a key to exit");
Console.ReadKey();

void OnX(object sender, FileChangedEvent e)
{
    Console.WriteLine($"{e.ChangeType} | {e.FullPath}");
}
```

## License
[MIT](LICENSE)
