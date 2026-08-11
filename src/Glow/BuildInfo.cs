namespace Glow;

// Build-time switches.
internal static class BuildInfo
{
    /// <summary>
    /// Whether Glow checks for updates on its own at startup.
    ///
    /// Off for beta builds: a tester decides when to move between versions, and
    /// beta releases are published as GitHub prereleases anyway, which the
    /// /releases/latest endpoint the updater uses never returns. The manual
    /// "Check for updates" menu item keeps working either way.
    /// </summary>
    public static readonly bool AutoUpdateCheckEnabled = false;
}
