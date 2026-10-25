using System.Runtime.CompilerServices;
using Bunit;

namespace Riftcaster.WebUI.Tests;

// How long a test waits for the page to catch up with something that happens on another thread (a port switch
// or an update check, say) before it says it didn't. bUnit's default of one second is too short for a busy CI
// machine; a wait only lasts as long as the thing it waits for, so a long limit costs nothing when it passes.
internal static class WaitTimeout
{
    [ModuleInitializer]
    internal static void Set() => BunitContext.DefaultWaitTimeout = TimeSpan.FromSeconds(10);
}
