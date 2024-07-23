namespace MPowerKit.VirtualizeListView;

public static class BuilderExtensions
{
    public static MauiAppBuilder UseMPowerKitListView(this MauiAppBuilder builder)
    {
        builder.ConfigureMauiHandlers(handlers =>
        {
#if !WINDOWS
            handlers.AddHandler<VirtualizeListView, VirtualizeListViewHandler>();
            handlers.AddHandler<FixedRefreshView, FixedRefreshViewRenderer>();
#endif
        });

        return builder;
    }
}