using Android.Content;
using Android.Views;
using Android.Widget;

using AndroidX.Core.Widget;

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace MPowerKit.VirtualizeListView;

public partial class VirtualizeListViewHandler : ScrollViewHandler
{
    protected override MauiScrollView CreatePlatformView()
    {
        var scrollView = new SmoothScrollView(
            new ContextThemeWrapper(MauiContext!.Context, Resource.Style.scrollViewTheme),
            null!,
            Resource.Attribute.scrollViewStyle,
            (VirtualView as VirtualizeListView)!)
        {
            ClipToOutline = true,
            FillViewport = true
        };

        return scrollView;
    }
}

public class SmoothScrollView : MauiScrollView
{
    private OverScroller? _scroller;
    private VirtualizeListView? _listView;

    public SmoothScrollView(Context context, VirtualizeListView listView) : base(context)
    {
        Init(listView);
    }

    public SmoothScrollView(Context context, Android.Util.IAttributeSet attrs, VirtualizeListView listView) : base(context, attrs)
    {
        Init(listView);
    }

    public SmoothScrollView(Context context, Android.Util.IAttributeSet attrs, int defStyleAttr, VirtualizeListView listView) : base(context, attrs, defStyleAttr)
    {
        Init(listView);
    }

    private void Init(VirtualizeListView listView)
    {
        var field = Java.Lang.Class.FromType(typeof(NestedScrollView)).GetDeclaredField("mScroller");
        field.Accessible = true;

        _scroller = field.Get(this) as OverScroller;
        _listView = listView;
    }

    public virtual void AdjustScroll(double dxdp, double dydp)
    {
        if (_scroller is null) return;

        var dx = (int)this.Context.ToPixels(dxdp);
        var dy = (int)this.Context.ToPixels(dydp);

        if (!_scroller.IsFinished)
        {
            var velocity = _scroller.CurrVelocity + dy;

            var direction = _scroller.FinalY < _scroller.CurrY ? -velocity : velocity;

            this.ScrollBy(dx, dy);

            _scroller.ForceFinished(true);
            base.Fling((int)direction);
        }
        else
        {
            ScrollBy(dx, dy);
        }
    }

    public override void Fling(int velocityY)
    {
        velocityY /= (int)(_listView?.ScrollSpeed ?? ScrollSpeed.Normal);

        base.Fling(velocityY);
    }

    protected override void OnScrollChanged(int l, int t, int oldl, int oldt)
    {
        base.OnScrollChanged(l, t, oldl, oldt);

        try
        {
            if (_scroller is not null && (!CanScrollVertically(1) || !CanScrollVertically(-1)) && !_scroller.IsFinished)
            {
                _scroller.AbortAnimation();
            }
        }
        catch { }
    }

    public override void ComputeScroll()
    {
        if (_scroller is null || !_scroller.ComputeScrollOffset() || _scroller.IsFinished) return;

        int oldY = ScrollY;
        int newY = _scroller.CurrY;
        if (oldY != newY)
        {
            ScrollTo(0, newY);
        }
        PostInvalidateOnAnimation();
    }
}