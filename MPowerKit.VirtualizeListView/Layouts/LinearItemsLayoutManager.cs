namespace MPowerKit.VirtualizeListView;

public class LinearItemsLayoutManager : VirtualizeItemsLayoutManger
{
    #region ItemSpacing
    public double ItemSpacing
    {
        get => (double)GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    public static readonly BindableProperty ItemSpacingProperty =
        BindableProperty.Create(
            nameof(ItemSpacing),
            typeof(double),
            typeof(LinearItemsLayoutManager));
    #endregion

    protected override Size GetEstimatedItemSize(VirtualizeListViewItem item, Size availableSpace)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position < 0) return new();

        return IsOrientation(ScrollOrientation.Vertical)
            ? new(availableSpace.Width, EstimatedSize)
            : new(EstimatedSize, availableSpace.Height);
    }

    protected override Size MeasureItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || items.Count == 0 || item.Position == -1) return new();

        var iview = (item.Cell as IView)!;

        Size measure, size, sizeWithSpacing;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            measure = iview.Measure(GetEstimatedItemSize(item, availableSpace).Width, double.PositiveInfinity);

            size = new(availableSpace.Width, measure.Height);
            sizeWithSpacing = new(size.Width, size.Height + item.Margin.VerticalThickness);
        }
        else
        {
            measure = iview.Measure(double.PositiveInfinity, GetEstimatedItemSize(item, availableSpace).Height);

            size = new(measure.Width, availableSpace.Height);
            sizeWithSpacing = new(size.Width + item.Margin.HorizontalThickness, size.Height);
        }

        item.CellBounds = new(item.CellBounds.X, item.CellBounds.Y, size.Width, size.Height);
        item.Bounds = new(item.Bounds.X, item.Bounds.Y, sizeWithSpacing.Width, sizeWithSpacing.Height);

        return measure;
    }

    protected override void ArrangeItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both)
            || count == 0 || item.Position == -1) return;

        var prevIndex = item.Position - 1;
        var prevItemBounds = prevIndex == -1 ? new() : items[prevIndex].Bounds;

        var margin = GetItemMargin(items, item);

        item.Margin = margin;

        double top = 0d;
        double left = 0d;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            top = prevItemBounds.Bottom;
        }
        else
        {
            left = prevItemBounds.Right;
        }

        item.CellBounds = new(left + margin.Left, top + margin.Top, 0d, 0d);
        item.Bounds = new(left, top, 0d, 0d);

        MeasureItem(items, item, availableSpace);
    }

    protected override void ShiftItemsChunk(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both) || start < 0
            || start >= count || exclusiveEnd <= 0 || exclusiveEnd > count) return;

        var item = items[start];
        var prevIndex = start - 1;
        var prevBounds = prevIndex == -1 ? new() : items[prevIndex].Bounds;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var dy = prevBounds.Bottom - item.Bounds.Y;
            if (dy == 0d) return;

            for (int i = start; i < exclusiveEnd; i++)
            {
                var currentItem = items[i];

                currentItem.CellBounds = new(currentItem.CellBounds.X, currentItem.CellBounds.Y + dy, currentItem.CellBounds.Width, currentItem.CellBounds.Height);
                currentItem.Bounds = new(currentItem.Bounds.X, currentItem.Bounds.Y + dy, currentItem.Bounds.Width, currentItem.Bounds.Height);
            }
        }
        else
        {
            var dx = prevBounds.Right - item.Bounds.X;
            if (dx == 0d) return;

            for (int i = start; i < exclusiveEnd; i++)
            {
                var currentItem = items[i];

                currentItem.CellBounds = new(currentItem.CellBounds.X + dx, currentItem.CellBounds.Y, currentItem.CellBounds.Width, currentItem.CellBounds.Height);
                currentItem.Bounds = new(currentItem.Bounds.X + dx, currentItem.Bounds.Y, currentItem.Bounds.Width, currentItem.Bounds.Height);
            }
        }
    }

    protected override void ShiftItemsConsecutively(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both) || start < 0
            || start >= count || exclusiveEnd <= 0 || exclusiveEnd > count) return;

        var prevIndex = start - 1;
        var prevBounds = prevIndex == -1 ? new() : items[prevIndex].Bounds;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            for (int i = start; i < exclusiveEnd; i++)
            {
                var item = items[i];

                var dy = prevBounds.Bottom - item.Bounds.Y;
                if (dy == 0d)
                {
                    prevBounds = item.Bounds;
                    continue;
                }

                item.CellBounds = new(item.CellBounds.X, item.CellBounds.Y + dy, item.CellBounds.Width, item.CellBounds.Height);
                item.Bounds = new(item.Bounds.X, item.Bounds.Y + dy, item.Bounds.Width, item.Bounds.Height);

                prevBounds = item.Bounds;
            }
        }
        else
        {
            for (int i = start; i < exclusiveEnd; i++)
            {
                var item = items[i];

                var dx = prevBounds.Right - item.Bounds.X;
                if (dx == 0d)
                {
                    prevBounds = item.Bounds;
                    continue;
                }

                item.CellBounds = new(item.CellBounds.X + dx, item.CellBounds.Y, item.CellBounds.Width, item.CellBounds.Height);
                item.Bounds = new(item.Bounds.X + dx, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height);
            }
        }
    }

    protected override bool AdjustScrollIfNeeded(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevCellBounds)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position == -1) return false;

        bool needs;

        double dx = 0d, dy = 0d;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            dy = item.CellBounds.Bottom - prevCellBounds.Bottom;

            needs = dy != 0d;
            if (!needs) return needs;
        }
        else
        {
            dx = item.CellBounds.Right - prevCellBounds.Right;

            needs = dx != 0d;
            if (!needs) return needs;
        }

        Control!.AdjustScroll(dx, dy);

        return needs;
    }

    protected override void AdjustScrollForItemBoundsChange(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevCellBounds)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || item.Position == -1) return;

        double dx = 0d, dy = 0d;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var bottom = item.Bounds.Bottom;
            var top = item.Bounds.Y + Control!.Padding.Top;
            dy = bottom - prevCellBounds.Bottom;

            var scrollY = Control.ScrollY;

            if (dy == 0d || (top < (scrollY + Control.Height) && top > scrollY)) return;
        }
        else
        {
            var right = item.Bounds.Right;
            var left = item.Bounds.X + Control!.Padding.Left;
            dx = right - prevCellBounds.Right;

            var scrollX = Control.ScrollX;

            if (dx == 0d || (left < (scrollX + Control.Width) && left > scrollX)) return;
        }

        Control.AdjustScroll(dx, dy);
    }

    protected override Thickness GetItemMargin(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || item.Position <= 0) return new();

        return IsOrientation(ScrollOrientation.Vertical)
            ? new(0d, ItemSpacing, 0d, 0d)
            : new(ItemSpacing, 0d, 0d, 0d);
    }
}