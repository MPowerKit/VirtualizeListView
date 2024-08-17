
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

        var estimatedSize = GetEstimatedItemSize(item);
        item.CellBounds = item.Bounds = new(0d, 0d, estimatedSize.Width, estimatedSize.Height);

        return item;
    }

    protected virtual void SetupSpanForItem(IReadOnlyCollection<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position < 0) return;

        var isSuplementary = Control!.Adapter.IsSuplementary(item.Position);

        if (isSuplementary)
        {
            item.Span = Span;
        }
    }

    protected virtual void SetupRowColumnForItem(IReadOnlyCollection<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position < 0) return;

        var prevItem = items.LastOrDefault() ?? new VirtualizeListViewItem(this);

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var lastColumnIndex = (prevItem.Column + 1) * prevItem.Span;
            if (lastColumnIndex + item.Span > Span)
            {
                item.Column = 0;
                item.Row = prevItem.Row + 1;
                return;
            }

            item.Column = lastColumnIndex;
            item.Row = prevItem.Row;
        }
        else
        {
            var lastRowIndex = (prevItem.Row + 1) * prevItem.Span;
            if (lastRowIndex + item.Span > Span)
            {
                item.Row = 0;
                item.Column = prevItem.Column + 1;
                return;
            }

            item.Row = lastRowIndex;
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

        var direction = GetItemsFullDirection(items, item);

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var availableWidth = item.Span * ((availableSpace.Width - (HorizontalItemsSpacing * (Span - 1))) / Span) + HorizontalItemsSpacing * (item.Span - 1);

            var newAvailableSpace = new Size(availableWidth, availableSpace.Height);

            var request = MeasureItem(items, item, newAvailableSpace);

            var top = prevItem?.Row == item.Row ? prevItemBounds.Top : prevItemBounds.Bottom;

            if (direction.Count == 0 || (direction.Count == 1 && direction[0] == item))
            {
                if (item.Cell!.WidthRequest != newAvailableSpace.Width
                    || item.Cell.HeightRequest != AutoSize)
                {
                    item.Cell!.WidthRequest = newAvailableSpace.Width;
                    item.Cell.HeightRequest = AutoSize;
                }

                item.CellBounds = new Rect(margin.Left, top + margin.Top, newAvailableSpace.Width, request.Height);
                item.Bounds = new Rect(0d, top, newAvailableSpace.Width, request.Height + margin.VerticalThickness);

                return;
            }

            if (request.Height > direction[0].CellBounds.Height)
            {
                //foreach (var rowItem in direction.FindAll())
                //{

                //}
            }

            var biggestItem = direction.MaxBy(static x => x.CellBounds.Height);



        }
        else
        {
            var right = prevItemBounds.Right;

            var newAvailableSpace = new Size(availableSpace.Width, availableSpace.Height - margin.VerticalThickness);

            var request = MeasureItem(items, item, newAvailableSpace);

            if (item.Cell!.WidthRequest != AutoSize
                || item.Cell.HeightRequest != newAvailableSpace.Height)
            {
                item.Cell!.HeightRequest = newAvailableSpace.Height;
                item.Cell.WidthRequest = AutoSize;
            }

            item.CellBounds = new Rect(right + margin.Left, margin.Top, request.Width, newAvailableSpace.Height);
            item.Bounds = new Rect(right, 0d, request.Width + margin.HorizontalThickness, newAvailableSpace.Height);
        }
    }

    protected virtual List<VirtualizeListViewItem> GetItemsFullDirection(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both)
            || count == 0 || item.Position == -1) return [];

        List<VirtualizeListViewItem> direction = new(Span);

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var index = item.Position - 1;
            while (index > 0)
            {
                var prevItem = items[index];
                if (prevItem.Row != item.Row) break;

                direction.Insert(0, prevItem);

                index--;
            }

            index = item.Position + 1;
            while (index >= count)
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
            while (index > 0)
            {
                var prevItem = items[index];
                if (prevItem.Column != item.Column) break;

                direction.Insert(0, prevItem);

                index--;
            }

            index = item.Position + 1;
            while (index >= count)
            {
                var nextItem = items[index];
                if (nextItem.Column != item.Column) break;

                direction.Add(nextItem);

                index++;
            }
        }

        return direction;
    }

    protected override Size GetEstimatedItemSize(VirtualizeListViewItem item)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position < 0) return new();

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            return new Size(AvailableSpace.Width / Span * item.Span, 200d);
        }
        else
        {
            return new Size(200d, AvailableSpace.Height / Span * item.Span);
        }
    }

    protected override Thickness GetItemMargin(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || item.Position <= 0) return base.GetItemMargin(items, item);

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            return new(item.Column > 0 ? HorizontalItemsSpacing : 0d, VerticalItemSpacing, 0d, 0d);
        }
        else
        {
            return new(HorizontalItemsSpacing, item.Row > 0 ? VerticalItemSpacing : 0d, 0d, 0d);
        }
    }

    protected override Size MeasureItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || items.Count == 0 || item.Position == -1) return new Size();

        var iview = (item.Cell as IView)!;

        return IsOrientation(ScrollOrientation.Vertical)
            ? iview.Measure(availableSpace.Width, double.PositiveInfinity)
            : iview.Measure(double.PositiveInfinity, availableSpace.Height);
    }

    protected override void AdjustScrollForItemBoundsChange(VirtualizeListViewItem item, Rect prevBounds)
    {

    }

    protected override bool AdjustScrollIfNeeded(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem prevItem, Rect prevCellBounds)
    {
        if (IsOrientation(ScrollOrientation.Both) || prevItem.Position == -1) return false;

        bool needs;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var dy = prevItem.CellBounds.Bottom - prevCellBounds.Bottom;

            needs = dy != 0d;
            if (!needs) return needs;

            Control!.AdjustScroll(0d, dy);
        }
        else
        {
            var dx = prevItem.CellBounds.Right - prevCellBounds.Right;

            needs = dx != 0d;
            if (!needs) return needs;

            Control!.AdjustScroll(dx, 0d);
        }

        return needs;
    }

    protected override void ShiftAllItems(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        ShiftItemsConsecutively(items, start, exclusiveEnd);
    }

    protected override void ShiftItemsConsecutively(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        //var count = items.Count;

        //if (IsOrientation(ScrollOrientation.Both) || start < 0
        //    || start >= count || exclusiveEnd <= 0 || exclusiveEnd > count) return;

        //var prevIndex = start - 1;
        //var prevBounds = prevIndex == -1 ? new() : items[prevIndex].Bounds;

        //if (IsOrientation(ScrollOrientation.Vertical))
        //{
        //    for (int i = start; i < exclusiveEnd; i++)
        //    {
        //        var item = items[i];

        //        var dy = prevBounds.Bottom - item.Bounds.Y;
        //        if (dy == 0d)
        //        {
        //            prevBounds = item.Bounds;
        //            continue;
        //        }

        //        item.CellBounds = new Rect(item.CellBounds.X, item.CellBounds.Y + dy, item.CellBounds.Width, item.CellBounds.Height);
        //        item.Bounds = new Rect(item.Bounds.X, item.Bounds.Y + dy, item.Bounds.Width, item.Bounds.Height);

        //        prevBounds = item.Bounds;
        //    }
        //}
        //else
        //{
        //    for (int i = start; i < exclusiveEnd; i++)
        //    {
        //        var item = items[i];

        //        var dx = prevBounds.Right - item.Bounds.X;
        //        if (dx == 0d)
        //        {
        //            prevBounds = item.Bounds;
        //            continue;
        //        }

        //        item.CellBounds = new Rect(item.CellBounds.X + dx, item.CellBounds.Y, item.CellBounds.Width, item.CellBounds.Height);
        //        item.Bounds = new Rect(item.Bounds.X + dx, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height);
        //    }
        //}
    }

    protected override Size GetDesiredLayoutSize(double widthConstraint, double heightConstraint)
    {
        throw new NotImplementedException();
    }
}