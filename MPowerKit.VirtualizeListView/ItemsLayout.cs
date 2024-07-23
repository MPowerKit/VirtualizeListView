namespace MPowerKit.VirtualizeListView;

public interface IItemsLayout
{
    int InitialCachePoolSize { get; set; }
}

public class ItemsLayout : IItemsLayout
{
    public int InitialCachePoolSize { get; set; } = 4;
}

public class LinearLayout : ItemsLayout
{
    public double ItemSpacing { get; set; }
}