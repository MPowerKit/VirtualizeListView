namespace MPowerKit.VirtualizeListView;

public class GridItemsLayoutManager : VirtualizeItemsLayoutManger
{
    #region Span
    public int Span
    {
        get => (int)GetValue(SpanProperty);
        set => SetValue(SpanProperty, value);
    }

    public static readonly BindableProperty SpanProperty =
        BindableProperty.Create(
            nameof(Span),
            typeof(int),
            typeof(LinearItemsLayoutManager),
            1);
    #endregion

    #region VerticalItemSpacing
    public double VerticalItemSpacing
    {
        get => (double)GetValue(VerticalItemSpacingProperty);
        set => SetValue(VerticalItemSpacingProperty, value);
    }

    public static readonly BindableProperty VerticalItemSpacingProperty =
        BindableProperty.Create(
            nameof(VerticalItemSpacing),
            typeof(double),
            typeof(LinearItemsLayoutManager));
    #endregion

    #region HorizontalItemsSpacing
    public double HorizontalItemsSpacing
    {
        get => (double)GetValue(HorizontalItemsSpacingProperty);
        set => SetValue(HorizontalItemsSpacingProperty, value);
    }

    public static readonly BindableProperty HorizontalItemsSpacingProperty =
        BindableProperty.Create(
            nameof(HorizontalItemsSpacing),
            typeof(double),
            typeof(LinearItemsLayoutManager));
    #endregion

    protected override VirtualizeListViewItem CreateItemForPosition(int position)
    {
        var item = new VirtualizeListViewItem(this)
        {
            AdapterItem = Control!.Adapter.Items[position],
            Template = Control.Adapter.GetTemplate(position),
            Position = position,
        };

        SetupSpanForItem(LaidOutItems, item);
        SetupRowColumnForItem(LaidOutItems, item);

        var estimatedSize = GetEstimatedItemSize(item, AvailableSpace);
        item.CellBounds = item.Bounds = new(0d, 0d, estimatedSize.Width, estimatedSize.Height);

        return item;
    }

