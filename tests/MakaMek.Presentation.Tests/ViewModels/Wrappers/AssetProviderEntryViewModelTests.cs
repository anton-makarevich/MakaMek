using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class AssetProviderEntryViewModelTests
{
    private static AssetProviderConfigData Provider(
        string id,
        AssetType assetType = AssetType.Units,
        bool isActive = true,
        bool isDefault = false) =>
        new(id, ProviderType.Filesystem, assetType, "/assets/" + id, isActive, isDefault, 0);

    [Fact]
    public void Constructor_ExposesProviderProperties()
    {
        var provider = new AssetProviderConfigData(
            "local", ProviderType.Filesystem, AssetType.Hexes, "C:\\assets", IsActive: true, IsDefault: false, SortOrder: 3);

        var sut = new AssetProviderEntryViewModel(provider);

        sut.Id.ShouldBe("local");
        sut.ProviderType.ShouldBe(ProviderType.Filesystem);
        sut.AssetType.ShouldBe(AssetType.Hexes);
        sut.UrlOrPath.ShouldBe("C:\\assets");
        sut.IsDefault.ShouldBeFalse();
        sut.SortOrder.ShouldBe(3);
        sut.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenDefault_CanRemoveIsFalse()
    {
        var sut = new AssetProviderEntryViewModel(Provider("bucket", isDefault: true));

        sut.CanRemove.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenNotDefault_CanRemoveIsTrue()
    {
        var sut = new AssetProviderEntryViewModel(Provider("local", isDefault: false));

        sut.CanRemove.ShouldBeTrue();
    }

    [Fact]
    public void CanDeactivate_DefaultsToTrue()
    {
        var sut = new AssetProviderEntryViewModel(Provider("a"));

        sut.CanDeactivate.ShouldBeTrue();
    }

    [Fact]
    public void CanDeactivate_Set_RaisesPropertyChanged()
    {
        var sut = new AssetProviderEntryViewModel(Provider("a"));
        string? changed = null;
        sut.PropertyChanged += (_, e) => changed = e.PropertyName;

        sut.CanDeactivate = false;

        changed.ShouldBe(nameof(AssetProviderEntryViewModel.CanDeactivate));
        sut.CanDeactivate.ShouldBeFalse();
    }

    [Fact]
    public void IsActive_Set_RaisesPropertyChanged()
    {
        var sut = new AssetProviderEntryViewModel(Provider("a"));
        string? changed = null;
        sut.PropertyChanged += (_, e) => changed = e.PropertyName;

        sut.IsActive = false;

        changed.ShouldBe(nameof(AssetProviderEntryViewModel.IsActive));
        sut.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void IsActive_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var sut = new AssetProviderEntryViewModel(Provider("a"));
        var raised = false;
        sut.PropertyChanged += (_, _) => raised = true;

        sut.IsActive = true;

        raised.ShouldBeFalse();
    }

    [Fact]
    public async Task ToggleActiveCommand_WhenExecuted_InvokesOnToggleActive()
    {
        AssetProviderEntryViewModel? toggled = null;
        var sut = new AssetProviderEntryViewModel(Provider("a"), onToggleActive: e => { toggled = e; return Task.CompletedTask; });

        await ((IAsyncCommand)sut.ToggleActiveCommand).ExecuteAsync();

        toggled.ShouldBe(sut);
    }

    [Fact]
    public async Task RemoveCommand_WhenExecuted_InvokesOnRemove()
    {
        AssetProviderEntryViewModel? removed = null;
        var sut = new AssetProviderEntryViewModel(Provider("a"), onRemove: e => { removed = e; return Task.CompletedTask; });

        await ((IAsyncCommand)sut.RemoveCommand).ExecuteAsync();

        removed.ShouldBe(sut);
    }

    [Fact]
    public async Task ToggleActiveCommand_WhenCallbackIncomplete_ExecutionStaysIncomplete()
    {
        var completion = new TaskCompletionSource();
        AssetProviderEntryViewModel? toggled = null;
        var sut = new AssetProviderEntryViewModel(Provider("a"), onToggleActive: e =>
        {
            toggled = e;
            return completion.Task;
        });

        // Act
        var execution = ((IAsyncCommand)sut.ToggleActiveCommand).ExecuteAsync();

        // Assert - command execution must not complete until the awaited callback finishes
        await Task.Delay(50);
        execution.IsCompleted.ShouldBeFalse();
        toggled.ShouldBe(sut);

        completion.SetResult();
        await execution;
        execution.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void CanEdit_WhenDefault_IsFalse()
    {
        var sut = new AssetProviderEntryViewModel(Provider("bucket", isDefault: true));

        sut.CanEdit.ShouldBeFalse();
    }

    [Fact]
    public void CanEdit_WhenNotDefault_IsTrue()
    {
        var sut = new AssetProviderEntryViewModel(Provider("local", isDefault: false));

        sut.CanEdit.ShouldBeTrue();
    }

    [Fact]
    public async Task StartEditing_WhenNotDefault_PopulatesEditableFieldsAndEntersEditing()
    {
        var sut = new AssetProviderEntryViewModel(Provider("local", isDefault: false));

        await sut.StartEditing();

        sut.IsEditing.ShouldBeTrue();
        sut.EditableProviderType.ShouldBe(ProviderType.Filesystem);
        sut.EditableAssetType.ShouldBe(AssetType.Units);
        sut.EditableUrlOrPath.ShouldBe("/assets/local");
    }

    [Fact]
    public async Task StartEditing_WhenDefault_DoesNotEnterEditing()
    {
        var sut = new AssetProviderEntryViewModel(Provider("bucket", isDefault: true));

        await sut.StartEditing();

        sut.IsEditing.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveCommand_WhenExecuted_InvokesOnSavedWithPendingProvider()
    {
        AssetProviderEntryViewModel? saved = null;
        var sut = new AssetProviderEntryViewModel(
            Provider("local", isDefault: false),
            onSaved: e =>
            {
                saved = e;
                return Task.CompletedTask;
            });
        await sut.StartEditing();
        sut.EditableProviderType = ProviderType.GitHub;
        sut.EditableAssetType = AssetType.Hexes;
        sut.EditableUrlOrPath = "  https://new-url  ";

        await ((IAsyncCommand)sut.SaveCommand).ExecuteAsync();

        saved.ShouldBe(sut);
        sut.PendingProvider.ProviderType.ShouldBe(ProviderType.GitHub);
        sut.PendingProvider.AssetType.ShouldBe(AssetType.Hexes);
        sut.PendingProvider.UrlOrPath.ShouldBe("https://new-url");
        sut.IsEditing.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveCommand_WhenUrlOrPathEmpty_DoesNotInvokeOnSaved()
    {
        var saved = false;
        var sut = new AssetProviderEntryViewModel(
            Provider("local", isDefault: false),
            onSaved: _ =>
            {
                saved = true;
                return Task.CompletedTask;
            });
        await sut.StartEditing();
        sut.EditableUrlOrPath = "   ";

        await ((IAsyncCommand)sut.SaveCommand).ExecuteAsync();

        saved.ShouldBeFalse();
        sut.IsEditing.ShouldBeTrue();
    }

    [Fact]
    public async Task CancelCommand_WhenExecuted_DiscardsEditsAndInvokesOnCancelled()
    {
        AssetProviderEntryViewModel? cancelled = null;
        var sut = new AssetProviderEntryViewModel(
            Provider("local", isDefault: false),
            onCancelled: e => cancelled = e);
        await sut.StartEditing();
        sut.EditableUrlOrPath = "https://changed";

        await ((IAsyncCommand)sut.CancelCommand).ExecuteAsync();

        cancelled.ShouldBe(sut);
        sut.IsEditing.ShouldBeFalse();
        sut.EditableProviderType.ShouldBe(ProviderType.Filesystem);
        sut.EditableAssetType.ShouldBe(AssetType.Units);
        sut.EditableUrlOrPath.ShouldBe("/assets/local");
    }
}
