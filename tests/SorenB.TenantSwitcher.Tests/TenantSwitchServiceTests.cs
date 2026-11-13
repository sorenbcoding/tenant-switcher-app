using SorenB.TenantSwitcher.Services;
using Xunit;

namespace SorenB.TenantSwitcher.Tests;

public class TenantSwitchServiceTests
{
    [Fact]
    public void Recognises_the_disconnect_refusal_while_a_process_runs()
    {
        // Verbatim from switch.log (UiRobot 26.10).
        const string output =
            "RemoteException wrapping System.InvalidOperationException: Cannot perform action while there are any processes running on any of the Robots configured.";

        Assert.True(TenantSwitchService.IsProcessRunningError(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Orchestrator already connected!")]
    [InlineData("The Robot is not connected to Orchestrator.")]
    public void Ignores_other_disconnect_failures(string output)
    {
        Assert.False(TenantSwitchService.IsProcessRunningError(output));
    }
}
