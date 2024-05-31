using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MPowerKit.VirtualizeListView;

public class DataAdapter : IDisposable
{
    public event EventHandler DataSetChanged;
    public event EventHandler<(int startingIndex, int totalCount)> ItemRangeInserted;
    public event EventHandler<(int startingIndex, int totalCount)> ItemRangeRemoved;
    public event EventHandler<(int startingIndex, int oldCount, int newCount)> ItemRangeChanged;
    public event EventHandler<(int oldIndex, int newIndex)> ItemMoved;

    protected VirtualizeListView Control { get; set; }
    protected List<object> InternalItems { get; set; } = [];

    public IReadOnlyList<object> Items => InternalItems;

    public bool IsDisposed { get; protected set; }

    protected bool HadHeader { get; set; }
    protected bool HadFooter { get; set; }

    public virtual bool HasHeader => Control.Header is not null && Control.HeaderTemplate is not null;
    public virtual bool HasFooter => Control.Footer is not null && Control.FooterTemplate is not null;

    public virtual int ItemsCount => InternalItems?.Count ?? 0;

    public DataAdapter(VirtualizeListView listView)
    {
        Control = listView;

        Control.PropertyChanging += Control_PropertyChanging;
        Control.PropertyChanged += Control_PropertyChanged;
    }

    private void Control_PropertyChanging(object sender, Microsoft.Maui.Controls.PropertyChangingEventArgs e)
    {
        if (e.PropertyName == VirtualizeListView.HeaderProperty.PropertyName
            || e.PropertyName == VirtualizeListView.HeaderTemplateProperty.PropertyName)
        {
            HadHeader = HasHeader;
        }
        else if (e.PropertyName == VirtualizeListView.FooterProperty.PropertyName
            || e.PropertyName == VirtualizeListView.FooterTemplateProperty.PropertyName)
        {
            HadFooter = HasFooter;
        }
    }

