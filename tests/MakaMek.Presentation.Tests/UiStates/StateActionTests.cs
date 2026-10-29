using Sanet.MakaMek.Presentation.UiStates;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class StateActionTests
{
    [Fact]
    public void Defaults_ToVisibleAndEnabledWithoutAReason()
    {
        var action = new StateAction("Push", true, () => { });

        action.IsVisible.ShouldBeTrue();
        action.IsEnabled.ShouldBeTrue();
        action.DisabledReason.ShouldBeNull();
    }

    [Fact]
    public void CanBeDisabledWithAReason()
    {
        var action = new StateAction("Push", true, () => { }, IsEnabled: false, DisabledReason: "No target");

        action.IsEnabled.ShouldBeFalse();
        action.DisabledReason.ShouldBe("No target");
    }
}
