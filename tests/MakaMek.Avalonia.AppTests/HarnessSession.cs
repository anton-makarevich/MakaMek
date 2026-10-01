using global::Avalonia.Headless;

namespace MakaMek.Avalonia.AppTests;

/// <summary>
/// One headless session and one dispatch entry point for every test in this assembly.
///
/// This is not tidiness. <c>AvaloniaDispatcherService.Scheduler</c> returns the static
/// <c>AvaloniaScheduler.Instance</c>, which binds to whichever dispatcher first uses it. Anything
/// that constructs a <c>BattleMapViewModel</c> binds it. If a later test runs against a different
/// dispatcher, every <c>ObserveOn</c> subscription posts into the first one and delivers nothing,
/// silently: a view model that receives no commands looks exactly like a game that published none.
/// </summary>
internal static class HarnessSession
{
    private static readonly HeadlessUnitTestSession Session =
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HarnessSession).Assembly);

    /// <summary>Runs a synchronous body on the headless dispatcher.</summary>
    public static Task Run(Action body) => Session.Dispatch(body, CancellationToken.None);

    /// <summary>
    /// Runs async work. The body is wrapped so that it returns a value, which forces the
    /// Func&lt;Task&lt;T&gt;&gt; overload of Dispatch. Passing a Func&lt;Task&gt; binds to the
    /// Action overload instead, making it async void: the task is dropped and every assertion
    /// failure inside is swallowed while the test still reports green.
    /// </summary>
    public static Task Run(Func<Task> body) => Session.Dispatch(async () =>
    {
        await body();
        return true;
    }, CancellationToken.None);
}
