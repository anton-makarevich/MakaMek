using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
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

    public static readonly AttachedProperty<bool> EnableListBoxAutoScrollProperty =
        AvaloniaProperty.RegisterAttached<ListBox, bool>(
            "EnableListBoxAutoScroll",
            typeof(AutoScrollBehavior));

    public static bool GetEnableListBoxAutoScroll(ListBox element) =>
        element.GetValue(EnableListBoxAutoScrollProperty);

    public static void SetEnableListBoxAutoScroll(ListBox element, bool value) =>
        element.SetValue(EnableListBoxAutoScrollProperty, value);

    static AutoScrollBehavior()
    {
        EnableAutoScrollProperty.Changed.AddClassHandler<ScrollViewer>(OnEnableAutoScrollChanged);
        EnableListBoxAutoScrollProperty.Changed.AddClassHandler<ListBox>(OnEnableListBoxAutoScrollChanged);
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

    private static void OnEnableListBoxAutoScrollChanged(ListBox listBox, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            SubscribeToListBoxCollection(listBox);
        }
        else
        {
            UnsubscribeFromListBoxCollection(listBox);
        }
    }

    // Store per-instance state using a ConditionalWeakTable
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ScrollViewer, CollectionState> States = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ListBox, ListBoxCollectionState> ListBoxStates = new();

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

    private static void SubscribeToListBoxCollection(ListBox listBox)
    {
        UnsubscribeFromListBoxCollection(listBox);

        if (listBox.Items is INotifyCollectionChanged collection)
        {
            var state = ListBoxStates.GetOrCreateValue(listBox);
            state.Collection = collection;
            state.ListBox = listBox;
            collection.CollectionChanged += state.OnCollectionChanged;
        }
    }

    private static void UnsubscribeFromListBoxCollection(ListBox listBox)
    {
        if (ListBoxStates.TryGetValue(listBox, out var state) && state.Collection != null)
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

    private sealed class ListBoxCollectionState
    {
        public INotifyCollectionChanged? Collection { get; set; }
        public ListBox? ListBox { get; set; }

        public void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (ListBox?.Scroll is ScrollViewer scrollViewer)
                    {
                        scrollViewer.ScrollToEnd();
                    }
                }, DispatcherPriority.Loaded);
            }
        }
    }
}