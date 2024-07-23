using Microsoft.Maui.Handlers;

using UIKit;

namespace MPowerKit.VirtualizeListView;

public partial class VirtualizeListViewHandler : ScrollViewHandler
{
    public static void MapScrollSpeed(VirtualizeListViewHandler handler, VirtualizeListView virtualizeListView)
    {
        handler.PlatformView.DecelerationRate =
            virtualizeListView.ScrollSpeed is ScrollSpeed.Normal
                ? UIScrollView.DecelerationRateNormal
                : UIScrollView.DecelerationRateFast;
    }
}