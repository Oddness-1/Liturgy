using Liturgy;

namespace SandboxLibrary;

public static class LibraryRites
{
    public static int ReadyCount { get; private set; }

    [Rite.OnReady(Mode = Rite.ReadyMode.Immediate)]
    private static void LibraryReady() => ReadyCount++;
}
