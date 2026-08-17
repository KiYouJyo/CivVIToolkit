namespace CivVIToolkit.Core.Game;

public sealed record GameExecutable(
    string Path,
    GraphicsBackend GraphicsBackend)
{
    public string FileName => System.IO.Path.GetFileName(Path);
}
