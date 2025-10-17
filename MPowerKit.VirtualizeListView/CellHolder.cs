namespace MPowerKit.VirtualizeListView;

public class CellHolder : Grid
{
    public VirtualizeListViewItem? Item { get; set; }
    //public bool IsCached => Item is null;
    //public bool WasArranged { get; protected set; }
    //public bool WasMeasured { get; protected set; }
    public bool Attached { get; set; }

    public View? Content
    {
        get => this.ElementAtOrDefault(0) as View;
        set
        {
            if (value == Content) return;

            this.ClearItems();

            if (value is null) return;

            this.Add(value);
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        Item?.OnCellSizeChanged();
    }
}