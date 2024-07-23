using System.Runtime.InteropServices;

using Microsoft.Maui.Layouts;

namespace MPowerKit.VirtualizeListView;

public class ItemsLayoutManager : LayoutManager
{
    protected VirtualizeItemsLayoutManger Layout { get; }

    public ItemsLayoutManager(VirtualizeItemsLayoutManger virtualizeItemsLayout) : base(virtualizeItemsLayout)
    {
        Layout = virtualizeItemsLayout;
    }

    public override Size Measure(double widthConstraint, double heightConstraint)
    {
        Size newSize = new();
        if (Layout.ReadOnlyLaidOutItems.Count > 0)
        {
            var lastItem = Layout.ReadOnlyLaidOutItems[^1];

            newSize = new(lastItem.Bounds.Right, lastItem.Bounds.Bottom);
        }

        var items = CollectionsMarshal.AsSpan((Layout as IBindableLayout).Children as List<IView>);
        var length = items.Length;

        for (int n = 0; n < length; n++)
        {
            var child = items[n];
            var view = child as CellHolder;

            if (view.IsCached || !view.Item.IsAttached) continue;

            // this triggers item size change when needed
            var measure = child.Measure(double.PositiveInfinity, double.PositiveInfinity);
        }

        return newSize;
    }

    public override Size ArrangeChildren(Rect bounds)
    {
        var padding = Layout.Padding;

        var availableWidth = bounds.Width - padding.HorizontalThickness;
        var availableHeight = bounds.Height - padding.VerticalThickness;

        var items = CollectionsMarshal.AsSpan((Layout as IBindableLayout).Children as List<IView>);
        var length = items.Length;

        for (int n = 0; n < length; n++)
        {
            var child = items[n];
            var view = child as CellHolder;

#if !MACIOS
            if (view.IsCached || !view.Item.IsAttached) continue;
#endif

            var (x, y) =
#if ANDROID
                (view.Item.CellBounds.X, view.Item.CellBounds.Y);
#else
                (0d, 0d);
#endif

            var newBounds = new Rect(x, y, view.DesiredSize.Width, view.DesiredSize.Height);

#if MACIOS
            if (view.Bounds == newBounds) continue;
#endif

            child.Arrange(newBounds);
        }

        return new(availableWidth, availableHeight);
    }
}