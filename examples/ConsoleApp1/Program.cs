// See https://aka.ms/new-console-template for more information

using Stef.FileWatcher;

Console.WriteLine("Hello, World!");

var _fw = new FileWatcher(@"C:\temp\fs")
{
    IncludeSubdirectories = true
};
_fw.OnCreated += FW_OnX;
_fw.OnDeleted += FW_OnX;
_fw.OnChanged += FW_OnX;
_fw.OnRenamed += FW_OnX;


//_fw.OnCreated += FW_OnCreated;
//_fw.OnDeleted += FW_OnDeleted;
//_fw.OnChanged += FW_OnChanged;
//_fw.OnError += FW_OnError;

// thread-safe for event handlers
//_fw.SynchronizingObject = s;

// start watching
_fw.Start();

Console.ReadLine();

void FW_OnX(object sender, FileChangedEvent e)
{
    Console.WriteLine($"[cha] {Enum.GetName(typeof(ChangeType), e.ChangeType)} | {e.FullPath}");
}