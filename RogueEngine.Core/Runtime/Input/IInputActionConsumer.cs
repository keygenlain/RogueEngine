namespace RogueEngine.Core.Runtime.Input;

/// <summary>
/// Implemented by runtimes that consume action-map input produced by EngineLoop.
/// </summary>
public interface IInputActionConsumer
{
    void AttachInputActionMap(InputActionMap inputActions);
}
