using Android.Content;

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace MPowerKit.VirtualizeListView;

public class FixedRefreshViewRenderer : ViewHandler<FixedRefreshView, FixedRefreshViewRenderer.RefreshLayout>
{
    public static IPropertyMapper<FixedRefreshView, FixedRefreshViewRenderer> RefreshViewRendererMapper = new PropertyMapper<FixedRefreshView, FixedRefreshViewRenderer>(ViewMapper)
    {
        [FixedRefreshView.IsRefreshingProperty.PropertyName] = MapIsRefreshing,
        [FixedRefreshView.ContentProperty.PropertyName] = MapContent,
        [FixedRefreshView.RefreshColorProperty.PropertyName] = MapRefreshColor,
        [FixedRefreshView.BackgroundProperty.PropertyName] = MapBackground,
        [FixedRefreshView.IsEnabledProperty.PropertyName] = (r, v) => { },
    };

    public static CommandMapper<FixedRefreshView, FixedRefreshViewRenderer> RefreshViewRendererCommandMapper = new(ViewCommandMapper)
    {
    };

    public FixedRefreshViewRenderer() : base(RefreshViewRendererMapper, RefreshViewRendererCommandMapper)
    {

    }

    public FixedRefreshViewRenderer(IPropertyMapper? mapper)
        : base(mapper ?? RefreshViewRendererMapper, RefreshViewRendererCommandMapper)
    {
    }

    public FixedRefreshViewRenderer(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? RefreshViewRendererMapper, commandMapper ?? RefreshViewRendererCommandMapper)
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

    static void UpdateContent(FixedRefreshViewRenderer handler)
    {
        handler.PlatformView.UpdateContent(handler.VirtualView.Content, handler.MauiContext);
    }

    static void UpdateRefreshColor(FixedRefreshViewRenderer handler)
    {
        var color = handler.VirtualView.RefreshColor?.ToInt();
        if (color is null) return;

        handler.PlatformView.SetColorSchemeColors(color.Value);
    }

    static void UpdateIsRefreshing(FixedRefreshViewRenderer handler)
    {
        var virtualView = handler.VirtualView;
        var platformView = handler.PlatformView;
        var refreshing = virtualView.IsRefreshing;

        if (refreshing && (!virtualView.IsPullToRefreshEnabled || !virtualView.IsEnabled)
            || platformView.Refreshing == refreshing) return;

        platformView.Refreshing = refreshing;
    }

    static void UpdateBackground(FixedRefreshViewRenderer handler)
    {
        var color = (handler.VirtualView as IView)?.Background?.ToColor()?.ToInt();
        if (color is null) return;

        handler.PlatformView.SetProgressBackgroundColorSchemeColor(color.Value);
    }

    public static void MapBackground(FixedRefreshViewRenderer handler, FixedRefreshView refreshView)
    {
        UpdateBackground(handler);
    }

    public static void MapIsRefreshing(FixedRefreshViewRenderer handler, FixedRefreshView refreshView)
    {
        UpdateIsRefreshing(handler);
    }

    public static void MapContent(FixedRefreshViewRenderer handler, FixedRefreshView refreshView)
    {
        UpdateContent(handler);
    }

    public static void MapRefreshColor(FixedRefreshViewRenderer handler, FixedRefreshView refreshView)
    {
        UpdateRefreshColor(handler);
    }

    public class RefreshLayout : MauiSwipeRefreshLayout
    {
        private readonly FixedRefreshViewRenderer _renderer;

        public RefreshLayout(Context context, FixedRefreshViewRenderer renderer) : base(context)
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