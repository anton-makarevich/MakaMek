---
name: sanet-mvvm
description: "Use this skill whenever working in a project that references Sanet.MVVM packages (Sanet.MVVM.Core, Sanet.MVVM.Navigation.Avalonia, Sanet.MVVM.Views.Avalonia, Sanet.MVVM.DI.Avalonia). Triggers include: creating or editing ViewModels, setting up navigation, implementing modal dialogs, registering views/VMs, configuring DI, wiring up commands, managing lifecycle (AttachHandlers / DetachHandlers), or bootstrapping an AvaloniaUI app with this framework. Also trigger when the user asks how to pass data between screens, show action dialogs, compose child ViewModels, or handle reactive subscriptions inside a Sanet.MVVM project. Consult this skill even for seemingly small tasks like adding a command or a new screen — the framework has specific patterns that must be followed."
metadata:
  author: Anton Makarevich
  version: "1.0"
  framework: Sanet.MVVM
  platform: AvaloniaUI
---
# Sanet.MVVM – Agent Reference

Lightweight MVVM framework for AvaloniaUI. Key packages:

`Sanet.MVVM.Core` · `Sanet.MVVM.Navigation.Avalonia` ·
`Sanet.MVVM.Views.Avalonia` · `Sanet.MVVM.DI.Avalonia`

---

## Quick Decision Tree

Before writing any code, pick the right path:

| Task | Go to |
|---|---|
| New screen / ViewModel | §3 ViewModel + §4 View + §5 Registration |
| Navigate to existing screen | §6 Navigation |
| Modal / popup dialog (with or without a return value) | §7 Modal Dialogs |
| Simple yes/no action dialog | §8 Action Dialogs |
| First-time app setup | §1 Bootstrap + §2 DI |
| Pass data to the next screen | §6 → Data Passing |
| Subscriptions / observables | §9 Lifecycle Patterns |
| Child VM (no navigation) | §9 → Child ViewModels |

---

## §1 — App Bootstrap

name: sanet-mvvm
description: "Use this skill whenever working in a project that references Sanet.MVVM packages (Sanet.MVVM.Core, Sanet.MVVM.Navigation.Avalonia, Sanet.MVVM.Views.Avalonia, Sanet.MVVM.DI.Avalonia). Triggers include: creating or editing ViewModels, setting up navigation, implementing modal dialogs, registering views/VMs, configuring DI, wiring up commands, managing lifecycle (AttachHandlers / DetachHandlers), or bootstrapping an AvaloniaUI app with this framework. Also trigger when the user asks how to pass data between screens, show action dialogs, compose child ViewModels, or handle reactive subscriptions inside a Sanet.MVVM project. Consult this skill even for seemingly small tasks like adding a command or a new screen — the framework has specific patterns that must be followed."
metadata:
  author: Anton Makarevich
  version: "1.0"
  framework: Sanet.MVVM
  platform: AvaloniaUI
---

# Sanet.MVVM – Agent Reference

Lightweight MVVM framework for AvaloniaUI. Key packages:
`Sanet.MVVM.Core` · `Sanet.MVVM.Navigation.Avalonia` ·
`Sanet.MVVM.Views.Avalonia` · `Sanet.MVVM.DI.Avalonia`

---

## Quick Decision Tree

Before writing any code, pick the right path:

| Task | Go to |
|---|---|
| New screen / ViewModel | §3 ViewModel + §4 View + §5 Registration |
| Navigate to existing screen | §6 Navigation |
| Modal / popup dialog (with or without a return value) | §7 Modal Dialogs |
| Simple yes/no action dialog | §8 Action Dialogs |
| First-time app setup | §1 Bootstrap + §2 DI |
| Pass data to the next screen | §6 → Data Passing |
| Subscriptions / observables | §9 Lifecycle Patterns |
| Child VM (no navigation) | §9 → Child ViewModels |

---

## §1 — App Bootstrap

```csharp
// Program.cs
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .UseDependencyInjection(services =>
    {
        services.RegisterAppServices();
        services.RegisterViewModels();
    })
    .StartWithClassicDesktopLifetime(args);

// App.axaml.cs — OnFrameworkInitializationCompleted
if (Resources[AppBuilderExtensions.ServiceCollectionResourceKey]
        is not IServiceCollection services)
    throw new Exception("Services not initialized");

var sp = services.BuildServiceProvider();

if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
{
    var navService = sp.GetRequiredService<INavigationService>();
    RegisterViews(navService);              // see §5
    var mainVm = sp.GetRequiredService<MainViewModel>();
    await navService.NavigateToViewModelAsync(mainVm);
}
// Mobile / single-view: ISingleViewApplicationLifetime + SingleViewNavigationService
```

---

## §2 — Dependency Injection

**Rules:**
- `INavigationService` → **singleton**, constructed with the lifetime object and the service provider.
- ViewModels → **transient** (fresh state on every `GetNewViewModel<T>()` call).
- Shared services (repositories, settings, etc.) → **singleton**.

