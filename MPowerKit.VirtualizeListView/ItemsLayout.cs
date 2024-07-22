namespace MPowerKit.VirtualizeListView;

public interface IItemsLayout
{
    int PoolSize { get; set; }
}

public class ItemsLayout : IItemsLayout
{
    public int PoolSize { get; set; } = 4;
}

public class LinearLayout : ItemsLayout
{
    public double ItemSpacing { get; set; }
}