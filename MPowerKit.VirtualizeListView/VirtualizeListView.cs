using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MPowerKit.VirtualizeListView;

public partial class VirtualizeListView : ScrollView
{
    public event EventHandler<(double dx, double dy)>? AdjustScrollRequested;
    public event EventHandler<object>? ItemAppearing;
    public event EventHandler<object>? ItemDisappearing;
    public event EventHandler<object>? ItemTapped;

    protected object? PrevAppearedItem { get; set; }

    public ScrollOrientation PrevScrollOrientation { get; protected set; }

    public VirtualizeListView()
    {
        this.Scrolled += VirtualizeListView_Scrolled;

        PrevScrollOrientation = Orientation != ScrollOrientation.Neither ? Orientation : ScrollOrientation.Vertical;

        OnLayoutManagerChanged();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        ChangeScrollSpeed();
    }

    protected override void OnPropertyChanging([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanging(propertyName);

        if (propertyName == AdapterProperty.PropertyName)
        {
            OnAdapterChanging();
        }
        else if (propertyName == StickyHeadersProperty.PropertyName)
        {
            OnStickyHeadersChanging();
        }
    }

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == ItemsLayoutProperty.PropertyName)
        {
            LayoutManager = GetLayoutManger()!;
        }
        else if (propertyName == LayoutManagerProperty.PropertyName)
        {
            OnLayoutManagerChanged();
        }
        else if (propertyName == ItemTemplateProperty.PropertyName)
        {
            OnItemTemplateChanged();
        }
        else if (propertyName == ScrollSpeedProperty.PropertyName)
        {
            ChangeScrollSpeed();
        }
#if MACIOS
        else if (propertyName == ContentSizeProperty.PropertyName)
        {
            InvalidateMeasure();
        }
#endif
        else if (propertyName == AdapterProperty.PropertyName)
        {
            OnAdapterChanged();
        }
        else if (propertyName == ItemsSourceProperty.PropertyName
            || propertyName == IsGroupedProperty.PropertyName
            || propertyName == GroupHeaderTemplateProperty.PropertyName
            || propertyName == GroupFooterTemplateProperty.PropertyName)
        {
            ReloadData();
        }
        else if (propertyName == HeaderProperty.PropertyName
            || propertyName == HeaderTemplateProperty.PropertyName)
        {
            OnHeaderChanged();
        }
        else if (propertyName == FooterProperty.PropertyName
            || propertyName == FooterTemplateProperty.PropertyName)
        {
            OnFooterChanged();
        }
        else if (propertyName == CanScrollProperty.PropertyName)
        {
            OnCanScrollChanged();
        }
        else if (propertyName == OrientationProperty.PropertyName)
        {
            OnOrientationChanged();
        }
        else if (propertyName == PaddingProperty.PropertyName)
        {
            OnSizeChanged();
        }
        else if (propertyName == StickyHeadersProperty.PropertyName)
        {
            OnStickyHeadersChanged();
        }
    }

    protected virtual void OnStickyHeadersChanging()
    {
        if (!StickyHeaders) return;

        var stickyHeaders = ItemDecorators.OfType<StickyHeaderItemDecorator>().FirstOrDefault();
        ItemDecorators.Remove(stickyHeaders);
    }

    protected virtual void OnStickyHeadersChanged()
    {
        if (!StickyHeaders || ItemDecorators.Any(d => d is StickyHeaderItemDecorator)) return;

        ItemDecorators.Add(new StickyHeaderItemDecorator());
    }

    protected virtual void OnHeaderChanged()
    {
        Adapter?.SendHeaderChanged();
    }

    protected virtual void OnFooterChanged()
    {
        Adapter?.SendFooterChanged();
    }

    protected virtual void VirtualizeListView_Scrolled(object? sender, ScrolledEventArgs e)
    {
        LayoutManager?.SendListViewScrolled(e);
    }

    protected virtual void ReloadData()
    {
        Adapter?.ReloadData();
    }

    protected virtual void OnCanScrollChanged()
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

    protected virtual void OnOrientationChanged()
    {
        if (Orientation == ScrollOrientation.Neither) return;

        if (PrevScrollOrientation != Orientation)
        {
            LayoutManager?.InvalidateLayout();
        }

        PrevScrollOrientation = Orientation;
    }

    protected virtual void ChangeScrollSpeed()
    {
        if (Handler is null) return;

#if MACIOS
        var scroll = this.Handler?.PlatformView as UIKit.UIScrollView;
        scroll!.DecelerationRate = ScrollSpeed is ScrollSpeed.Normal
            ? UIKit.UIScrollView.DecelerationRateFast
            : UIKit.UIScrollView.DecelerationRateNormal;
#endif
    }

    protected override void OnParentChanging(ParentChangingEventArgs args)
    {
        base.OnParentChanging(args);

        if (args.OldParent is RefreshView oldRefresh)
        {
            oldRefresh.PropertyChanged -= Refresh_PropertyChanged;
        }

        if (args.NewParent is RefreshView newRefresh)
        {
            newRefresh.PropertyChanged += Refresh_PropertyChanged;
        }
    }

    protected virtual void Refresh_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == RefreshView.IsRefreshingProperty.PropertyName)
        {
            PrevAppearedItem = null;
        }
    }

    protected virtual void OnItemTemplateChanged()
    {
        LayoutManager?.InvalidateLayout();
    }

    protected virtual void OnLayoutManagerChanged()
    {
        var prevLayoutManager = this.Content;

        this.Content = LayoutManager;

#if NET9_0_OR_GREATER
        prevLayoutManager?.DisconnectHandlers();
#endif
    }

    protected virtual VirtualizeItemsLayoutManger? GetLayoutManger()
    {
        if (ItemsLayout is LinearLayout linearLayout)
        {
            return new LinearItemsLayoutManager()
            {
                ItemSpacing = linearLayout.ItemSpacing,
                CachePoolSize = linearLayout.InitialCachePoolSize,
                BindingContext = null
            };
        }
        else if (ItemsLayout is GridLayout gridLayout)
        {
            return new GridItemsLayoutManager()
            {
                VerticalItemSpacing = gridLayout.VerticalItemSpacing,
                HorizontalItemsSpacing = gridLayout.HorizontalItemSpacing,
                Span = gridLayout.Span,
                CachePoolSize = gridLayout.InitialCachePoolSize,
                BindingContext = null
            };
        }

        return null;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        OnSizeChanged();
    }

    protected virtual void OnSizeChanged()
    {
        LayoutManager?.SendListViewContentSizeChanged();
    }

    protected virtual void OnAdapterChanging()
    {
        if (Adapter?.IsDisposed is false)
        {
            Adapter.Dispose();
        }

        LayoutManager?.SendListViewAdapterReset();
    }

    protected virtual void OnAdapterChanged()
    {
        Adapter.InitCollection(ItemsSource);
        LayoutManager?.SendListViewAdapterReset();
    }

    public virtual void OnItemAppearing(object item, int realPosition, int realItemsCount)
    {
        if (ItemsSource is null) return;

        ItemAppearing?.Invoke(this, item);

        if (realItemsCount <= RemainingItemsThreshold) return;

        if (realPosition < realItemsCount - RemainingItemsThreshold) return;

        if (PrevAppearedItem is null || !ReferenceEquals(PrevAppearedItem, item))
        {
            PrevAppearedItem = item;
            if (ThresholdCommand?.CanExecute(null) is true)
            {
                ThresholdCommand.Execute(null);
            }
        }
    }

    public virtual void OnItemDisappearing(object data, int realPosition, int realItemsCount)
    {
        if (ItemsSource is null) return;

        ItemDisappearing?.Invoke(this, data);
    }

    public virtual void OnItemTapped(object data)
    {
        ItemTapped?.Invoke(this, data);

        if (ItemTapCommand?.CanExecute(data) is not true) return;

        ItemTapCommand.Execute(data);
    }

    public virtual IEnumerable<(object Item, int RealPosition, int RealItemsCount)> GetAllVisibleItems()
    {
        var adapter = Adapter;

        var visibleItems = LayoutManager.VisibleDataItems;

        foreach (var (data, position) in visibleItems)
        {
            var (realPosition, realItemsCount) = adapter.GetRealPositionAndCount(data, position);

            yield return (data, realPosition, realItemsCount);
        }
    }

    public virtual void AdjustScroll(double dx, double dy)
    {
        AdjustScrollRequested?.Invoke(this, (dx, dy));

        var pv = Handler?.PlatformView;
#if MACIOS
        var scroll = pv as UIKit.UIScrollView;
        scroll?.SetContentOffset(new(ScrollX + dx, ScrollY + dy), false);
#elif WINDOWS
        var scroll = pv as Microsoft.UI.Xaml.Controls.ScrollViewer;
        scroll?.ChangeView(ScrollX + dx, ScrollY + dy, null, true);
#elif ANDROID
        var scroll = pv as SmoothScrollView;
        scroll?.AdjustScroll(dx, dy);
#endif
    }

    public virtual bool IsOrientation(ScrollOrientation orientation)
    {
        return Orientation == orientation
            || (Orientation == ScrollOrientation.Neither && PrevScrollOrientation == orientation);
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        var horizontalPadding = Padding.HorizontalThickness + Margin.HorizontalThickness;
        var verticalPadding = Padding.VerticalThickness + Margin.VerticalThickness;

        if (LayoutManager is not null)
            LayoutManager.AvailableSpace = new(widthConstraint - horizontalPadding, heightConstraint - verticalPadding);

        base.MeasureOverride(widthConstraint, heightConstraint);

        var desiredWidth = widthConstraint;
        if (HorizontalOptions != LayoutOptions.Fill)
        {
            desiredWidth = horizontalPadding
                + (Content?.DesiredSize.Width ?? 0d);
        }

        var desiredHeight = heightConstraint;
        if (VerticalOptions != LayoutOptions.Fill)
        {
            desiredHeight = verticalPadding
                + (Content?.DesiredSize.Height ?? 0d);
        }

        return new(Math.Min(desiredWidth, widthConstraint), Math.Min(desiredHeight, heightConstraint));
    }

    public virtual async Task ScrollToItem(object item, ScrollToPosition scrollToPosition, bool animated)
    {
        if (LayoutManager is null) return;

        await LayoutManager.ScrollToItem(item, scrollToPosition, animated);
    }

    #region StickyHeaders
    public bool StickyHeaders
    {
        get { return (bool)GetValue(StickyHeadersProperty); }
        set { SetValue(StickyHeadersProperty, value); }
    }

    public static readonly BindableProperty StickyHeadersProperty =
        BindableProperty.Create(
            nameof(StickyHeaders),
            typeof(bool),
            typeof(VirtualizeListView));
    #endregion

    #region ItemDecorators
    public ObservableCollection<ItemDecorator> ItemDecorators
    {
        get => (ObservableCollection<ItemDecorator>)GetValue(ItemDecoratorsProperty);
    }

    public static readonly BindableProperty ItemDecoratorsProperty =
        BindableProperty.Create(
            nameof(ItemDecorators),
            typeof(ObservableCollection<ItemDecorator>),
            typeof(VirtualizeListView),
            defaultValueCreator: bindable =>
            {
                var listview = (bindable as VirtualizeListView)!;
                var decorators = new ObservableCollection<ItemDecorator>();
                if (listview.StickyHeaders)
                {
                    decorators.Add(new StickyHeaderItemDecorator());
                }
                return decorators;
            });
    #endregion

    #region Adapter
    public DataAdapter Adapter
    {
        get => (DataAdapter)GetValue(AdapterProperty);
        protected set => SetValue(AdapterProperty, value);
    }

    public static readonly BindableProperty AdapterProperty =
        BindableProperty.Create(
            nameof(Adapter),
            typeof(DataAdapter),
            typeof(VirtualizeListView),
            defaultValueCreator: bindable =>
            {
                return new GroupableDataAdapter((bindable as VirtualizeListView)!);
            });
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
            typeof(VirtualizeListView),
            new LinearLayout());
    #endregion

    #region LayoutManager
    public VirtualizeItemsLayoutManger LayoutManager
    {
        get { return (VirtualizeItemsLayoutManger)GetValue(LayoutManagerProperty); }
        protected set { SetValue(LayoutManagerProperty, value); }
    }

    public static readonly BindableProperty LayoutManagerProperty =
        BindableProperty.Create(
            nameof(LayoutManager),
            typeof(VirtualizeItemsLayoutManger),
            typeof(VirtualizeListView),
            defaultValueCreator: bindable =>
            {
                return (bindable as VirtualizeListView)!.GetLayoutManger();
            });
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
        get { return GetValue(HeaderProperty); }
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
        get { return GetValue(FooterProperty); }
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

    #region ScrollSpeed
    public ScrollSpeed ScrollSpeed
    {
        get { return (ScrollSpeed)GetValue(ScrollSpeedProperty); }
        set { SetValue(ScrollSpeedProperty, value); }
    }

    public static readonly BindableProperty ScrollSpeedProperty =
        BindableProperty.Create(
            nameof(ScrollSpeed),
            typeof(ScrollSpeed),
            typeof(VirtualizeListView),
            ScrollSpeed.Normal);
    #endregion
}