```csharp
// Desktop
services.AddSingleton<INavigationService>(sp =>
{
    var lifetime = sp.GetRequiredService<IClassicDesktopStyleApplicationLifetime>();
    return new NavigationService(lifetime, sp);
});

// Single-view (mobile / browser)
services.AddSingleton<INavigationService>(sp =>
{
    var lifetime = sp.GetRequiredService<ISingleViewApplicationLifetime>();
    var wrapper = new ContentControl();
    return new SingleViewNavigationService(lifetime, wrapper, sp);
});

// ViewModels — always transient
services.AddTransient<MainViewModel>();
services.AddTransient<DetailViewModel>();
services.AddTransient<FilterViewModel>();

// Domain services — singleton
services.AddSingleton<IGameService, GameService>();
```

---

## §3 — ViewModels

Inherit from `BaseViewModel`. **Do not** start work in the constructor.

```csharp
public class MyViewModel : BaseViewModel
{
    // ── Properties ────────────────────────────────────────────────────
    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);   // raises PropertyChanged
    }

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand GoToDetailCommand { get; }

    public MyViewModel(IMyService service)
    {
        _service = service;
        GoToDetailCommand = new AsyncCommand(GoToDetail);  // AsyncAwaitBestPractices.MVVM
    }

    // ── Lifecycle ──────────────────────────────────────────────────────
    public override void AttachHandlers()   // view attached to visual tree
    {
        base.AttachHandlers();
        _cts = new CancellationTokenSource();
        LoadDataAsync(_cts.Token).SafeFireAndForget();
        _service.SomethingChanged += OnSomethingChanged;
    }

    public override void DetachHandlers()   // view detached
    {
        _cts?.Cancel();
        _service.SomethingChanged -= OnSomethingChanged;
        base.DetachHandlers();
    }

    // ── Navigation helper ──────────────────────────────────────────────
    private async Task GoToDetail()
    {
        var vm = NavigationService.GetNewViewModel<DetailViewModel>()
            ?? throw new InvalidOperationException("DetailViewModel not registered");
        vm.ItemId = _selectedId;
        await NavigationService.NavigateToViewModelAsync(vm);
    }
}
```

**Provided by `BaseViewModel`:**

| Member | Purpose |
|---|---|
| `NavigationService` | Lazy `INavigationService`; throws if not injected |
| `IsBusy` | Bindable busy flag |
| `ExpectsResult` | Set by framework for modal VMs |
| `BackCommand` | Calls `NavigateBackAsync` |
| `CloseAsync(result)` | Closes the view, fires `OnResult` |
| `SetProperty(ref field, value)` | Property + change notification |
| `NotifyPropertyChanged(nameof(X))` | Manual single-prop notification |
| `NotifyAllPropertiesChanged()` | Refresh all bindings |

---

## §4 — Views

```csharp
// MyView.axaml.cs
public partial class MyView : BaseView<MyViewModel>
{
    public MyView() => InitializeComponent();

    // Optional — called once after ViewModel is assigned (DataContext already set)
    protected override void OnViewModelSet()
    {
        // safe to access ViewModel here
    }
}
```

The framework automatically calls `AttachHandlers()` / `DetachHandlers()` on visual-tree events — no manual wiring needed.

---

## §5 — View Registration

Every ViewModel–View pair **must** be registered. Do this after building the service provider, before the first navigation.

```csharp
void RegisterViews(INavigationService nav)
{
    nav.RegisterViews(typeof(MainView),     typeof(MainViewModel));
    nav.RegisterViews(typeof(DetailView),   typeof(DetailViewModel));
    nav.RegisterViews(typeof(FilterView),   typeof(FilterViewModel));
    nav.RegisterViews(typeof(SettingsView), typeof(SettingsViewModel));

    // Responsive / platform variants
    if (IsMobile())
        nav.RegisterViews(typeof(DetailViewNarrow), typeof(DetailViewModel));
    else
        nav.RegisterViews(typeof(DetailViewWide),   typeof(DetailViewModel));
}
```

> ⚠️ Missing registration → runtime exception on first navigation to that VM.

---

## §6 — Navigation

```csharp
// ── Forward (fresh state) ──────────────────────────────────────────────
var vm = NavigationService.GetNewViewModel<DetailViewModel>()
    ?? throw new InvalidOperationException("DetailViewModel not registered");
vm.ItemId = selectedId;          // pass data via properties before navigating
await NavigationService.NavigateToViewModelAsync(vm);

// ── Forward (reuse existing state) ────────────────────────────────────
var vm = NavigationService.GetViewModel<SharedViewModel>();
await NavigationService.NavigateToViewModelAsync(vm);

// ── Back ───────────────────────────────────────────────────────────────
await NavigationService.NavigateBackAsync();

// ── Root ───────────────────────────────────────────────────────────────
await NavigationService.NavigateToRootAsync();

// ── Check registration ─────────────────────────────────────────────────
bool ok = NavigationService.HasViewModel<SomeViewModel>();
```

**Data-passing patterns:**

```csharp
// 1. Property injection (preferred for simple values)
vm.Item = selectedItem;
await NavigationService.NavigateToViewModelAsync(vm);

// 2. Initialize method (preferred for multiple / complex args)
vm.Initialize(game, reason);
await NavigationService.NavigateToViewModelAsync(vm);
```

