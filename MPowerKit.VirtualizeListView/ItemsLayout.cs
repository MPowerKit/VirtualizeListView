namespace MPowerKit.VirtualizeListView;

public interface IItemsLayout
{

}

public class LinearLayout : IItemsLayout
{
    public double ItemSpacing { get; set; }
}