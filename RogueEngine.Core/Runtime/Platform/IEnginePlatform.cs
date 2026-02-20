using RogueEngine.Core.Runtime.Input;

namespace RogueEngine.Core.Runtime.Platform;

/// <summary>
/// Platform abstraction for host services used by runtime systems.
/// </summary>
public interface IEnginePlatform
{
    TimeProvider TimeProvider { get; }

    IInputSource Input { get; }

    IFileSystem FileSystem { get; }

    IEngineLogger Logger { get; }
}
