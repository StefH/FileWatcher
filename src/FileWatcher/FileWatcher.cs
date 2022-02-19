using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Stef.FileWatcher.VsCodeWindows;

namespace Stef.FileWatcher;

public class FileWatcher : IDisposable
{
    #region Private Properties
    private Thread? _thread;
    private EventProcessorVsCodeWindows? _processor;
    private readonly BlockingCollection<FileChangedEvent> _fileEventQueue = new();
    private FileWatcherVsCodeWindows _watcher = new();
    private FileSystemWatcher _fsw = new();
    private readonly CancellationTokenSource _cancelSource = new();
    #endregion

    #region Public Properties
    /// <summary>
    /// Folder path to watch
    /// </summary>
    public string FolderPath { get; set; }

    /// <summary>
    /// Filter string used for determining what files are monitored in a directory
    /// </summary>
    public string Filter { get; set; } = "*.*";

    /// <summary>
    /// Gets, sets the type of changes to watch for
    /// </summary>
    public NotifyFilters NotifyFilter { get; set; } = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;


    /// <summary>
    /// Gets or sets a value indicating whether subdirectories within the specified path should be monitored.
    /// </summary>
    public bool IncludeSubdirectories { get; set; } = false;


    /// <summary>
    /// Gets or sets the object used to marshal the event handler calls issued as a result of a directory change.
    /// </summary>
    public ISynchronizeInvoke? SynchronizingObject { get; set; }
    #endregion

    #region Public Events
    public delegate void DelegateOnChanged(object sender, FileChangedEvent e);
    public event DelegateOnChanged? OnChanged;

    public delegate void DelegateOnDeleted(object sender, FileChangedEvent e);
    public event DelegateOnDeleted? OnDeleted;

    public delegate void DelegateOnCreated(object sender, FileChangedEvent e);
    public event DelegateOnCreated? OnCreated;

    public delegate void DelegateOnRenamed(object sender, FileChangedEvent e);
    public event DelegateOnRenamed? OnRenamed;

    public delegate void DelegateOnError(object sender, ErrorEventArgs e);
    public event DelegateOnError? OnError;

    public delegate void DelegateOnLog(object sender, string log);
    public event DelegateOnLog? OnLog;
    #endregion

    /// <summary>
    /// Initialize new instance of FileWatcherEx
    /// </summary>
    /// <param name="path">The directory to monitor, in standard or Universal Naming Convention (UNC) notation.</param>
    public FileWatcher(string path = "")
    {
        FolderPath = path;
    }

    /// <summary>
    /// Start watching files
    /// </summary>
    public void Start()
    {
        if (!Directory.Exists(FolderPath))
        {
            return;
        }

        _processor = new EventProcessorVsCodeWindows(e =>
        {
            InvokeEvent(SynchronizingObject, e);

            void InvokeEvent(object? sender, FileChangedEvent fileEvent)
            {
                if (sender is ISynchronizeInvoke { InvokeRequired: true } synchronizingObject)
                {
                    synchronizingObject.Invoke(new Action<object, FileChangedEvent>(InvokeEvent), new object[] { synchronizingObject, e });
                }
                else
                {
                    switch (e.ChangeType)
                    {
                        case ChangeType.Changed:
                            OnChanged?.Invoke(sender ?? this, e);
                            break;

                        case ChangeType.Created:
                            OnCreated?.Invoke(sender ?? this, e);
                            break;

                        case ChangeType.Deleted:
                            OnDeleted?.Invoke(sender ?? this, e);
                            break;

                        case ChangeType.Renamed:
                            OnRenamed?.Invoke(sender ?? this, e);
                            break;
                    }
                }
            }
        },
        log =>
        {
            OnLog?.Invoke(this, log);
        });

        _thread = new Thread(() => Thread_DoingWork(_cancelSource.Token))
        {
            // this ensures the thread does not block the process from terminating!
            IsBackground = true
        };

        _thread.Start();

        // Log each event in our special format to output queue
        void OnEvent(FileChangedEvent e)
        {
            _fileEventQueue.Add(e);
        }

        // OnError
        void OnError(ErrorEventArgs? e)
        {
            if (e != null)
            {
                this.OnError?.Invoke(this, e);
            }
        }

        // Start watcher
        _watcher = new FileWatcherVsCodeWindows();

        _fsw = _watcher.Create(FolderPath, OnEvent, OnError);
        _fsw.Filter = Filter;
        _fsw.NotifyFilter = NotifyFilter;
        _fsw.IncludeSubdirectories = IncludeSubdirectories;

#if !NETSTANDARD1_3
        _fsw.SynchronizingObject = SynchronizingObject;
#endif

        // Start watching
        _fsw.EnableRaisingEvents = true;
    }

    private void Thread_DoingWork(CancellationToken cancelToken)
    {
        while (true)
        {
            if (cancelToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var e = _fileEventQueue.Take(cancelToken);
                _processor?.ProcessEvent(e);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Stop watching files
    /// </summary>
    public void Stop()
    {
        _fsw.EnableRaisingEvents = false;

        _watcher.Dispose();

        // stop the thread
        _cancelSource.Cancel();
    }

    /// <summary>
    /// Dispose the FileWatcherEx instance
    /// </summary>
    public void Dispose()
    {
        _fsw.Dispose();
    }
}
