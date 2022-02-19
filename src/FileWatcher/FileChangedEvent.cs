using System.Diagnostics.CodeAnalysis;

namespace Stef.FileWatcher;

public class FileChangedEvent
{
    /// <summary>
    /// Type of change event
    /// </summary>
    public ChangeType ChangeType { get; set; }

    /// <summary>
    /// The full path
    /// </summary>
    public string FullPath { get; set; } = null!;

    /// <summary>
    /// The old full path (used if ChangeType = RENAMED)
    /// </summary>
    [MemberNotNullWhen(true, nameof(HasOldFullPath))]
    public string? OldFullPath { get; set; }

    public bool HasOldFullPath => ChangeType == ChangeType.Renamed;
}