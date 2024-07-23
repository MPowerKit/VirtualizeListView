using Microsoft.Maui.Handlers;

namespace MPowerKit.VirtualizeListView;

public partial class VirtualizeListViewHandler : ScrollViewHandler
{
    public static IPropertyMapper<IScrollView, IScrollViewHandler> VirtualizeListViewMapper = new PropertyMapper<VirtualizeListView, VirtualizeListViewHandler>(Mapper)
    {
#if IOS || MACCATALYST
        [nameof(VirtualizeListView.ScrollSpeed)] = MapScrollSpeed,
#endif
    };

    public VirtualizeListViewHandler() : this(VirtualizeListViewMapper, CommandMapper)
    {

    }

    public VirtualizeListViewHandler(IPropertyMapper? mapper)
        : this(mapper ?? VirtualizeListViewMapper, CommandMapper)
    {
    }

    public VirtualizeListViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper)
        : base(mapper ?? VirtualizeListViewMapper, commandMapper ?? CommandMapper)
    {

    }
}