using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MPowerKit.VirtualizeListView;

public class VirtualizeListView : ScrollView
{
    public event EventHandler<(double dx, double dy)> AdjustScrollRequested;
    public event EventHandler Refreshing;
    public event EventHandler<object> ItemAppearing;
    public event EventHandler<object> ItemDisappearing;
    public event EventHandler<object> ItemTapped;

    private object? _prevAppearedItem;

    public ScrollOrientation PrevScrollOrientation { get; protected set; }

    public VirtualizeListView()
    {
        PrevScrollOrientation = Orientation != ScrollOrientation.Neither ? Orientation : ScrollOrientation.Vertical;

        Adapter = new GroupableDataAdapter(this);

        ItemsLayout = new LinearLayout();
    }

    protected override void OnPropertyChanging([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanging(propertyName);

        if (propertyName == AdapterProperty.PropertyName)
        {
            if (Adapter?.IsDisposed is false)
            {
                Adapter.Dispose();
            }
        }
        else if (propertyName == nameof(Parent))
        {
            if (Parent is RefreshView refresh)
            {
                refresh.PropertyChanged -= Refresh_PropertyChanged;
            }
        }
    }

    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == ItemsLayoutProperty.PropertyName)
        {
            OnItemsLayoutSet();
        }
        else if (propertyName == LayoutManagerProperty.PropertyName)
        {
            this.Content = LayoutManager;
        }
        else if (propertyName == nameof(Parent))
        {
            if (Parent is RefreshView refresh)
            {
                refresh.PropertyChanged += Refresh_PropertyChanged;
            }
        }
        else if (propertyName == ItemTemplateProperty.PropertyName)
        {
            LayoutManager?.InvalidateLayout();
        }
#if IOS
        else if (propertyName == ContentSizeProperty.PropertyName)
        {
            InvalidateMeasure();
        }
#endif
        else if (propertyName == AdapterProperty.PropertyName)
        {
            Adapter?.InitCollection(ItemsSource);
        }
        else if (propertyName == ItemsSourceProperty.PropertyName
            || propertyName == IsGroupedProperty.PropertyName
            || propertyName == GroupHeaderTemplateProperty.PropertyName
            || propertyName == GroupFooterTemplateProperty.PropertyName)
        {
            Adapter?.ReloadData();
        }
        else if (propertyName == CanScrollProperty.PropertyName)
        {
            if (!CanScroll)
            {
                PrevScrollOrientation = Orientation;
                Orientation = ScrollOrientation.Neither;
            }
            else
            {
                Orientation = PrevScrollOrientation;
            }
        }
        else if (propertyName == OrientationProperty.PropertyName)
        {
            if (Orientation == ScrollOrientation.Neither) return;

            if (PrevScrollOrientation != Orientation)
            {
                LayoutManager?.InvalidateLayout();
            }

            PrevScrollOrientation = Orientation;
        }
    }

    private void Refresh_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == RefreshView.IsRefreshingProperty.PropertyName)
        {
            _prevAppearedItem = null;
        }
    }

    protected virtual void OnItemsLayoutSet()
    {
        if (ItemsLayout is LinearLayout linearLayout)
        {
            LayoutManager = new LinearItemsLayoutManager() { ItemSpacing = linearLayout.ItemSpacing };
        }
    }

    public virtual void OnItemAppearing(object data, int index)
    {
        if (ItemsSource is null) return;

        ItemAppearing?.Invoke(this, data);

        var count = CountItems(ItemsSource);

        if (count == 0) return;

        if (count <= RemainingItemsThreshold) return;

        if (index >= count - RemainingItemsThreshold)
        {
            if (_prevAppearedItem is null || _prevAppearedItem != data)
            {
                _prevAppearedItem = data;
                if (ThresholdCommand?.CanExecute(null) is true)
                {
                    ThresholdCommand.Execute(null);
                }
            }
        }
    }

    public virtual void SendItemDisappearing(object data, int index)
    {
        ItemDisappearing?.Invoke(this, data);
    }

    public virtual void OnItemTapped(object data)
    {
        ItemTapped?.Invoke(this, data);

        if (ItemTapCommand?.CanExecute(data) is not true) return;

        ItemTapCommand.Execute(data);
    }

    public virtual void AdjustScroll(double dx, double dy)
    {
        AdjustScrollRequested?.Invoke(this, (dx, dy));

#if !ANDROID
        ScrollToAsync(ScrollX + dx, ScrollY + dy, false);
#endif
    }

    private static int CountItems(IEnumerable enumerable)
    {
        int count = 0;
        foreach (var item in enumerable)
        {
            count++;
        }
        return count;
    }

    #region Adapter
    public DataAdapter Adapter
    {
        get { return (DataAdapter)GetValue(AdapterProperty); }
        protected set { SetValue(AdapterProperty, value); }
    }

    public static readonly BindableProperty AdapterProperty =
        BindableProperty.Create(
            nameof(Adapter),
            typeof(DataAdapter),
            typeof(VirtualizeListView));
    #endregion

    #region ItemsLayout
    public IItemsLayout ItemsLayout
    {
        get { return (IItemsLayout)GetValue(ItemsLayoutProperty); }
        set { SetValue(ItemsLayoutProperty, value); }
    }

    public static readonly BindableProperty ItemsLayoutProperty =
        BindableProperty.Create(
            nameof(ItemsLayout),
            typeof(IItemsLayout),
            typeof(VirtualizeListView));
    #endregion

    #region LayoutManager
    public ItemsLayoutManager LayoutManager
    {
        get { return (ItemsLayoutManager)GetValue(LayoutManagerProperty); }
        protected set { SetValue(LayoutManagerProperty, value); }
    }

    public static readonly BindableProperty LayoutManagerProperty =
        BindableProperty.Create(
            nameof(LayoutManager),
            typeof(ItemsLayoutManager),
            typeof(VirtualizeListView));
    #endregion

    #region CanScroll
    public bool CanScroll
    {
        get => (bool)GetValue(CanScrollProperty);
        set => SetValue(CanScrollProperty, value);
    }

    public static readonly BindableProperty CanScrollProperty =
        BindableProperty.Create(
            nameof(CanScroll),
            typeof(bool),
            typeof(VirtualizeListView),
            true);
    #endregion

    #region ItemTemplate
    public DataTemplate ItemTemplate
    {
        get { return (DataTemplate)GetValue(ItemTemplateProperty); }
        set { SetValue(ItemTemplateProperty, value); }
    }

    public static readonly BindableProperty ItemTemplateProperty =
        BindableProperty.Create(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(VirtualizeListView));
    #endregion

    #region HeaderTemplate
    public DataTemplate HeaderTemplate
    {
        get { return (DataTemplate)GetValue(HeaderTemplateProperty); }
        set { SetValue(HeaderTemplateProperty, value); }
    }

    public static readonly BindableProperty HeaderTemplateProperty =
        BindableProperty.Create(
            nameof(HeaderTemplate),
            typeof(DataTemplate),
            typeof(VirtualizeListView));
    #endregion

    #region FooterTemplate
    public DataTemplate FooterTemplate
    {
        get { return (DataTemplate)GetValue(FooterTemplateProperty); }
        set { SetValue(FooterTemplateProperty, value); }
    }

    public static readonly BindableProperty FooterTemplateProperty =
        BindableProperty.Create(
            nameof(FooterTemplate),
            typeof(DataTemplate),
            typeof(VirtualizeListView));
    #endregion

    #region Header
    public object Header
    {
        get { return (object)GetValue(HeaderProperty); }
        set { SetValue(HeaderProperty, value); }
    }

    public static readonly BindableProperty HeaderProperty =
        BindableProperty.Create(
            nameof(Header),
            typeof(object),
            typeof(VirtualizeListView));
    #endregion

    #region Footer
    public object Footer
    {
        get { return (object)GetValue(FooterProperty); }
        set { SetValue(FooterProperty, value); }
    }

    public static readonly BindableProperty FooterProperty =
        BindableProperty.Create(
            nameof(Footer),
            typeof(object),
            typeof(VirtualizeListView));
    #endregion

    #region ItemsSource
    public IEnumerable ItemsSource
    {
        get { return (IEnumerable)GetValue(ItemsSourceProperty); }
        set { SetValue(ItemsSourceProperty, value); }
    }

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(VirtualizeListView));
    #endregion

    #region RemainingItemsThreshold
    public int RemainingItemsThreshold
    {
        get => (int)GetValue(RemainingItemsThresholdProperty);
        set => SetValue(RemainingItemsThresholdProperty, value);
    }

    public static readonly BindableProperty RemainingItemsThresholdProperty =
        BindableProperty.Create(
            nameof(RemainingItemsThreshold),
            typeof(int),
            typeof(VirtualizeListView),
            5);
    #endregion

    #region ThresholdCommand
    public ICommand ThresholdCommand
    {
        get => (ICommand)GetValue(ThresholdCommandProperty);
        set => SetValue(ThresholdCommandProperty, value);
    }

    public static readonly BindableProperty ThresholdCommandProperty =
        BindableProperty.Create(
            nameof(ThresholdCommand),
            typeof(ICommand),
            typeof(VirtualizeListView));
    #endregion

    #region ItemTapCommand
    public ICommand ItemTapCommand
    {
        get => (ICommand)GetValue(ItemTapCommandProperty);
        set => SetValue(ItemTapCommandProperty, value);
    }

    public static readonly BindableProperty ItemTapCommandProperty =
        BindableProperty.Create(
            nameof(ItemTapCommand),
            typeof(ICommand),
            typeof(VirtualizeListView));
    #endregion

    #region IsGrouped
    public bool IsGrouped
    {
        get { return (bool)GetValue(IsGroupedProperty); }
        set { SetValue(IsGroupedProperty, value); }
    }

    public static readonly BindableProperty IsGroupedProperty =
        BindableProperty.Create(
            nameof(IsGrouped),
            typeof(bool),
            typeof(VirtualizeListView));
    #endregion

    #region GroupHeaderTemplate
    public DataTemplate GroupHeaderTemplate
    {
        get { return (DataTemplate)GetValue(GroupHeaderTemplateProperty); }
        set { SetValue(GroupHeaderTemplateProperty, value); }
    }

    public static readonly BindableProperty GroupHeaderTemplateProperty =
        BindableProperty.Create(
            nameof(GroupHeaderTemplate),
            typeof(DataTemplate),
            typeof(VirtualizeListView));
    #endregion

    #region GroupFooterTemplate
    public DataTemplate GroupFooterTemplate
    {
        get { return (DataTemplate)GetValue(GroupFooterTemplateProperty); }
        set { SetValue(GroupFooterTemplateProperty, value); }
    }

    public static readonly BindableProperty GroupFooterTemplateProperty =
        BindableProperty.Create(
            nameof(GroupFooterTemplate),
            typeof(DataTemplate),
            typeof(VirtualizeListView));
    #endregion
}