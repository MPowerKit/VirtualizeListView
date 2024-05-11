using Android.Content;
using Android.Views;
using Android.Widget;

using AndroidX.Core.Widget;

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace MPowerKit.VirtualizeListView;

public class VirtualizeListViewRenderer : ScrollViewHandler
{
    protected override MauiScrollView CreatePlatformView()
    {
        var scrollView = new SmoothScrollView(
            new ContextThemeWrapper(MauiContext!.Context, Resource.Style.scrollViewTheme), null!,
                Resource.Attribute.scrollViewStyle)
        {
            ClipToOutline = true,
            FillViewport = true
        };

        return scrollView;
    }

    protected override void ConnectHandler(MauiScrollView platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView is VirtualizeListView listView)
        {
            listView.AdjustScrollRequested += ListView_AdjustScrollRequested;
        }
    }

    protected override void DisconnectHandler(MauiScrollView platformView)
    {
        if (VirtualView is VirtualizeListView listView)
        {
            listView.AdjustScrollRequested -= ListView_AdjustScrollRequested;
        }

        base.DisconnectHandler(platformView);
    }

    private void ListView_AdjustScrollRequested(object? sender, (double dx, double dy) e)
    {
        if (PlatformView is SmoothScrollView scrollView)
        {
            scrollView.AdjustScroll((int)this.Context.ToPixels(e.dx), (int)this.Context.ToPixels(e.dy));
        }
    }
}

public class SmoothScrollView : MauiScrollView
{
    private OverScroller _scroller;

    public SmoothScrollView(Context context) : base(context)
    {
        Init(context);
    }

    public SmoothScrollView(Context context, Android.Util.IAttributeSet attrs) : base(context, attrs)
    {
        Init(context);
    }

    public SmoothScrollView(Context context, Android.Util.IAttributeSet attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
    {
        Init(context);
    }

    private void Init(Context context)
    {
        var field = Java.Lang.Class.FromType(typeof(NestedScrollView)).GetDeclaredField("mScroller");
        field.Accessible = true;

        _scroller = (field.Get(this) as OverScroller)!;
    }

    public virtual void AdjustScroll(int dx, int dy)
    {
        if (!_scroller.IsFinished)
        {
            var velocity = _scroller.CurrVelocity + dy;

            var direction = _scroller.FinalY < _scroller.CurrY ? -velocity : velocity;

            this.ScrollBy(dx, dy);

            _scroller.ForceFinished(true);
            Fling((int)direction);
        }
        else
        {
            ScrollBy(dx, dy);
        }
    }

    protected override void OnScrollChanged(int l, int t, int oldl, int oldt)
    {
        base.OnScrollChanged(l, t, oldl, oldt);

        try
        {
            if (!CanScrollVertically(1) || !CanScrollVertically(-1))
            {
                if (!_scroller.IsFinished)
                {
                    _scroller.AbortAnimation();
                }
                return;
            }
        }
        catch (Exception ex)
        {

        }
    }

    public override void ComputeScroll()
    {
        if (!_scroller.ComputeScrollOffset() || _scroller.IsFinished) return;

        int oldY = ScrollY;
        int newY = _scroller.CurrY;
        if (oldY != newY)
        {
            ScrollTo(0, newY);
        }
        PostInvalidateOnAnimation();
    }
}