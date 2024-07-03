namespace MPowerKit.VirtualizeListView;

public class CellHolder : Grid
{
    public VirtualizeListViewItem? Item { get; set; }
    public bool WasRecycled { get; set; }

    public CellHolder()
    {
        VerticalOptions = HorizontalOptions = LayoutOptions.Start;

        this.SizeChanged += CellHolder_SizeChanged;
    }

    private void CellHolder_SizeChanged(object? sender, EventArgs e)
    {
        Item?.OnCellSizeChanged();
    }
}