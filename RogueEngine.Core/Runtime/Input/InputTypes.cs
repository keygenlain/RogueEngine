namespace RogueEngine.Core.Runtime.Input;

public readonly record struct InputSnapshot(
    IReadOnlySet<string> PressedKeys,
    IReadOnlySet<string> JustPressedKeys,
    IReadOnlySet<string> JustReleasedKeys);

public interface IInputSource
{
    InputSnapshot Poll();
}

public sealed class NullInputSource : IInputSource
{
    private static readonly HashSet<string> EmptySet = [];

    public InputSnapshot Poll() => new(EmptySet, EmptySet, EmptySet);
}
