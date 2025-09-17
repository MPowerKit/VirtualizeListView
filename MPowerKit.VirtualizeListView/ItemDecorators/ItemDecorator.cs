using static MPowerKit.VirtualizeListView.GroupableDataAdapter;

namespace MPowerKit.VirtualizeListView;

public abstract class ItemDecorator
{
    public virtual void OnAttached(VirtualizeListView listView, VirtualizeItemsLayoutManger layoutManger, DataAdapter dataAdapter) { }
    public abstract void OnDrawOver();
    public virtual void OnDetached(VirtualizeListView listView, VirtualizeItemsLayoutManger layoutManger, DataAdapter dataAdapter) { }
}

public class StickyHeaderItemDecorator : ItemDecorator
{
    private const double OffScreen = -1000000d;

    private VirtualizeListViewItem? _prevStickyHeaderItem;
    private CellHolder? _stickyHeader;
    private VirtualizeListView? _listView;
    private VirtualizeItemsLayoutManger? _layoutManger;
    private GroupableDataAdapter? _dataAdapter;

    public override void OnAttached(VirtualizeListView listView, VirtualizeItemsLayoutManger layoutManger, DataAdapter dataAdapter)
    {
        base.OnAttached(listView, layoutManger, dataAdapter);

        _listView = listView;
        _layoutManger = layoutManger;
        _dataAdapter = dataAdapter as GroupableDataAdapter;
    }

    public override void OnDetached(VirtualizeListView listView, VirtualizeItemsLayoutManger layoutManger, DataAdapter dataAdapter)
    {
        _stickyHeader = null;
        _listView = null;
        _layoutManger = null;
        _dataAdapter = null;

        base.OnDetached(listView, layoutManger, dataAdapter);
    }

    public override void OnDrawOver()
    {
        if (_listView is null || _layoutManger is null || _dataAdapter is null
            || _listView.IsOrientation(ScrollOrientation.Both)) return;

        var visibleItems = _layoutManger.VisibleItems;
        var firstVisibleItem = visibleItems.OrderBy(i => i.Position).FirstOrDefault();
        if (firstVisibleItem == default) return;

        var topChildPosition = firstVisibleItem.Position;

        var originalHeaderItem = GetGroupHeaderItemForPosition(topChildPosition, _layoutManger.ReadOnlyLaidOutItems);
        if (originalHeaderItem is null) return;

        if (_prevStickyHeaderItem?.Position != originalHeaderItem.Position)
        {
            var item = _layoutManger.CreateItemForPosition(originalHeaderItem.Position);
            item.Size = originalHeaderItem.Size;
            item.LeftTopWithMargin = originalHeaderItem.LeftTopWithMargin;

            var cell = _dataAdapter.OnCreateCell(item.Template!, item.Position);
            if (_stickyHeader is not null)
            {
                _layoutManger.Remove(_stickyHeader);
            }
            item.Cell = _stickyHeader = cell;
            _layoutManger.Add(_stickyHeader);

            _dataAdapter.OnBindCell(_stickyHeader, item.AdapterItem!, item.Position);
            _stickyHeader.ZIndex = 1000;
            _stickyHeader.TranslationX = OffScreen;
            _stickyHeader.TranslationY = OffScreen;

            _layoutManger.MeasureItem(item, _layoutManger.AvailableSpace);
            (_stickyHeader as IView).Arrange(item.Bounds);

            _prevStickyHeaderItem = item;
        }

        var childInContact = GetChildInContact(visibleItems, _prevStickyHeaderItem, _listView);
        if (_prevStickyHeaderItem.Position != childInContact?.Position
            && childInContact?.AdapterItem is GroupHeaderItem)
        {
            ShiftHeader(_prevStickyHeaderItem, childInContact, _listView);
            return;
        }

        DrawHeader(_prevStickyHeaderItem, originalHeaderItem, _listView);
    }

