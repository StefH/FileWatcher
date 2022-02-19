/*---------------------------------------------------------
 * Copyright (C) Microsoft Corporation. All rights reserved.
 *--------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.IO;

namespace Stef.FileWatcher.VsCodeWindows;

// Based on https://github.com/microsoft/vscode-filewatcher-windows/tree/main/FileWatcher
internal class FileWatcherVsCodeWindows : IDisposable
{
    private string _watchPath = string.Empty;
    private Action<FileChangedEvent>? _eventCallback;
    private readonly Dictionary<string, FileSystemWatcher> _fwDictionary = new();
    private Action<ErrorEventArgs>? _onError;

    /// <summary>
    /// Create new instance of FileSystemWatcher
    /// </summary>
    /// <param name="path">Full folder path to watcher</param>
    /// <param name="onEvent">onEvent callback</param>
    /// <param name="onError">onError callback</param>
    /// <returns></returns>
    public FileSystemWatcher Create(string path, Action<FileChangedEvent> onEvent, Action<ErrorEventArgs> onError)
    {
        _watchPath = path;
        _eventCallback = onEvent;
        _onError = onError;

        var watcher = new FileSystemWatcher
        {
            Path = _watchPath,
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite
                | NotifyFilters.FileName
                | NotifyFilters.DirectoryName,
        };

        // Bind internal events to manipulate the possible symbolic links
        watcher.Created += MakeWatcher_Created;
        watcher.Deleted += MakeWatcher_Deleted;

        watcher.Changed += (_, e) => ProcessEvent(e, ChangeType.Changed);
        watcher.Created += (_, e) => ProcessEvent(e, ChangeType.Created);
        watcher.Deleted += (_, e) => ProcessEvent(e, ChangeType.Deleted);
        watcher.Renamed += (_, e) => ProcessEvent(e);
        watcher.Error += (_, e) => onError(e);

        //changing this to a higher value can lead into issues when watching UNC drives
        watcher.InternalBufferSize = 32768;
        _fwDictionary.Add(path, watcher);

        foreach (var dirInfo in new DirectoryInfo(path).GetDirectories())
        {
            var attrs = File.GetAttributes(dirInfo.FullName);

            // TODO: consider skipping hidden/system folders? 
            // See IG Issue #405 comment below
            // https://github.com/d2phap/ImageGlass/issues/405
            if (attrs.HasFlag(FileAttributes.Directory) && attrs.HasFlag(FileAttributes.ReparsePoint))
            {
                try
                {
                    MakeWatcher(dirInfo.FullName);
                }
                catch
                {
                    // IG Issue #405: throws exception on Windows 10
                    // for "c:\users\user\application data" folder and sub-folders.
                }
            }
        }

        return watcher;
    }


    /// <summary>
    /// Process event for type = [CHANGED; DELETED; CREATED]
    /// </summary>
    /// <param name="e"></param>
    /// <param name="changeType"></param>
    private void ProcessEvent(FileSystemEventArgs e, ChangeType changeType)
    {
        _eventCallback?.Invoke(new()
        {
            ChangeType = changeType,
            FullPath = e.FullPath,
        });
    }


    /// <summary>
    /// Process event for type = RENAMED
    /// </summary>
    /// <param name="e"></param>
    private void ProcessEvent(RenamedEventArgs e)
    {
        _eventCallback?.Invoke(new()
        {
            ChangeType = ChangeType.Renamed,
            FullPath = e.FullPath,
            OldFullPath = e.OldFullPath,
        });
    }


    private void MakeWatcher(string path)
    {
        if (!_fwDictionary.ContainsKey(path))
        {
            var fileSystemWatcherRoot = new FileSystemWatcher
            {
                Path = path,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            // Bind internal events to manipulate the possible symbolic links
            fileSystemWatcherRoot.Created += MakeWatcher_Created;
            fileSystemWatcherRoot.Deleted += MakeWatcher_Deleted;

            fileSystemWatcherRoot.Changed += (_, e) => ProcessEvent(e, ChangeType.Changed);
            fileSystemWatcherRoot.Created += (_, e) => ProcessEvent(e, ChangeType.Created);
            fileSystemWatcherRoot.Deleted += (_, e) => ProcessEvent(e, ChangeType.Deleted);
            fileSystemWatcherRoot.Renamed += (_, e) => ProcessEvent(e);
            fileSystemWatcherRoot.Error += (_, e) => _onError?.Invoke(e);

            _fwDictionary.Add(path, fileSystemWatcherRoot);
        }

        foreach (var item in new DirectoryInfo(path).GetDirectories())
        {
            var attrs = File.GetAttributes(item.FullName);

            // If is a directory and symbolic link
            if (attrs.HasFlag(FileAttributes.Directory) && attrs.HasFlag(FileAttributes.ReparsePoint))
            {
                if (!_fwDictionary.ContainsKey(item.FullName))
                {
                    var fswItem = new FileSystemWatcher
                    {
                        Path = item.FullName,
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true,
                    };

                    // Bind internal events to manipulate the possible symbolic links
                    fswItem.Created += MakeWatcher_Created;
                    fswItem.Deleted += MakeWatcher_Deleted;

                    fswItem.Changed += (_, e) => ProcessEvent(e, ChangeType.Changed);
                    fswItem.Created += (_, e) => ProcessEvent(e, ChangeType.Created);
                    fswItem.Deleted += (_, e) => ProcessEvent(e, ChangeType.Deleted);
                    fswItem.Renamed += (_, e) => ProcessEvent(e);
                    fswItem.Error += (_, e) => _onError?.Invoke(e);

                    _fwDictionary.Add(item.FullName, fswItem);
                }

                MakeWatcher(item.FullName);
            }
        }
    }


    private void MakeWatcher_Created(object sender, FileSystemEventArgs fileSystemEventArgs)
    {
        try
        {
            var attrs = File.GetAttributes(fileSystemEventArgs.FullPath);
            if (attrs.HasFlag(FileAttributes.Directory) && attrs.HasFlag(FileAttributes.ReparsePoint))
            {
                var watcherCreated = new FileSystemWatcher
                {
                    Path = fileSystemEventArgs.FullPath,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                // Bind internal events to manipulate the possible symbolic links
                watcherCreated.Created += MakeWatcher_Created;
                watcherCreated.Deleted += MakeWatcher_Deleted;

                watcherCreated.Changed += (_, e) => ProcessEvent(e, ChangeType.Changed);
                watcherCreated.Created += (_, e) => ProcessEvent(e, ChangeType.Created);
                watcherCreated.Deleted += (_, e) => ProcessEvent(e, ChangeType.Deleted);
                watcherCreated.Renamed += (_, e) => ProcessEvent(e);
                watcherCreated.Error += (_, e) => _onError?.Invoke(e);

                _fwDictionary.Add(fileSystemEventArgs.FullPath, watcherCreated);
            }
        }
        catch (Exception ex)
        {
            _onError?.Invoke(new ErrorEventArgs(ex));
        }
    }

    private void MakeWatcher_Deleted(object sender, FileSystemEventArgs e)
    {
        // If object removed, then I will dispose and remove them from dictionary
        if (_fwDictionary.ContainsKey(e.FullPath))
        {
            _fwDictionary[e.FullPath].Dispose();
            _fwDictionary.Remove(e.FullPath);
        }
    }
    
    /// <summary>
    /// Dispose the instance
    /// </summary>
    public void Dispose()
    {
        foreach (var item in _fwDictionary)
        {
            item.Value.Dispose();
        }
    }
}