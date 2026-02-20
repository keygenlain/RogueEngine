namespace RogueEngine.Core.Runtime.Input;

/// <summary>
/// Maps physical key names to semantic input actions.
/// </summary>
public sealed class InputActionMap
{
    private readonly Dictionary<string, HashSet<string>> _actionBindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pressedActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _justPressedActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _justReleasedActions = new(StringComparer.OrdinalIgnoreCase);

    public void Bind(string actionName, params string[] keyNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);
        if (!_actionBindings.TryGetValue(actionName, out var keys))
        {
            keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _actionBindings[actionName] = keys;
        }

        foreach (var key in keyNames.Where(k => !string.IsNullOrWhiteSpace(k)))
            keys.Add(key);
    }

    public void ClearBindings(string actionName)
    {
        _actionBindings.Remove(actionName);
    }

    public void Update(InputSnapshot snapshot)
    {
        _pressedActions.Clear();
        _justPressedActions.Clear();
        _justReleasedActions.Clear();

        foreach (var (action, keys) in _actionBindings)
        {
            if (keys.Overlaps(snapshot.PressedKeys))
                _pressedActions.Add(action);
            if (keys.Overlaps(snapshot.JustPressedKeys))
                _justPressedActions.Add(action);
            if (keys.Overlaps(snapshot.JustReleasedKeys))
                _justReleasedActions.Add(action);
        }
    }

    public bool IsPressed(string actionName) => _pressedActions.Contains(actionName);

    public bool IsJustPressed(string actionName) => _justPressedActions.Contains(actionName);

    public bool IsJustReleased(string actionName) => _justReleasedActions.Contains(actionName);
}
