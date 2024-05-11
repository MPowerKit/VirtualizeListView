using CoreGraphics;

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

using UIKit;

using WebKit;

namespace MPowerKit.VirtualizeListView;

public class FixedRefreshViewRenderer : ViewHandler<FixedRefreshView, FixedRefreshViewRenderer.RefreshViewWrapper>
{
    public static IPropertyMapper<FixedRefreshView, FixedRefreshViewRenderer> RefreshViewRendererMapper = new PropertyMapper<FixedRefreshView, FixedRefreshViewRenderer>(ViewMapper)
    {
        [FixedRefreshView.IsRefreshingProperty.PropertyName] = MapIsRefreshing,
        [FixedRefreshView.ContentProperty.PropertyName] = MapContent,
        [FixedRefreshView.RefreshColorProperty.PropertyName] = MapRefreshColor,
        [FixedRefreshView.BackgroundProperty.PropertyName] = MapBackground,
        [FixedRefreshView.IsPullToRefreshEnabledProperty.PropertyName] = MapIsPullToRefreshEnabled,
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

    protected override RefreshViewWrapper CreatePlatformView()
    {
        return new();
    }

    protected override void ConnectHandler(RefreshViewWrapper platformView)
    {
        base.ConnectHandler(platformView);

        platformView.RefreshControl.ValueChanged += OnRefresh;

        if (!VirtualView.IsRefreshing) return;

        PlatformView.IsRefreshing = false;
        PlatformView.IsRefreshing = true;
    }

    protected override void DisconnectHandler(RefreshViewWrapper platformView)
    {
        platformView.RefreshControl.ValueChanged -= OnRefresh;

        base.DisconnectHandler(platformView);
    }

    protected virtual void OnRefresh(object? sender, EventArgs e)
    {
        if (VirtualView.IsPullToRefreshEnabled && VirtualView.IsEnabled)
        {
            ((IElementController)VirtualView).SetValueFromRenderer(FixedRefreshView.IsRefreshingProperty, true);

            PlatformView.TryOffsetRefreshAnimated(PlatformView, true);
        }
        else PlatformView.IsRefreshing = false;
    }

    public static void MapBackground(FixedRefreshViewRenderer handler, FixedRefreshView view)
    {
        handler.PlatformView.RefreshControl.UpdateBackground(view);
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

    public static void MapIsPullToRefreshEnabled(FixedRefreshViewRenderer handler, FixedRefreshView refreshView)
    {
        handler.PlatformView?.UpdateIsEnabled(refreshView.IsPullToRefreshEnabled);
    }

    static void UpdateIsRefreshing(FixedRefreshViewRenderer handler)
    {
        var virtualView = handler.VirtualView;
        var platformView = handler.PlatformView;
        var refreshing = virtualView.IsRefreshing;

        if (refreshing && (!virtualView.IsPullToRefreshEnabled || !virtualView.IsEnabled)
            || platformView.IsRefreshingAnimated == refreshing) return;

        platformView.IsRefreshingAnimated = refreshing;
    }

    static void UpdateContent(FixedRefreshViewRenderer handler)
    {
        handler.PlatformView.UpdateContent(handler.VirtualView.Content, handler.MauiContext);
    }

    static void UpdateRefreshColor(FixedRefreshViewRenderer handler)
    {
        var color = handler.VirtualView?.RefreshColor?.ToPlatform();

        if (color is null) return;

        var isRefreshing = handler.VirtualView!.IsRefreshing;

        handler.PlatformView.RefreshControl.TintColor = color;

        handler.PlatformView.IsRefreshing = true;
        handler.PlatformView.IsRefreshing = false;

        if (isRefreshing) handler.PlatformView.IsRefreshing = true;
    }

    public class RefreshViewWrapper : UIView
    {
        public UIRefreshControl RefreshControl { get; protected set; }

        protected nfloat OriginalY { get; set; }
        protected nfloat RefreshControlHeight { get; set; }
        protected UIView RefreshControlParent { get; set; }
        protected UIView? ContentView { get; set; }

        public RefreshViewWrapper()
        {
            RefreshControl = new();
            RefreshControlParent = this;
        }

        public override CGRect Bounds
        {
            get => base.Bounds;
            set
            {
                base.Bounds = value;

                if (ContentView is null) return;

                ContentView.Frame = value;
            }
        }

        public virtual void UpdateContent(IView? content, IMauiContext? mauiContext)
        {
            if (RefreshControlParent is not null)
            {
                TryRemoveRefresh(RefreshControlParent);
            }

            ContentView?.RemoveFromSuperview();

            if (content is null || mauiContext is null) return;

            ContentView = content.ToPlatform(mauiContext);
            AddSubview(ContentView);
            TryInsertRefresh(ContentView);
        }

        public virtual bool IsRefreshing
        {
            get => RefreshControl.Refreshing;
            set
            {
                if (value)
                {
                    TryOffsetRefresh(this, value);
                    RefreshControl.BeginRefreshing();
                }
                else
                {
                    RefreshControl.EndRefreshing();
                    TryOffsetRefresh(this, value);
                }
            }
        }

        public virtual bool IsRefreshingAnimated
        {
            get => RefreshControl.Refreshing;
            set
            {
                if (value)
                {
                    TryOffsetRefreshAnimated(this, value);
                    RefreshControl.BeginRefreshing();
                }
                else
                {
                    RefreshControl.EndRefreshing();
                    TryOffsetRefreshAnimated(this, value);
                }
            }
        }

        protected virtual bool TryOffsetRefresh(UIView view, bool refreshing)
        {
            if (view is UIScrollView scrollView)
            {
                if (refreshing)
                {
                    scrollView.ContentOffset = new(scrollView.ContentOffset.X, OriginalY - RefreshControlHeight);
                }
                else scrollView.ContentOffset = new(scrollView.ContentOffset.X, OriginalY);

                return true;
            }

            if (view is WKWebView) return true;

            if (view.Subviews is null) return false;

            for (int i = 0; i < view.Subviews.Length; i++)
            {
                var control = view.Subviews[i];
                if (TryOffsetRefresh(control, refreshing)) return true;
            }

            return false;
        }

        public virtual bool TryOffsetRefreshAnimated(UIView view, bool refreshing)
        {
            if (view is UIScrollView scrollView)
            {
                if (refreshing)
                {
                    UpdateContentOffset(scrollView, OriginalY - RefreshControlHeight);
                }
                else UpdateContentOffset(scrollView, OriginalY);

                return true;
            }

            if (view is WKWebView) return true;

            if (view.Subviews is null) return false;

            for (int i = 0; i < view.Subviews.Length; i++)
            {
                var control = view.Subviews[i];
                if (TryOffsetRefreshAnimated(control, refreshing)) return true;
            }

            return false;
        }

        protected virtual void UpdateContentOffset(UIScrollView scrollView, nfloat offset, Action? completed = null)
        {
            UIView.Animate(0.2, () => scrollView.ContentOffset = new(scrollView.ContentOffset.X, offset), completed);
        }

        protected virtual bool TryRemoveRefresh(UIView view, int index = 0)
        {
            RefreshControlParent = view;

            if (RefreshControl.Superview is not null)
            {
                RefreshControl.RemoveFromSuperview();
            }

            if (view is UIScrollView scrollView)
            {
                if (CanUseRefreshControlProperty())
                {
                    scrollView.RefreshControl = null;
                }

                return true;
            }

            if (view.Subviews is null) return false;

            for (int i = 0; i < view.Subviews.Length; i++)
            {
                var control = view.Subviews[i];

                if (TryRemoveRefresh(control, i)) return true;
            }

            return false;
        }

        protected virtual bool TryInsertRefresh(UIView view, int index = 0)
        {
            RefreshControlParent = view;

            if (view is UIScrollView scrollView)
            {
                if (CanUseRefreshControlProperty())
                {
                    scrollView.RefreshControl = RefreshControl;
                }
                else scrollView.InsertSubview(RefreshControl, index);

                scrollView.AlwaysBounceVertical = true;

                OriginalY = scrollView.ContentOffset.Y;
                RefreshControlHeight = RefreshControl.Frame.Height;

                return true;
            }

            if (view is WKWebView webView)
            {
                webView.ScrollView.InsertSubview(RefreshControl, index);
                return true;
            }

            if (view.Subviews is null) return false;

            for (int i = 0; i < view.Subviews.Length; i++)
            {
                var control = view.Subviews[i];
                if (TryInsertRefresh(control, i)) return true;
            }

            return false;
        }

        public virtual void UpdateIsEnabled(bool isRefreshViewEnabled)
        {
            RefreshControl.Enabled = isRefreshViewEnabled;

            UserInteractionEnabled = true;

            if (IsRefreshing || (isRefreshViewEnabled && RefreshControl.Superview is not null)) return;

            if (isRefreshViewEnabled)
            {
                TryInsertRefresh(RefreshControlParent);
            }
            else TryRemoveRefresh(RefreshControlParent);

            UserInteractionEnabled = true;
        }

        protected virtual bool CanUseRefreshControlProperty()
        {
            return this.GetNavigationController()?.NavigationBar?.PrefersLargeTitles ?? true;
        }
    }
}