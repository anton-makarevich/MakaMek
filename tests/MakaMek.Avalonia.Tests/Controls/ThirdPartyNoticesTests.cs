using Avalonia.Headless;
using Avalonia.Platform;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Controls;

/// <summary>
/// The icons in the control assembly come from third-party sets whose licences have to travel with
/// the binaries, not only with the source. The notices are an Avalonia resource, so this checks the
/// file is really inside the assembly rather than next to the repository.
/// </summary>
public class ThirdPartyNoticesTests
{
    private static readonly Uri NoticesUri =
        new("avares://Sanet.MakaMek.Avalonia.Controls/Assets/THIRD-PARTY-NOTICES.txt");

    [Fact]
    public Task TheNoticesShipWithTheControlAssembly() => Dispatch(() =>
    {
        AssetLoader.Exists(NoticesUri).ShouldBeTrue("the notices are an AvaloniaResource under Assets");

        using var stream = AssetLoader.Open(NoticesUri);
        using var reader = new StreamReader(stream);
        var notices = reader.ReadToEnd();

        notices.ShouldContain("Phosphor Icons");
        notices.ShouldContain("Copyright (c) 2020 Phosphor Icons");
        notices.ShouldContain("Permission is hereby granted, free of charge");
    });

    private static Task Dispatch(Action action)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ThirdPartyNoticesTests).Assembly);
        return session.Dispatch(action, CancellationToken.None);
    }
}