    private static void DrawHeader(VirtualizeListViewItem stickyHeaderItem, VirtualizeListViewItem originalHeaderItem, VirtualizeListView listView)
    {
        var cell = stickyHeaderItem.Cell!;
        var leftTop = originalHeaderItem.LeftTop;

        if (listView.IsOrientation(ScrollOrientation.Vertical))
        {
            var offsetY = listView.ScrollY - listView.Padding.Top;
            if (leftTop.Y >= offsetY)
            {
                cell.TranslationX = OffScreen;
                cell.TranslationY = OffScreen;
            }
            else
            {
#if !WINDOWS
                cell.TranslationX = 0d;
                cell.TranslationY = offsetY - leftTop.Y;
#else
                cell.TranslationX = 0d;
                cell.TranslationY = 0d;
#endif
            }
        }
        else
        {
            var offsetX = listView.ScrollX - listView.Padding.Left;
            if (leftTop.X >= offsetX)
            {
                cell.TranslationX = OffScreen;
                cell.TranslationY = OffScreen;
            }
            else
            {
#if !WINDOWS
                cell.TranslationX = offsetX - leftTop.X;
                cell.TranslationY = 0d;
#else
                cell.TranslationX = 0d;
                cell.TranslationY = 0d;
#endif
            }
        }
    }

    private static void ShiftHeader(VirtualizeListViewItem stickyHeaderItem, VirtualizeListViewItem headerItemInContact, VirtualizeListView listView)
    {
        var cell = stickyHeaderItem.Cell!;
        var leftTop = stickyHeaderItem.LeftTop;

        if (listView.IsOrientation(ScrollOrientation.Vertical))
        {
            if (leftTop.Y + cell.TranslationY + stickyHeaderItem.Size.Height <= headerItemInContact.LeftTop.Y)
            {
#if !WINDOWS
                cell.TranslationX = 0d;
                cell.TranslationY = headerItemInContact.LeftTop.Y - stickyHeaderItem.Size.Height - leftTop.Y;
#else
                cell.TranslationX = 0d;
                cell.TranslationY = 0d;
#endif
            }
        }
        else
        {
            if (leftTop.X + cell.TranslationX + stickyHeaderItem.Size.Width <= headerItemInContact.LeftTop.X)
            {
#if !WINDOWS
                cell.TranslationX = headerItemInContact.LeftTop.X - stickyHeaderItem.Size.Width - leftTop.X;
                cell.TranslationY = 0d;
#else
                cell.TranslationX = 0d;
                cell.TranslationY = 0d;
#endif
            }
        }
    }

    private static VirtualizeListViewItem? GetChildInContact(IEnumerable<VirtualizeListViewItem> visibleItems, VirtualizeListViewItem stickyHeaderItem, VirtualizeListView listView)
    {
        var stickyBounds = stickyHeaderItem.Bounds;

        if (listView.IsOrientation(ScrollOrientation.Vertical))
        {
            var offsetY = listView.ScrollY - listView.Padding.Top;
            var contactPoint = offsetY + stickyBounds.Height;

            foreach (var child in visibleItems)
            {
                var childBounds = child.Bounds;
                if (childBounds.Top <= contactPoint && childBounds.Bottom > contactPoint) return child;
            }

            //for (int i = 0; i < visibleItems.Count; i++)
            //{
            //    var child = visibleItems[i];
            //    var childBounds = child.Bounds;
            //    if (childBounds.Top <= contactPoint && childBounds.Bottom > contactPoint) return child;
            //}
        }
        else
        {
            var offsetX = listView.ScrollX - listView.Padding.Left;
            var contactPoint = offsetX + stickyBounds.Width;

            foreach (var child in visibleItems)
            {
                var childBounds = child.Bounds;
                if (childBounds.Left <= contactPoint && childBounds.Right > contactPoint) return child;
            }

            //for (int i = 0; i < visibleItems.Count; i++)
            //{
            //    var child = visibleItems[i];
            //    var childBounds = child.Bounds;
            //    if (childBounds.Left <= contactPoint && childBounds.Right > contactPoint) return child;
            //}
        }

        return null;
    }

    private static VirtualizeListViewItem? GetGroupHeaderItemForPosition(int position, IReadOnlyList<VirtualizeListViewItem> items)
    {
        for (int i = position; i >= 0; i--)
        {
            var item = items[i];

            if (item.AdapterItem is GroupHeaderItem) return item;
        }

        return null;
    }
}