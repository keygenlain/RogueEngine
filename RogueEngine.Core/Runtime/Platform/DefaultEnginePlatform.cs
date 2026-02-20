using RogueEngine.Core.Runtime.Input;

namespace RogueEngine.Core.Runtime.Platform;

/// <summary>
/// Default host implementation using .NET system services.
/// </summary>
public sealed class DefaultEnginePlatform : IEnginePlatform
{
    public DefaultEnginePlatform(
        TimeProvider? timeProvider = null,
        IInputSource? input = null,
        IFileSystem? fileSystem = null,
        IEngineLogger? logger = null)
    {
        TimeProvider = timeProvider ?? TimeProvider.System;
        Input = input ?? new NullInputSource();
        FileSystem = fileSystem ?? new SystemFileSystem();
        Logger = logger ?? new ConsoleEngineLogger();
    }

    public TimeProvider TimeProvider { get; }

    public IInputSource Input { get; }

    public IFileSystem FileSystem { get; }

    public IEngineLogger Logger { get; }
}

public sealed class SystemFileSystem : IFileSystem
{
    public bool Exists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string contents)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, contents);
    }
}

public sealed class ConsoleEngineLogger : IEngineLogger
{
    public void Info(string message) => Console.WriteLine($"[INFO] {message}");

    public void Warn(string message) => Console.WriteLine($"[WARN] {message}");

    public void Error(string message) => Console.WriteLine($"[ERROR] {message}");
}
