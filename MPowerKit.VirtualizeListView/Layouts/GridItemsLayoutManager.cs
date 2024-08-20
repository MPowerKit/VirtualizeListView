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
            AdapterItem = Adapter!.Items[position],
            Template = Adapter.GetTemplate(position),
            Position = position,
        };

        SetupSpanForItem(LaidOutItems, item);
        SetupRowColumnForItem(LaidOutItems, item);

        item.Margin = GetItemMargin(LaidOutItems, item);
        item.Size = GetEstimatedItemSize(item, AvailableSpace);

        return item;
    }

    protected virtual void SetupSpanForItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position < 0) return;

        item.Span = Adapter!.IsSuplementary(item.Position) ? Span : 1;
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

            item.Margin = GetItemMargin(items, item);
        }
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

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            return item.Row == 0
                ? new(HorizontalItemsSpacing, 0d, 0d, 0d)
                : new(item.Column > 0 ? HorizontalItemsSpacing : 0d, VerticalItemSpacing, 0d, 0d);
        }
        else
        {
            return item.Column == 0
               ? new(0d, VerticalItemSpacing, 0d, 0d)
               : new(HorizontalItemsSpacing, item.Row > 0 ? VerticalItemSpacing : 0d, 0d, 0d);
        }
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

    protected override Size MeasureItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || items.Count == 0 || item.Position == -1) return new();

        var iview = (item.Cell as IView)!;

        var direction = GetItemsFullDirection(items, item).FindAll(i => i.Cell?.WasMeasured is true);

        Size measure;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var availableWidth = GetEstimatedItemSize(item, availableSpace).Width;

            measure = iview.Measure(availableWidth, double.PositiveInfinity);

            double biggestHeight = measure.Height;
            if (direction.Count > 0)
            {
                biggestHeight = Math.Max(direction.Max(i => (i.Cell as IView)!.Measure(i.Bounds.Width, double.PositiveInfinity).Height), measure.Height);

                foreach (var di in direction.FindAll(i => i.Bounds.Height < biggestHeight))
                {
                    di.Size = new(di.Size.Width, biggestHeight);
                }
            }

            item.Size = new(availableWidth, biggestHeight);
        }
        else
        {
            var availableHeight = GetEstimatedItemSize(item, availableSpace).Height;

            measure = iview.Measure(double.PositiveInfinity, availableHeight);

            double biggestWidth = measure.Width;
            if (direction.Count > 0)
            {
                biggestWidth = Math.Max(direction.Max(i => (i.Cell as IView)!.Measure(double.PositiveInfinity, i.Bounds.Height).Width), measure.Width);

                foreach (var di in direction.FindAll(i => i.Bounds.Width < biggestWidth))
                {
                    di.Size = new(biggestWidth, di.Size.Height);
                }
            }

            item.Size = new(biggestWidth, availableHeight);
        }

        return measure;
    }

    protected override void ArrangeItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both)
            || count == 0 || item.Position == -1) return;

        var prevIndex = item.Position - 1;

        VirtualizeListViewItem? prevItem = null;
        Point prevItemRightBottom = new();
        Point prevItemLeftTop = new();
        if (prevIndex >= 0)
        {
            prevItem = items[prevIndex];
            prevItemRightBottom = prevItem.RightBottomWithMargin;
            prevItemLeftTop = prevItem.LeftTopWithMargin;
        }

        var margin = GetItemMargin(items, item);

        item.Margin = margin;

        double top = 0d;
        double left = 0d;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            if (prevItem?.Row == item.Row)
            {
                top = prevItemLeftTop.Y;
                left = prevItemRightBottom.X;
            }
            else top = prevItemRightBottom.Y;
        }
        else
        {
            if (prevItem?.Column == item.Column)
            {
                left = prevItemLeftTop.X;
                top = prevItemRightBottom.Y;
            }
            else left = prevItemRightBottom.X;
        }

        item.LeftTopWithMargin = new(left, top);

        MeasureItem(items, item, availableSpace);
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

        VirtualizeListViewItem? prevItem = null;
        Point prevItemRightBottom = new();
        Point prevItemLeftTop = new();
        if (prevIndex >= 0)
        {
            prevItem = items[prevIndex];
            prevItemRightBottom = prevItem.RightBottomWithMargin;
            prevItemLeftTop = prevItem.LeftTopWithMargin;
        }

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            for (int i = start; i < exclusiveEnd; i++)
            {
                var item = items[i];

                item.LeftTopWithMargin = prevItem?.Row != item.Row
                    ? new(0d, prevItemRightBottom.Y)
                    : new(prevItemRightBottom.X, prevItemLeftTop.Y);

                prevItem = item;
                prevItemRightBottom = prevItem.RightBottomWithMargin;
                prevItemLeftTop = prevItem.LeftTopWithMargin;
            }
        }
        else
        {
            for (int i = start; i < exclusiveEnd; i++)
            {
                var item = items[i];

                item.LeftTopWithMargin = prevItem?.Column != item.Column
                    ? new(prevItemRightBottom.X, 0d)
                    : new(prevItemLeftTop.X, prevItemRightBottom.Y);

                prevItem = item;
                prevItemRightBottom = prevItem.RightBottomWithMargin;
                prevItemLeftTop = prevItem.LeftTopWithMargin;
            }
        }
    }

    protected override bool AdjustScrollIfNeeded(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevBoundsOfItem)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position == -1) return false;

        var direction = GetItemsFullDirection(items, item).FindAll(i => i.Cell?.WasMeasured is true);

        double dx = 0d, dy = 0d;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var biggestBottom = direction.Count > 0 ? direction.Max(i => i.RightBottom.Y) : 0d;

            var bottom = Math.Max(item.RightBottom.Y, biggestBottom);

            dy = bottom - prevBoundsOfItem.Bottom;

            if (dy == 0d) return false;
        }
        else
        {
            var biggestRight = direction.Count > 0 ? direction.Max(i => i.RightBottom.X) : 0d;

            var right = Math.Max(item.RightBottom.X, biggestRight);

            dx = right - prevBoundsOfItem.Right;

            if (dx == 0d) return false;
        }

        ListView!.AdjustScroll(dx, dy);

        return true;
    }

    protected override void AdjustScrollForItemBoundsChange(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevBoundsOfItem)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || item.Position == -1) return;

        var direction = GetItemsFullDirection(items, item).FindAll(i => i.Cell?.WasMeasured is true);

        double dx = 0d, dy = 0d;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var biggestBottom = direction.Count > 0 ? direction.Max(i => i.RightBottom.Y) : 0d;

            var bottom = Math.Max(item.RightBottom.Y, biggestBottom);

            var top = item.LeftTop.Y + ListView!.Padding.Top;
            dy = bottom - prevBoundsOfItem.Bottom;

            var scrollY = ListView.ScrollY;

            if (dy == 0d || (top < (scrollY + ListView.Height) && top > scrollY)) return;
        }
        else
        {
            var biggestRight = direction.Count > 0 ? direction.Max(i => i.RightBottom.X) : 0d;

            var right = Math.Max(item.RightBottom.X, biggestRight);

            var left = item.LeftTop.X + ListView!.Padding.Left;
            dx = right - prevBoundsOfItem.Right;

            var scrollX = ListView.ScrollX;

            if (dx == 0d || (left < (scrollX + ListView.Width) && left > scrollX)) return;
        }

        ListView.AdjustScroll(dx, dy);
    }
}