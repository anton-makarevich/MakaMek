using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using System.Collections.Specialized;
using System.Linq;

namespace Sanet.MakaMek.Avalonia.Behaviors;

public static class AutoScrollBehavior
{
    public static readonly AttachedProperty<bool> EnableAutoScrollProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
            "EnableAutoScroll",
            typeof(AutoScrollBehavior));

    public static bool GetEnableAutoScroll(ScrollViewer element) =>
        element.GetValue(EnableAutoScrollProperty);

    public static void SetEnableAutoScroll(ScrollViewer element, bool value) =>
        element.SetValue(EnableAutoScrollProperty, value);

    static AutoScrollBehavior()
    {
        EnableAutoScrollProperty.Changed.AddClassHandler<ScrollViewer>(OnEnableAutoScrollChanged);
    }

    private static void OnEnableAutoScrollChanged(ScrollViewer scrollViewer, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
            SubscribeToItemsControl(scrollViewer);
        }
        else
        {
            scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
            UnsubscribeFromCurrentCollection(scrollViewer);
        }
    }

    // Store per-instance state using a ConditionalWeakTable
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ScrollViewer, CollectionState> States = new();

    private static void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer && e.Property == ContentControl.ContentProperty)
        {
            SubscribeToItemsControl(scrollViewer);
        }
    }

    private static void SubscribeToItemsControl(ScrollViewer scrollViewer)
    {
        UnsubscribeFromCurrentCollection(scrollViewer);

        var itemsControl = scrollViewer.Content as ItemsControl
            ?? scrollViewer.GetLogicalDescendants().OfType<ItemsControl>().FirstOrDefault();

        if (itemsControl?.Items is INotifyCollectionChanged collection)
        {
            var state = States.GetOrCreateValue(scrollViewer);
            state.Collection = collection;
            state.ScrollViewer = scrollViewer;
            collection.CollectionChanged += state.OnCollectionChanged;
        }
    }

    private static void UnsubscribeFromCurrentCollection(ScrollViewer scrollViewer)
    {
        if (States.TryGetValue(scrollViewer, out var state) && state.Collection != null)
        {
            state.Collection.CollectionChanged -= state.OnCollectionChanged;
            state.Collection = null;
        }
    }

    private sealed class CollectionState
    {
        public INotifyCollectionChanged? Collection { get; set; }
        public ScrollViewer? ScrollViewer { get; set; }

        public void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                ScrollViewer?.ScrollToEnd();
            }
        }
    }
}