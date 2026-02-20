namespace RogueEngine.Core.Runtime.Platform;

public interface IFileSystem
{
    bool Exists(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string contents);
}
