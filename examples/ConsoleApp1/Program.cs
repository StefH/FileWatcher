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