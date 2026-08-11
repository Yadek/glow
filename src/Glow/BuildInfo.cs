namespace Glow;

// Build-time switches.
internal static class BuildInfo
{
    /// <summary>
    /// Whether Glow checks for updates on its own at startup.
    ///
    /// Turn this off while a beta is being tested: a tester decides when to move
    /// between versions. Beta releases are published as GitHub prereleases
    /// anyway, which the /releases/latest endpoint the updater uses never
    /// returns. The manual "Check for updates" menu item works either way.
    /// </summary>
    public static readonly bool AutoUpdateCheckEnabled = true;
}
