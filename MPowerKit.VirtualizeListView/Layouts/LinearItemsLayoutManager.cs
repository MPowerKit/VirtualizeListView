using System.Linq;

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
            ? new(availableSpace.Width, EstimatedItemSize)
            : new(EstimatedItemSize, availableSpace.Height);
    }

    protected override Thickness GetItemMargin(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || item.Position <= 0) return new();

        return IsOrientation(ScrollOrientation.Vertical)
            ? new(0d, ItemSpacing, 0d, 0d)
            : new(ItemSpacing, 0d, 0d, 0d);
    }

    public override VirtualizeListViewItem CreateItemForPosition(int position)
    {
        var item = new VirtualizeListViewItem(this)
        {
            AdapterItem = Adapter!.Items[position],
            Template = Adapter.GetTemplate(position),
            Position = position
        };

        item.Margin = GetItemMargin(LaidOutItems, item);
        item.Size = GetEstimatedItemSize(item, AvailableSpace);

        return item;
    }

    protected override void RepositionItemsFromIndex(IReadOnlyList<VirtualizeListViewItem> items, int index)
    {
        var count = items.Count;

        if (count == 0 || index < 0 || index >= count) return;

        for (int i = index; i < count; i++)
        {
            var item = items[i];
            item.Position = i;
            item.Margin = GetItemMargin(items, item);
        }
    }

    protected override Size MeasureItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || items.Count == 0 || item.Position == -1) return new();

        var iview = (item.Cell as IView)!;

        Size measure;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            measure = iview.Measure(GetEstimatedItemSize(item, availableSpace).Width, double.PositiveInfinity);

            item.Size = new(availableSpace.Width, measure.Height);
            if (ListViewHorizontalOptions == LayoutOptions.Fill)
            {
                width = availableSpace.Width;
            }
            else
            {
                var visibleItems = VisibleItems.Except([item]).ToList();
                var maxWidth = visibleItems.Count == 0 ? 0d : visibleItems.Max(i => i.MeasuredSize.Width);
                width = Math.Max(measure.Width, maxWidth);
            }
            item.MeasuredSize = measure;
            item.Size = new(width, measure.Height);
        }
        else
        {
            measure = iview.Measure(double.PositiveInfinity, GetEstimatedItemSize(item, availableSpace).Height);

            double height;
            if (ListViewVerticalOptions == LayoutOptions.Fill)
            {
                height = availableSpace.Height;
            }
            else
            {
                var visibleItems = VisibleItems.Except([item]).ToList();
                var maxHeight = visibleItems.Count == 0 ? 0d : visibleItems.Max(i => i.MeasuredSize.Height);
                height = Math.Max(measure.Height, maxHeight);
            }
            item.MeasuredSize = measure;
            item.Size = new(measure.Width, height);
        }

        return measure;
    }

    protected override void ArrangeItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || items.Count == 0 || item.Position == -1) return;

        var prevIndex = item.Position - 1;
        var prevItemRightBottom = prevIndex == -1 ? new() : items[prevIndex].RightBottomWithMargin;

        item.LeftTopWithMargin = IsOrientation(ScrollOrientation.Vertical)
            ? new(0d, prevItemRightBottom.Y)
            : new(prevItemRightBottom.X, 0d);

        MeasureItem(items, item, availableSpace);
    }

    protected override void ShiftItemsChunk(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both) || start < 0
            || start >= count || exclusiveEnd <= 0 || exclusiveEnd > count) return;

        var item = items[start];
        var prevIndex = start - 1;
        var prevItemRightBottom = prevIndex == -1 ? new() : items[prevIndex].RightBottomWithMargin;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var dy = prevItemRightBottom.Y - item.LeftTopWithMargin.Y;
            if (dy == 0d) return;

            for (int i = start; i < exclusiveEnd; i++)
            {
                var currentItem = items[i];

                currentItem.LeftTopWithMargin = new(currentItem.LeftTopWithMargin.X, currentItem.LeftTopWithMargin.Y + dy);
            }
        }
        else
        {
            var dx = prevItemRightBottom.X - item.LeftTopWithMargin.X;
            if (dx == 0d) return;

            for (int i = start; i < exclusiveEnd; i++)
            {
                var currentItem = items[i];

                currentItem.LeftTopWithMargin = new(currentItem.LeftTopWithMargin.X + dx, currentItem.LeftTopWithMargin.Y);
            }
        }
    }

    protected override void ShiftItemsConsecutively(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both) || start < 0
            || start >= count || exclusiveEnd <= 0 || exclusiveEnd > count) return;

        var prevIndex = start - 1;
        var prevItemRightBottom = prevIndex == -1 ? new() : items[prevIndex].RightBottomWithMargin;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            for (int i = start; i < exclusiveEnd; i++)
            {
                var item = items[i];

                item.LeftTopWithMargin = new(item.LeftTopWithMargin.X, prevItemRightBottom.Y);

                prevItemRightBottom = item.RightBottomWithMargin;
            }
        }
        else
        {
            for (int i = start; i < exclusiveEnd; i++)
            {
                var item = items[i];

                item.LeftTopWithMargin = new(prevItemRightBottom.X, item.LeftTopWithMargin.Y);

                prevItemRightBottom = item.RightBottomWithMargin;
            }
        }
    }

    protected override bool AdjustScrollIfNeeded(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevBoundsOfItem)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position == -1) return false;

        double dx = 0d, dy = 0d;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            dy = item.RightBottom.Y - prevBoundsOfItem.Bottom;

            if (dy == 0d) return false;
        }
        else
        {
            dx = item.RightBottom.X - prevBoundsOfItem.Right;

            if (dx == 0d) return false;
        }

        ListView!.AdjustScroll(dx, dy);

        return true;
    }

    protected override void AdjustScrollForItemBoundsChange(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevBoundsOfItem)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || item.Position == -1) return;

        double dx = 0d, dy = 0d;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var bottom = item.RightBottom.Y;
            var top = item.LeftTop.Y + ListView!.Padding.Top;
            dy = bottom - prevBoundsOfItem.Bottom;

            var scrollY = ListView.ScrollY;

            if (dy == 0d || (top < (scrollY + ListView.Height) && top > scrollY)) return;
        }
        else
        {
            var right = item.RightBottom.X;
            var left = item.LeftTop.X + ListView!.Padding.Left;
            dx = right - prevBoundsOfItem.Right;

            var scrollX = ListView.ScrollX;

            if (dx == 0d || (left < (scrollX + ListView.Width) && left > scrollX)) return;
        }

        ListView.AdjustScroll(dx, dy);
    }
}