    protected virtual void SetupSpanForItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position < 0) return;

        item.Span = Control!.Adapter.IsSuplementary(item.Position) ? Span : 1;
    }

    protected virtual void SetupRowColumnForItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position < 0) return;

        var prevIndex = item.Position - 1;
        var prevItem = prevIndex == -1 ? null : items[prevIndex];

        if (prevItem is null)
        {
            item.Column = 0;
            item.Row = 0;
            return;
        }

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var newColumn = prevItem.Column + prevItem.Span;
            if (newColumn + item.Span > Span)
            {
                item.Column = 0;
                item.Row = prevItem.Row + 1;
                return;
            }

            item.Column = newColumn;
            item.Row = prevItem.Row;
        }
        else
        {
            var newRow = prevItem.Row + prevItem.Span;
            if (newRow + item.Span > Span)
            {
                item.Row = 0;
                item.Column = prevItem.Column + 1;
                return;
            }

            item.Row = newRow;
            item.Column = prevItem.Column;
        }
    }

    protected override void RepositionItemsFromIndex(IReadOnlyList<VirtualizeListViewItem> items, int index)
    {
        var count = items.Count;

        if (count == 0 || index < 0 || index >= count) return;

        for (int i = index; i < count; i++)
        {
            var item = LaidOutItems[i];
            item.Position = i;
            SetupRowColumnForItem(items, item);
        }
    }

    protected override Size MeasureItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || items.Count == 0 || item.Position == -1) return new();

        var iview = (item.Cell as IView)!;

        var direction = GetItemsFullDirection(items, item).FindAll(i => i.Cell?.WasMeasured is true);

        Size measure, size;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var availableWidth = GetEstimatedItemSize(item, availableSpace).Width;

            measure = iview.Measure(availableWidth, double.PositiveInfinity);

            double biggestHeight = measure.Height;
            if (direction.Count > 0)
            {
                biggestHeight = Math.Max(direction.Max(i => (i.Cell as IView)!.Measure(i.CellBounds.Width, double.PositiveInfinity).Height), measure.Height);

                foreach (var di in direction.FindAll(i => i.CellBounds.Height < biggestHeight))
                {
                    di.CellBounds = new(di.CellBounds.X, di.CellBounds.Y, di.CellBounds.Width, biggestHeight);
                    di.Bounds = new(di.Bounds.X, di.Bounds.Y, di.Bounds.Width, biggestHeight + di.Margin.VerticalThickness);
                }
            }

            size = new(availableWidth, biggestHeight);
        }
        else
        {
            var availableHeight = GetEstimatedItemSize(item, availableSpace).Height;

            measure = iview.Measure(double.PositiveInfinity, availableHeight);

            double biggestWidth = measure.Width;
            if (direction.Count > 0)
            {
                biggestWidth = Math.Max(direction.Max(i => (i.Cell as IView)!.Measure(double.PositiveInfinity, i.CellBounds.Height).Width), measure.Width);

                foreach (var di in direction.FindAll(i => i.CellBounds.Width < biggestWidth))
                {
                    di.CellBounds = new(di.CellBounds.X, di.CellBounds.Y, biggestWidth, di.CellBounds.Height);
                    di.Bounds = new(di.Bounds.X, di.Bounds.Y, biggestWidth + di.Margin.HorizontalThickness, di.Bounds.Height);
                }
            }

            size = new(biggestWidth, availableHeight);
        }

        item.CellBounds = new(item.CellBounds.X, item.CellBounds.Y, size.Width, size.Height);
        item.Bounds = new(item.Bounds.X, item.Bounds.Y, size.Width + item.Margin.HorizontalThickness, size.Height + item.Margin.VerticalThickness);

        return measure;
    }

    protected override void ArrangeItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both)
            || count == 0 || item.Position == -1) return;

        var prevIndex = item.Position - 1;
        var prevItem = prevIndex == -1 ? null : items[prevIndex];
        var prevItemBounds = prevItem is null ? new() : items[prevIndex].Bounds;

        var margin = GetItemMargin(items, item);

        item.Margin = margin;

        double top = 0d;
        double left = 0d;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            if (prevItem?.Row == item.Row)
            {
                top = prevItemBounds.Top;
                left = prevItemBounds.Right;
            }
            else top = prevItemBounds.Bottom;
        }
        else
        {
            if (prevItem?.Column == item.Column)
            {
                top = prevItemBounds.Bottom;
                left = prevItemBounds.Left;
            }
            else left = prevItemBounds.Right;
        }

        item.CellBounds = new(left + margin.Left, top + margin.Top, 0d, 0d);
        item.Bounds = new(left, top, 0d, 0d);

        MeasureItem(items, item, availableSpace);
    }

    protected virtual List<VirtualizeListViewItem> GetItemsFullDirection(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both)
            || count == 0 || item.Position == -1) return [];

        List<VirtualizeListViewItem> direction = new(Span - 1);

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var index = item.Position - 1;
            while (index >= 0)
            {
                var prevItem = items[index];
                if (prevItem.Row != item.Row) break;

                direction.Insert(0, prevItem);

                index--;
            }

            index = item.Position + 1;
            while (index < count)
            {
                var nextItem = items[index];
                if (nextItem.Row != item.Row) break;

                direction.Add(nextItem);

                index++;
            }
        }
        else
        {
            var index = item.Position - 1;
            while (index >= 0)
            {
                var prevItem = items[index];
                if (prevItem.Column != item.Column) break;

                direction.Insert(0, prevItem);

                index--;
            }

            index = item.Position + 1;
            while (index < count)
            {
                var nextItem = items[index];
                if (nextItem.Column != item.Column) break;

                direction.Add(nextItem);

                index++;
            }
        }

        return direction;
    }

    protected override Size GetEstimatedItemSize(VirtualizeListViewItem item, Size availableSpace)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position < 0) return new();

        return IsOrientation(ScrollOrientation.Vertical)
            ? new(item.Span * ((availableSpace.Width - (HorizontalItemsSpacing * (Span - 1))) / Span) + HorizontalItemsSpacing * (item.Span - 1), EstimatedSize)
            : new(EstimatedSize, item.Span * ((availableSpace.Height - (VerticalItemSpacing * (Span - 1))) / Span) + VerticalItemSpacing * (item.Span - 1));
    }

    protected override Thickness GetItemMargin(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || item.Position <= 0) return new();

        Thickness margin;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            margin = item.Row == 0
                ? new(HorizontalItemsSpacing, 0d, 0d, 0d)
                : new(item.Column > 0 ? HorizontalItemsSpacing : 0d, VerticalItemSpacing, 0d, 0d);
            return margin;
        }
        else
        {
            margin = item.Column == 0
               ? new(0d, VerticalItemSpacing, 0d, 0d)
               : new(HorizontalItemsSpacing, item.Row > 0 ? VerticalItemSpacing : 0d, 0d, 0d);
        }

        return margin;
    }

    protected override void AdjustScrollForItemBoundsChange(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevCellBounds)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || item.Position == -1) return;

        var direction = GetItemsFullDirection(items, item).FindAll(i => i.Cell?.WasMeasured is true);

        double dx = 0d, dy = 0d;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            double biggestBottom = 0d;
            if (direction.Count > 0)
            {
                biggestBottom = direction.Max(i => i.Bounds.Bottom);
            }

            var bottom = Math.Max(item.Bounds.Bottom, biggestBottom);
            var top = item.Bounds.Y + Control!.Padding.Top;
            dy = bottom - prevCellBounds.Bottom;

            var scrollY = Control.ScrollY;

            if (dy == 0d || (top < (scrollY + Control.Height) && top > scrollY)) return;
        }
        else
        {
            double biggestRight = 0d;
            if (direction.Count > 0)
            {
                biggestRight = direction.Max(i => i.Bounds.Right);
            }

            var right = Math.Max(item.Bounds.Right, biggestRight);
            var left = item.Bounds.X + Control!.Padding.Left;
            dx = right - prevCellBounds.Right;

            var scrollX = Control.ScrollX;

            if (dx == 0d || (left < (scrollX + Control.Width) && left > scrollX)) return;
        }

        Control.AdjustScroll(dx, dy);
    }

    protected override bool AdjustScrollIfNeeded(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevCellBounds)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position == -1) return false;

        var direction = GetItemsFullDirection(items, item).FindAll(i => i.Cell?.WasMeasured is true);

        bool needs;

        double dx = 0d, dy = 0d;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            double biggestBottom = 0d;
            if (direction.Count > 0)
            {
                biggestBottom = direction.Max(i => i.Bounds.Bottom);
            }

            var bottom = Math.Max(item.Bounds.Bottom, biggestBottom);

            dy = bottom - prevCellBounds.Bottom;

            needs = dy != 0d;
            if (!needs) return needs;
        }
        else
        {
            double biggestRight = 0d;
            if (direction.Count > 0)
            {
                biggestRight = direction.Max(i => i.Bounds.Right);
            }

            var right = Math.Max(item.Bounds.Right, biggestRight);

            dx = right - prevCellBounds.Right;

            needs = dx != 0d;
            if (!needs) return needs;
        }

        Control!.AdjustScroll(dx, dy);

        return needs;
    }

    protected override void ShiftItemsChunk(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        ShiftItemsConsecutively(items, start, exclusiveEnd);
    }

    protected override void ShiftItemsConsecutively(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both) || start < 0
            || start >= count || exclusiveEnd <= 0 || exclusiveEnd > count) return;

        var prevIndex = start - 1;
        var prevItem = prevIndex == -1 ? null : items[prevIndex];
        var prevBounds = prevItem is null ? new() : items[prevIndex].Bounds;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            for (int i = start; i < exclusiveEnd; i++)
            {
                var item = items[i];

                double dx, dy;

                if (prevItem?.Row != item.Row)
                {
                    dx = -item.Bounds.X;
                    dy = prevBounds.Bottom - item.Bounds.Y;
                }
                else
                {
                    dx = prevBounds.Right - item.Bounds.X;
                    dy = prevBounds.Top - item.Bounds.Y;
                }

                if (dy == 0d && dx == 0)
                {
                    prevBounds = item.Bounds;
                    prevItem = item;
                    continue;
                }

                item.CellBounds = new(item.CellBounds.X + dx, item.CellBounds.Y + dy, item.CellBounds.Width, item.CellBounds.Height);
                item.Bounds = new(item.Bounds.X + dx, item.Bounds.Y + dy, item.Bounds.Width, item.Bounds.Height);

                prevBounds = item.Bounds;
                prevItem = item;
            }
        }
        else
        {
            for (int i = start; i < exclusiveEnd; i++)
            {
                var item = items[i];

                double dx, dy;

                if (prevItem?.Column != item.Column)
                {
                    dx = prevBounds.Right - item.Bounds.X;
                    dy = -item.Bounds.Y;
                }
                else
                {
                    dx = prevBounds.Right - item.Bounds.X;
                    dy = prevBounds.Top - item.Bounds.Y;
                }

                if (dy == 0d && dx == 0)
                {
                    prevBounds = item.Bounds;
                    prevItem = item;
                    continue;
                }

                item.CellBounds = new(item.CellBounds.X + dx, item.CellBounds.Y + dy, item.CellBounds.Width, item.CellBounds.Height);
                item.Bounds = new(item.Bounds.X + dx, item.Bounds.Y + dy, item.Bounds.Width, item.Bounds.Height);

                prevBounds = item.Bounds;
                prevItem = item;
            }
        }
    }
}