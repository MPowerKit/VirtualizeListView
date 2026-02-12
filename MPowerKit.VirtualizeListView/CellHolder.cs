namespace MPowerKit.VirtualizeListView;

public class CellHolder : Grid
{
    public const double CachedItemsCoords = -10000000d;

    private VirtualizeListViewItem? _item;

    public void Cache()
    {
        this.TranslationX = CachedItemsCoords;
        this.TranslationY = CachedItemsCoords;
    }

    public void GetFromCache()
    {
        this.TranslationX = 0d;
        this.TranslationY = 0d;
    }

    public override string ToString()
    {
        return $"Item={Item}, {base.ToString()}";
    }

    public VirtualizeListViewItem? Item
    {
        get => _item;
        set
        {
            _item = value;
            if (value is null)
            {
                Cache();
                return;
            }

            ZIndex = value.Position;

            //if (value.State is not ItemState.Idle) return;
            //if (!value.AnyOpacityAnimation)
            //{
            this.Opacity = 1d;
            //}
            //if (!value.AnyTranslationAnimation && !value.PreTranslationAnimation)
            //{
            if (value.State is not
                (ItemState.ShouldBeShiftedOnInsert or ItemState.ShouldBeShiftedOnRemove))
            {
                GetFromCache();
            }
            //}
        }
    }
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