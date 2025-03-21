namespace MPowerKit.VirtualizeListView;

public interface IItemsLayout
{
    int InitialCachePoolSize { get; set; }
}

public class ItemsLayout : IItemsLayout
{
    public int InitialCachePoolSize { get; set; } = 2;
}

public class LinearLayout : ItemsLayout
{
    public double ItemSpacing { get; set; }
}

public class GridLayout : ItemsLayout
{
    public int Span { get; set; } = 1;
    public double VerticalItemSpacing { get; set; }
    public double HorizontalItemSpacing { get; set; }
}