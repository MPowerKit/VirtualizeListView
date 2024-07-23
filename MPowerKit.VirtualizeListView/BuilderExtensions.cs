namespace MPowerKit.VirtualizeListView;

public static class BuilderExtensions
{
    public static MauiAppBuilder UseMPowerKitListView(this MauiAppBuilder builder)
    {
        builder.ConfigureMauiHandlers(handlers =>
        {
#if ANDROID
            handlers.AddHandler<VirtualizeListView, VirtualizeListViewHandler>();
#endif

#if !WINDOWS
            handlers.AddHandler<FixedRefreshView, FixedRefreshViewRenderer>();
#endif
        });

        return builder;
    }
}