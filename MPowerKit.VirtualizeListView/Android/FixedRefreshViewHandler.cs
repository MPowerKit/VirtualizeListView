using Android.Content;

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace MPowerKit.VirtualizeListView;

public class FixedRefreshViewHandler : ViewHandler<FixedRefreshView, FixedRefreshViewHandler.RefreshLayout>
{
    public static IPropertyMapper<FixedRefreshView, FixedRefreshViewHandler> FixedRefreshViewHandlerMapper = new PropertyMapper<FixedRefreshView, FixedRefreshViewHandler>(ViewMapper)
    {
        [FixedRefreshView.IsRefreshingProperty.PropertyName] = MapIsRefreshing,
        [FixedRefreshView.ContentProperty.PropertyName] = MapContent,
        [FixedRefreshView.RefreshColorProperty.PropertyName] = MapRefreshColor,
        [FixedRefreshView.BackgroundProperty.PropertyName] = MapBackground,
        [FixedRefreshView.IsEnabledProperty.PropertyName] = (h, v) => { },
    };

    public static CommandMapper<FixedRefreshView, FixedRefreshViewHandler> FixedRefreshViewHandlerCommandMapper = new(ViewCommandMapper)
    {
    };

    public FixedRefreshViewHandler() : base(FixedRefreshViewHandlerMapper, FixedRefreshViewHandlerCommandMapper)
    {

    }

    public FixedRefreshViewHandler(IPropertyMapper? mapper)
        : base(mapper ?? FixedRefreshViewHandlerMapper, FixedRefreshViewHandlerCommandMapper)
    {
    }

    public FixedRefreshViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? FixedRefreshViewHandlerMapper, commandMapper ?? FixedRefreshViewHandlerCommandMapper)
    {
    }

    protected override RefreshLayout CreatePlatformView()
    {
        return new RefreshLayout(Context, this);
    }

    protected override void ConnectHandler(RefreshLayout platformView)
    {
        base.ConnectHandler(platformView);

        platformView.Refresh += OnSwipeRefresh;
    }

    protected override void DisconnectHandler(RefreshLayout platformView)
    {
        platformView.Refresh -= OnSwipeRefresh;
        platformView.UpdateContent(null, null);

        base.DisconnectHandler(platformView);
    }

    protected virtual void OnSwipeRefresh(object? sender, EventArgs e)
    {
        if (VirtualView.IsPullToRefreshEnabled && VirtualView.IsEnabled)
        {
            ((IElementController)VirtualView).SetValueFromRenderer(FixedRefreshView.IsRefreshingProperty, true);
        }
        else PlatformView.Refreshing = false;
    }

    private static void UpdateContent(FixedRefreshViewHandler handler)
    {
        handler.PlatformView.UpdateContent(handler.VirtualView.Content, handler.MauiContext);
    }

    private static void UpdateRefreshColor(FixedRefreshViewHandler handler)
    {
        var color = handler.VirtualView.RefreshColor?.ToInt();
        if (color is null) return;

        handler.PlatformView.SetColorSchemeColors(color.Value);
    }

    private static void UpdateIsRefreshing(FixedRefreshViewHandler handler)
    {
        var virtualView = handler.VirtualView;
        var platformView = handler.PlatformView;
        var refreshing = virtualView.IsRefreshing;

        if (refreshing && (!virtualView.IsPullToRefreshEnabled || !virtualView.IsEnabled)
            || platformView.Refreshing == refreshing) return;

        platformView.Refreshing = refreshing;
    }

    private static void UpdateBackground(FixedRefreshViewHandler handler)
    {
        var color = (handler.VirtualView as IView)?.Background?.ToColor()?.ToInt();
        if (color is null) return;

        handler.PlatformView.SetProgressBackgroundColorSchemeColor(color.Value);
    }

    public static void MapBackground(FixedRefreshViewHandler handler, FixedRefreshView refreshView)
    {
        UpdateBackground(handler);
    }

    public static void MapIsRefreshing(FixedRefreshViewHandler handler, FixedRefreshView refreshView)
    {
        UpdateIsRefreshing(handler);
    }

    public static void MapContent(FixedRefreshViewHandler handler, FixedRefreshView refreshView)
    {
        UpdateContent(handler);
    }

    public static void MapRefreshColor(FixedRefreshViewHandler handler, FixedRefreshView refreshView)
    {
        UpdateRefreshColor(handler);
    }

    public class RefreshLayout : MauiSwipeRefreshLayout
    {
        private readonly FixedRefreshViewHandler _renderer;

        public RefreshLayout(Context context, FixedRefreshViewHandler renderer) : base(context)
        {
            _renderer = renderer;
        }

        public override bool Enabled
        {
            get => _renderer?.VirtualView?.IsPullToRefreshEnabled ?? base.Enabled;
            set
            {
                base.Enabled = (_renderer?.VirtualView is null || (value && _renderer.VirtualView.IsPullToRefreshEnabled))
                    ? value
                    : _renderer.VirtualView.IsPullToRefreshEnabled;
            }
        }
    }
}