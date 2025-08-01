using System.Collections;
using System.Collections.Specialized;

namespace MPowerKit.VirtualizeListView;

public static class BindableLayout
{
    private static readonly Dictionary<IEnumerable, List<WeakReference<BindableObject>>> _bindableObjects = [];

    #region ItemTemplate
    public static readonly BindableProperty ItemTemplateProperty =
        BindableProperty.CreateAttached(
            "ItemTemplate",
            typeof(DataTemplate),
            typeof(BindableLayout),
            null);

    public static DataTemplate GetItemTemplate(BindableObject view) => (DataTemplate)view.GetValue(ItemTemplateProperty);

    public static void SetItemTemplate(BindableObject view, DataTemplate value) => view.SetValue(ItemTemplateProperty, value);
    #endregion

    #region ItemsSource
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.CreateAttached(
            "ItemsSource",
            typeof(IEnumerable),
            typeof(BindableLayout),
            null,
            propertyChanged: OnItemsSourcePropertyChanged,
            propertyChanging: OnItemsSourcePropertyChanging);

    public static IEnumerable GetItemsSource(BindableObject view) => (IEnumerable)view.GetValue(ItemsSourceProperty);

    public static void SetItemsSource(BindableObject view, IEnumerable value) => view.SetValue(ItemsSourceProperty, value);

    private static void OnItemsSourcePropertyChanging(BindableObject bindable, object oldValue, object newValue)
    {
        if (oldValue is INotifyCollectionChanged collectionChanged)
        {
            collectionChanged.CollectionChanged -= CollectionChanged_CollectionChanged;
        }

        if (bindable is Layout layout)
        {
            ClearItems(layout);
        }

        if (oldValue is IEnumerable enumerable)
        {
            RemoveBindableObject(enumerable, bindable);
        }
    }

    private static void RemoveBindableObject(IEnumerable itemsSource, BindableObject bindable)
    {
        if (_bindableObjects.TryGetValue(itemsSource, out var bindableList))
        {
            bindableList.RemoveAll(weakRef => !weakRef.TryGetTarget(out var target) || ReferenceEquals(target, bindable));

            if (bindableList.Count == 0) _bindableObjects.Remove(itemsSource);
        }
    }

    private static void OnItemsSourcePropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (newValue is INotifyCollectionChanged collectionChanged)
        {
            collectionChanged.CollectionChanged += CollectionChanged_CollectionChanged;
        }

        if (newValue is IEnumerable enumerable && bindable is Layout layout)
        {
            AddBindableObject(enumerable, layout);

            AddItems(layout, enumerable, 0);
        }
    }

    private static void AddBindableObject(IEnumerable itemsSource, BindableObject bindable)
    {
        if (!_bindableObjects.TryGetValue(itemsSource, out var value))
        {
            _bindableObjects[itemsSource] = value = [];
        }

        value.Add(new(bindable));
    }

    private static void CollectionChanged_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is not IEnumerable enumerable || !_bindableObjects.TryGetValue(enumerable, out var wrList) || wrList?.Count is null or 0) return;

        var layoutList = wrList.Select(wr => wr.TryGetTarget(out var target) ? target : null).OfType<Layout>();
        foreach (var layout in layoutList)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddItems(layout, e.NewItems, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    RemoveItems(layout, e.OldItems, e.OldStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    AddItems(layout, e.NewItems, e.NewStartingIndex);
                    RemoveItems(layout, e.OldItems, e.OldStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    MoveItems(layout, e.OldItems, e.OldStartingIndex, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    ClearItems(layout);
                    break;
            }
        }
    }

    public static void ClearItems(this Layout layout)
    {
        var items = layout.Children.OfType<VisualElement>().ToList();
        layout.Clear();
        foreach (var item in items)
        {
            DisconnectItem(item);
        }
    }

    private static void AddItems(Layout layout, IEnumerable? items, int index)
    {
        if (items is null) return;

        var template = GetItemTemplate(layout);

        if (template is null) return;

        foreach (var item in items)
        {
            while (template is DataTemplateSelector selector)
            {
                template = selector.SelectTemplate(item, layout);
            }

            if (template.CreateContent() is not View view) continue;
            view.BindingContext = item;
            layout.Insert(index++, view);
        }
    }

    private static void RemoveItems(Layout layout, IEnumerable? items, int index)
    {
        if (items is null) return;

        foreach (var item in items)
        {
            var view = layout.ElementAt(index) as VisualElement;
            layout.RemoveAt(index);

            DisconnectItem(view);
        }
    }

    public static void DisconnectItem(this VisualElement? visualElement)
    {
        if (visualElement is null) return;

        visualElement.BindingContext = null;
        visualElement.Behaviors?.Clear();
#if NET9_0_OR_GREATER
        visualElement.DisconnectHandlers();
#endif
    }

    private static void MoveItems(Layout layout, IEnumerable? items, int oldIndex, int newIndex)
    {
        if (items is null) return;

        foreach (var item in items)
        {
            layout.Move(oldIndex, newIndex);
        }
    }
    #endregion
}