    private void Control_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == VirtualizeListView.HeaderProperty.PropertyName
             || e.PropertyName == VirtualizeListView.HeaderTemplateProperty.PropertyName)
        {
            if (HadHeader && !HasHeader)
            {
                InternalItems.RemoveAt(0);
                NotifyItemRangeRemoved(0, 1);
            }
            else if (!HadHeader && HasHeader)
            {
                InternalItems.Insert(0, Control.Header);
                NotifyItemRangeInserted(0, 1);
            }
            else if (HadHeader && HasHeader)
            {
                InternalItems.RemoveAt(0);
                InternalItems.Insert(0, Control.Header);
                NotifyItemRangeChanged(0, 1, 1);
            }
        }
        else if (e.PropertyName == VirtualizeListView.FooterProperty.PropertyName
            || e.PropertyName == VirtualizeListView.FooterTemplateProperty.PropertyName)
        {
            if (HadFooter && !HasFooter)
            {
                var position = InternalItems.Count - 1;

                InternalItems.RemoveAt(position);
                NotifyItemRangeRemoved(position, 1);
            }
            else if (!HadFooter && HasFooter)
            {
                InternalItems.Add(Control.Footer);
                NotifyItemRangeInserted(InternalItems.Count - 1, 1);
            }
            else if (HadFooter && HasFooter)
            {
                var position = InternalItems.Count - 1;
                InternalItems.RemoveAt(position);
                InternalItems.Add(Control.Footer);
                NotifyItemRangeChanged(position, 1, 1);
            }
        }
    }

    public virtual DataTemplate GetTemplate(int position)
    {
        if (HasHeader && position == 0)
        {
            return GetHeaderTemplate();
        }

        if (HasFooter && position == ItemsCount - 1)
        {
            return GetFooterTemplate();
        }

        return GetItemTemplate(InternalItems[position]);
    }

    protected virtual DataTemplate GetHeaderTemplate()
    {
        if (Control.HeaderTemplate is DataTemplateSelector headerSelector)
        {
            return headerSelector.SelectTemplate(Control.Header, Control);
        }

        return Control.HeaderTemplate;
    }

    protected virtual DataTemplate GetFooterTemplate()
    {
        if (Control.FooterTemplate is DataTemplateSelector footerSelector)
        {
            return footerSelector.SelectTemplate(Control.Footer, Control);
        }

        return Control.FooterTemplate;
    }

    protected virtual DataTemplate GetItemTemplate(object item)
    {
        if (Control.ItemTemplate is DataTemplateSelector selector)
        {
            return selector.SelectTemplate(item, Control);
        }

        return Control.ItemTemplate;
    }

    public virtual CellHolder OnCreateCell(DataTemplate template, int position)
    {
        var content = template.CreateContent() as View;

        if (HasHeader && position == 0 && content is VirtualizeListViewCell)
        {
            throw new ArgumentException("HeaderTemplate can't be typeof(VirtualizeListViewCell)");
        }

        if (HasFooter && position == ItemsCount - 1 && content is VirtualizeListViewCell)
        {
            throw new ArgumentException("FooterTemplate can't be typeof(VirtualizeListViewCell)");
        }

        if (content is not VirtualizeListViewCell)
        {
            throw new ArgumentException("ItemTemplate has to be typeof(VirtualizeListViewCell)");
        }

        var holder = new CellHolder()
        {
            /*Content =*/ content
        };
        return holder;
    }

    protected virtual bool IsOneOf(DataTemplate itemTemplate, DataTemplate currentTemplate, int position)
    {
        var item = Items[position];

        if (itemTemplate is DataTemplateSelector selector)
        {
            itemTemplate = selector.SelectTemplate(item, Control);
        }
        return itemTemplate == currentTemplate;
    }

    public virtual bool IsHeader(DataTemplate template, int position)
    {
        return IsOneOf(Control.HeaderTemplate, template, position);
    }

    public virtual bool IsFooter(DataTemplate template, int position)
    {
        return IsOneOf(Control.FooterTemplate, template, position);
    }

    public virtual void OnBindCell(CellHolder holder, int position)
    {
        var data = Items[position];

        holder.BindingContext = data;

        if (holder.Children[0] is not VirtualizeListViewCell cell) return;

        cell.SendAppearing();
        OnItemAppearing(data, position);
    }

    protected virtual void OnItemAppearing(object item, int position)
    {
        var (realPosition, realItemsCount) = GetRealPositionAndCount(item, position);

        if (realItemsCount == 0) return;

        Control.OnItemAppearing(item, realPosition, realItemsCount);
    }

    public virtual void OnCellRecycled(CellHolder holder, int position)
    {
        var content = holder.Children[0];

        try
        {
            if (content is not VirtualizeListViewCell cell) return;

            cell.SendDisappearing();
            OnItemDisappearing(holder.BindingContext, position);
        }
        finally
        {
            // commented out for better perfomance
            // theoretically bindingcontext should be nullified
            // but practically performance getting worse if uncommented
            //holder.BindingContext = null;
        }
    }

    protected virtual void OnItemDisappearing(object item, int position)
    {
        var (realPosition, realItemsCount) = GetRealPositionAndCount(item, position);

        if (realItemsCount == 0) return;

        Control.OnItemDisappearing(item, realPosition, realItemsCount);
    }

    public virtual (int RealPosition, int RealItemsCount) GetRealPositionAndCount(object item, int position)
    {
        var header = HasHeader.ToInt();

        var realPosition = position - header;

        var realItemsCount = ItemsCount - header - HasFooter.ToInt();

        return (realPosition, realItemsCount);
    }

    public virtual void NotifyDataSetChanged()
    {
        NotifyWrapper(() =>
        {
            DataSetChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public virtual void NotifyItemRangeInserted(int startingIndex, int totalCount)
    {
        NotifyWrapper(() =>
        {
            ItemRangeInserted?.Invoke(this, (startingIndex, totalCount));
        });
    }

    public virtual void NotifyItemRangeRemoved(int startingIndex, int totalCount)
    {
        NotifyWrapper(() =>
        {
            ItemRangeRemoved?.Invoke(this, (startingIndex, totalCount));
        });
    }

    public virtual void NotifyItemRangeChanged(int startingIndex, int oldCount, int newCount)
    {
        NotifyWrapper(() =>
        {
            ItemRangeChanged?.Invoke(this, (startingIndex, oldCount, newCount));
        });
    }

    public virtual void NotifyItemMoved(int oldIndex, int newIndex)
    {
        NotifyWrapper(() =>
        {
            ItemMoved?.Invoke(this, (oldIndex, newIndex));
        });
    }

    public virtual void ReloadData()
    {
        var itemsSource = Control?.ItemsSource;

        RemoveListenerCollection(itemsSource);

        InitCollection(itemsSource);
    }

    protected virtual void RemoveListenerCollection(IEnumerable? itemsSource)
    {
        if (itemsSource is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= ItemsSourceCollectionChanged;
        }
    }

    public virtual void InitCollection(IEnumerable? itemsSource)
    {
        if (itemsSource is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += ItemsSourceCollectionChanged;
        }

        OnCollectionChangedReset(itemsSource);
    }

    #region CollectionChanged events
    protected virtual void ItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var itemsSource = sender as IEnumerable;

        if (IsDisposed)
        {
            RemoveListenerCollection(itemsSource);
            return;
        }

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                OnCollectionChangedAdd(itemsSource, e);
                break;
            case NotifyCollectionChangedAction.Remove:
                OnCollectionChangedRemove(itemsSource, e);
                break;
            case NotifyCollectionChangedAction.Replace:
                OnCollectionChangedReplace(itemsSource, e);
                break;
            case NotifyCollectionChangedAction.Move:
                OnCollectionChangedMove(itemsSource, e);
                break;
            case NotifyCollectionChangedAction.Reset:
                OnCollectionChangedReset(itemsSource);
                break;
        }
    }

    protected virtual void OnCollectionChangedAdd(IEnumerable? itemsSource, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems?.Count is null or 0) return;

        var index = e.NewStartingIndex + HasHeader.ToInt();

        InternalItems.InsertRange(index, e.NewItems.Cast<object>());
        NotifyItemRangeInserted(index, e.NewItems.Count);
    }

    protected virtual void OnCollectionChangedRemove(IEnumerable? itemsSource, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems?.Count is null or 0) return;

        var index = e.OldStartingIndex + HasHeader.ToInt();
        var count = e.OldItems.Count;

        InternalItems.RemoveRange(index, count);
        NotifyItemRangeRemoved(index, count);
    }

    protected virtual void OnCollectionChangedReplace(IEnumerable? itemsSource, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems?.Count is null or 0 || e.OldItems?.Count is null or 0) return;

        var index = e.NewStartingIndex + HasHeader.ToInt();

        InternalItems.RemoveRange(index, e.OldItems.Count);
        InternalItems.InsertRange(index, e.NewItems.Cast<object>());

        NotifyItemRangeChanged(index, e.OldItems.Count, e.NewItems.Count);
    }

    protected virtual void OnCollectionChangedMove(IEnumerable? itemsSource, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems?.Count is null or 0 or > 1 || e.OldItems?.Count is null or 0 or > 1
            || e.NewStartingIndex == e.OldStartingIndex) return;

        var additional = HasHeader.ToInt();
        var oldIndex = e.OldStartingIndex + additional;
        var newIndex = e.NewStartingIndex + additional;

        InternalItems.Move(oldIndex, newIndex);
        NotifyItemMoved(oldIndex, newIndex);
    }

    protected virtual void OnCollectionChangedReset(IEnumerable? itemsSource)
    {
        List<object> items = itemsSource is null ? [] : new(itemsSource.Cast<object>());

        if (HasHeader)
        {
            items.Insert(0, Control.Header);
        }

        if (HasFooter)
        {
            items.Add(Control.Footer);
        }

        InternalItems = items;

        NotifyDataSetChanged();
    }

    private void NotifyWrapper(Action notifyAction)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(Notify);
        }
        else Notify();

        void Notify()
        {
            if (IsDisposed)
            {
                RemoveListenerCollection(Control.ItemsSource);
            }
            else
            {
                notifyAction.Invoke();
            }
        }
    }
    #endregion

    #region IDisposable
    protected virtual void Dispose(bool disposing)
    {
        if (this.IsDisposed) return;

        RemoveListenerCollection(Control.ItemsSource);

        Control.PropertyChanging -= Control_PropertyChanging;
        Control.PropertyChanged -= Control_PropertyChanged;

        IsDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~DataAdapter()
    {
        Dispose(false);
    }
    #endregion
}