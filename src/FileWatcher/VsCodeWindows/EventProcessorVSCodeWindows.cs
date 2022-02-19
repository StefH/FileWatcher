/*---------------------------------------------------------
 * Copyright (C) Microsoft Corporation. All rights reserved.
 *--------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Stef.FileWatcher.VsCodeWindows;

// Based on https://github.com/microsoft/vscode-filewatcher-windows/tree/main/FileWatcher
internal class EventProcessorVsCodeWindows
{
    /// <summary>
    /// Aggregate and only emit events when changes have stopped for this duration (in ms)
    /// </summary>
    private static int EVENT_DELAY = 50;

    /// <summary>
    /// Warn after certain time span of event spam (in ticks)
    /// </summary>
    private static int EVENT_SPAM_WARNING_THRESHOLD = 60 * 1000 * 10000;

    private readonly object _lock = new();
    private Task? _delayTask;

    private readonly List<FileChangedEvent> _events = new();
    private readonly Action<FileChangedEvent> _handleEvent;

    private readonly Action<string> _logger;

    private long _lastEventTime;
    private long _delayStarted;

    private long _spamCheckStartTime;
    private bool _spamWarningLogged;


    private IEnumerable<FileChangedEvent> NormalizeEvents(FileChangedEvent[] events)
    {
        var mapPathToEvents = new Dictionary<string, FileChangedEvent>();
        var eventsWithoutDuplicates = new List<FileChangedEvent>();

        // Normalize duplicates
        foreach (var newEvent in events)
        {
            mapPathToEvents.TryGetValue(newEvent.FullPath, out var oldEvent); // Try get event from newEvent.FullPath

            switch (oldEvent)
            {
                case { ChangeType: ChangeType.Created } when newEvent.ChangeType == ChangeType.Deleted: // CREATED + DELETED => remove
                    mapPathToEvents.Remove(oldEvent.FullPath);
                    eventsWithoutDuplicates.Remove(oldEvent);
                    break;

                case { ChangeType: ChangeType.Deleted } when newEvent.ChangeType == ChangeType.Created: // DELETED + CREATED => CHANGED
                    oldEvent.ChangeType = ChangeType.Changed;
                    break;

                case { ChangeType: ChangeType.Created } when newEvent.ChangeType == ChangeType.Changed: // CREATED + CHANGED => CREATED
                    // Do nothing
                    break;

                default:
                    {
                        // Otherwise

                        if (newEvent.ChangeType == ChangeType.Renamed)
                        { // If <ANY> + RENAMED
                            do
                            {
                                mapPathToEvents.TryGetValue(newEvent.OldFullPath!, out var renameFromEvent); // Try get event from newEvent.OldFullPath

                                switch (renameFromEvent)
                                {
                                    case { ChangeType: ChangeType.Created }:
                                        {
                                            // If rename from CREATED file
                                            // Remove data about the CREATED file 
                                            mapPathToEvents.Remove(renameFromEvent.FullPath);
                                            eventsWithoutDuplicates.Remove(renameFromEvent);
                                            // Handle new event as CREATED
                                            newEvent.ChangeType = ChangeType.Created;
                                            newEvent.OldFullPath = null;

                                            if (oldEvent is { ChangeType: ChangeType.Deleted })
                                            { // DELETED + CREATED => CHANGED
                                                newEvent.ChangeType = ChangeType.Changed;
                                            }

                                            break;
                                        }
                                    case { ChangeType: ChangeType.Renamed }: // If rename from RENAMED file, remove data about the RENAMED file 
                                        mapPathToEvents.Remove(renameFromEvent.FullPath);
                                        eventsWithoutDuplicates.Remove(renameFromEvent);
                                        // Change OldFullPath
                                        newEvent.OldFullPath = renameFromEvent.OldFullPath;

                                        // Check again
                                        continue;

                                    default:
                                        // Otherwise do nothing
                                        // mapPathToEvents.TryGetValue(newEvent.OldFullPath, out oldEvent);
                                        // Try get event from newEvent.OldFullPath
                                        break;
                                }
                            } while (false);
                        }

                        if (oldEvent != null)
                        {
                            // If old event exists
                            // Replace old event data with data from the new event
                            oldEvent.ChangeType = newEvent.ChangeType;
                            oldEvent.OldFullPath = newEvent.OldFullPath;
                        }
                        else
                        {
                            // If old event is not exist
                            // Add new event
                            mapPathToEvents.Add(newEvent.FullPath, newEvent);
                            eventsWithoutDuplicates.Add(newEvent);
                        }

                        break;
                    }
            }
        }

        // Handle deletes
        var deletedPaths = new List<string>();

        // This algorithm will remove all DELETE events up to the root folder
        // that got deleted if any. This ensures that we are not producing
        // DELETE events for each file inside a folder that gets deleted.
        //
        // 1.) split ADD/CHANGE and DELETED events
        // 2.) sort short deleted paths to the top
        // 3.) for each DELETE, check if there is a deleted parent and ignore the event in that case

        return eventsWithoutDuplicates
            .Select((e, n) => new KeyValuePair<int, FileChangedEvent>(n, e)) // store original position value
            .OrderBy(e => e.Value.FullPath.Length) // shortest path first
            .Where(e =>
            {
                if (e.Value.ChangeType == ChangeType.Deleted)
                {
                    if (deletedPaths.Any(d => IsParent(e.Value.FullPath, d)))
                    {
                        return false; // DELETE is ignored if parent is deleted already
                    }

                    // otherwise mark as deleted
                    deletedPaths.Add(e.Value.FullPath);
                }

                return true;
            })
            .OrderBy(e => e.Key) // restore original position
            .Select(e => e.Value); //  remove unnecessary position value
    }


    private static bool IsParent(string p, string candidate)
    {
        return p.IndexOf(candidate + Path.DirectorySeparatorChar, StringComparison.Ordinal) == 0;
    }
    public EventProcessorVsCodeWindows(Action<FileChangedEvent> onEvent, Action<string> onLogging)
    {
        _handleEvent = onEvent;
        _logger = onLogging;
    }

    public void ProcessEvent(FileChangedEvent fileEvent)
    {
        lock (_lock)
        {
            var now = DateTime.Now.Ticks;

            // Check for spam
            if (_events.Count == 0)
            {
                _spamWarningLogged = false;
                _spamCheckStartTime = now;
            }
            else if (!_spamWarningLogged && _spamCheckStartTime + EVENT_SPAM_WARNING_THRESHOLD < now)
            {
                _spamWarningLogged = true;
                _logger($"Warning: Watcher is busy catching up wit {_events.Count} file changes in 60 seconds. Latest path is '{fileEvent.FullPath}'");
            }

            // Add into our queue
            _events.Add(fileEvent);
            _lastEventTime = now;

            // Process queue after delay
            if (_delayTask == null)
            {
                // Create function to buffer events
                void Func(Task value)
                {
                    lock (_lock)
                    {
                        // Check if another event has been received in the meantime
                        if (_delayStarted == _lastEventTime)
                        {
                            // Normalize and handle
                            var normalized = NormalizeEvents(_events.ToArray());
                            foreach (var e in normalized)
                            {
                                _handleEvent(e);
                            }

                            // Reset
                            _events.Clear();
                            _delayTask = null;
                        }

                        // Otherwise we have received a new event while this task was
                        // delayed and we reschedule it.
                        else
                        {
                            _delayStarted = _lastEventTime;
                            _delayTask = Task.Delay(EVENT_DELAY).ContinueWith(Func);
                        }
                    }
                }

                // Start function after delay
                _delayStarted = _lastEventTime;
                _delayTask = Task.Delay(EVENT_DELAY).ContinueWith(Func);
            }
        }
    }
}