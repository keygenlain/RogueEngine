namespace RogueEngine.Core.Runtime.Platform;

public interface IEngineLogger
{
    void Info(string message);

    void Warn(string message);

    void Error(string message);
}