---

## §7 — Modal Dialogs (Overlay / Popup Style)

Use `ShowViewModelForResultAsync` whenever you need a **modal overlay** — whether or not the dialog returns a value. The overlay is rendered via OverlayLayer with a semi-transparent backdrop and is **not** pushed onto the back stack.

### 7a — Modal without a return value

When you just need to show a popup and don't need data back, implement `IResultProvider<bool>` (or any throwaway type) and resolve it on close:

```csharp
public class InfoDialogViewModel : BaseViewModel, IResultProvider<bool>
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public Task<bool> GetResultAsync() => _tcs.Task;

    public async Task Close()
    {
        _tcs.SetResult(true);   // value is ignored by caller
        await CloseAsync();
    }
}

// ── Caller ────────────────────────────────────────────────────────────
var vm = NavigationService.GetViewModel<InfoDialogViewModel>();
await NavigationService.ShowViewModelForResultAsync<InfoDialogViewModel, bool>(vm);
// execution continues here after the user closes the dialog
```

### 7b — Modal with a typed return value

When the dialog must hand data back to the caller, use a meaningful result type:

```csharp
public class FilterViewModel : BaseViewModel, IResultProvider<FilterCriteria?>
{
    private readonly TaskCompletionSource<FilterCriteria?> _tcs = new();

    public Task<FilterCriteria?> GetResultAsync() => _tcs.Task;

    public async Task Apply()
    {
        _tcs.SetResult(BuildCriteria());
        await CloseAsync();
    }

    public async Task Cancel()
    {
        _tcs.SetResult(null);
        await CloseAsync();
    }
}

// ── Caller ────────────────────────────────────────────────────────────
var filterVm = NavigationService.GetViewModel<FilterViewModel>();
var criteria = await NavigationService
    .ShowViewModelForResultAsync<FilterViewModel, FilterCriteria?>(filterVm);

if (criteria != null)
    ApplyFilter(criteria);
```

**Rules for all modal dialogs:**
- Always resolve `_tcs` before calling `CloseAsync()` — never leave it hanging.
- Do **not** use `BackCommand` or `NavigateBackAsync` inside modal VMs; close via `CloseAsync()`.
- The caller's `await` unblocks as soon as `GetResultAsync()` completes.

---

## §8 — Action Dialogs (Built-in)

For simple user choices that don't need a custom screen.

```csharp
var yes = new UiAction { Title = "Delete" };
var no  = new UiAction { Title = "Cancel" };

var chosen = await NavigationService.AskForActionAsync(
    "Delete item?",
    "This cannot be undone.",
    yes, no);

if (chosen == yes)
    await DeleteAsync();
```

`UiAction` also supports `Command` / `CommandParameter` for button-specific logic.

---

## §9 — Lifecycle Patterns

### Reactive subscriptions

```csharp
private IDisposable? _sub;

public override void AttachHandlers()
{
    base.AttachHandlers();
    _sub = _service.Updates
        .ObserveOn(_dispatcher.Scheduler)
        .Subscribe(OnUpdate);
}

public override void DetachHandlers()
{
    _sub?.Dispose();
    base.DetachHandlers();
}
```

### Child ViewModels (composed inline, no navigation)

```csharp
public class ParentViewModel : BaseViewModel
{
    public MapConfigViewModel MapConfig { get; }

    public ParentViewModel(IMapService mapService)
    {
        MapConfig = new MapConfigViewModel(mapService);
    }
}
```

### IDisposable

Implement when the VM owns resources that outlive visual-tree detach:

```csharp
public class MyViewModel : BaseViewModel, IDisposable
{
    public void Dispose()
    {
        _subscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

---

## §10 — Checklist for New Screens

When adding a screen, do **all** of these steps:

- [ ] Create `XyzViewModel : BaseViewModel` in the ViewModels project
- [ ] Register `services.AddTransient<XyzViewModel>()` in DI setup
- [ ] Create `XyzView : BaseView<XyzViewModel>` in the Views project
- [ ] Call `nav.RegisterViews(typeof(XyzView), typeof(XyzViewModel))` in `RegisterViews`
- [ ] Navigate using `GetNewViewModel<XyzViewModel>()` (not `new XyzViewModel(...)`)
- [ ] Put startup logic in `AttachHandlers`, not the constructor
- [ ] Clean up subscriptions/resources in `DetachHandlers`
- [ ] Use `AsyncCommand` for any async `ICommand`

---

## §11 — Anti-Patterns to Avoid

| ❌ Don't | ✅ Do instead |
|---|---|
| `new MyViewModel(...)` directly | `NavigationService.GetNewViewModel<MyViewModel>()` |
| Start async work in constructor | Use `AttachHandlers` |
| Subscribe in constructor | Subscribe in `AttachHandlers`, unsubscribe in `DetachHandlers` |
| Forget to register view/VM pair | Always add to `RegisterViews` |
| Use `NavigateToViewModelAsync` for modal/popup dialogs | Use `ShowViewModelForResultAsync` |
| Resolve `NavigationService` in constructor | Access it lazily via the `NavigationService` property |
