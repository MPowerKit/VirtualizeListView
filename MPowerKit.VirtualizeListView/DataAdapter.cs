﻿using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MPowerKit.VirtualizeListView;

public class DataAdapter : IDisposable
{
    public class AdapterItem(object data)
    {
        public object Data { get; } = data;
    }
    public class HeaderItem(object data) : AdapterItem(data);
    public class FooterItem(object data) : AdapterItem(data);

    public event EventHandler? DataSetChanged;
    public event EventHandler<(int startingIndex, int totalCount)>? ItemRangeInserted;
    public event EventHandler<(int startingIndex, int totalCount)>? ItemRangeRemoved;
    public event EventHandler<(int startingIndex, int oldCount, int newCount)>? ItemRangeChanged;
    public event EventHandler<(int oldIndex, int newIndex)>? ItemMoved;

    protected VirtualizeListView Control { get; set; }
    protected List<AdapterItem> InternalItems { get; set; } = [];

    public IReadOnlyList<AdapterItem> Items => InternalItems;

    public bool IsDisposed { get; protected set; }

    protected bool HadHeader => InternalItems.ElementAtOrDefault(0) is HeaderItem;
    protected bool HadFooter => InternalItems.ElementAtOrDefault(ItemsCount - 1) is FooterItem;

    public virtual bool HasHeader => Control.Header is not null && Control.HeaderTemplate is not null;
    public virtual bool HasFooter => Control.Footer is not null && Control.FooterTemplate is not null;

    public virtual int ItemsCount => InternalItems?.Count ?? 0;

    public DataAdapter(VirtualizeListView listView)
    {
        Control = listView;

        Control.PropertyChanged += Control_PropertyChanged;
    }

    private void Control_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == VirtualizeListView.HeaderProperty.PropertyName
             || e.PropertyName == VirtualizeListView.HeaderTemplateProperty.PropertyName)
        {
            var hadHeader = HadHeader;

            if (hadHeader && !HasHeader)
            {
                InternalItems.RemoveAt(0);
                NotifyItemRangeRemoved(0, 1);
            }
            else if (!hadHeader && HasHeader)
            {
                InternalItems.Insert(0, new HeaderItem(Control.Header));
                NotifyItemRangeInserted(0, 1);
            }
            else if (HadHeader && HasHeader)
            {
                InternalItems.RemoveAt(0);
                InternalItems.Insert(0, new HeaderItem(Control.Header));
                NotifyItemRangeChanged(0, 1, 1);
            }
        }
        else if (e.PropertyName == VirtualizeListView.FooterProperty.PropertyName
            || e.PropertyName == VirtualizeListView.FooterTemplateProperty.PropertyName)
        {
            var hadFooter = HadFooter;

            if (hadFooter && !HasFooter)
            {
                var position = ItemsCount - 1;

                InternalItems.RemoveAt(position);
                NotifyItemRangeRemoved(position, 1);
            }
            else if (!hadFooter && HasFooter)
            {
                InternalItems.Add(new FooterItem(Control.Footer));
                NotifyItemRangeInserted(ItemsCount - 1, 1);
            }
            else if (hadFooter && HasFooter)
            {
                var position = ItemsCount - 1;
                InternalItems.RemoveAt(position);
                InternalItems.Add(new FooterItem(Control.Footer));
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

    protected virtual DataTemplate GetItemTemplate(AdapterItem item)
    {
        if (Control.ItemTemplate is DataTemplateSelector selector)
        {
            return selector.SelectTemplate(item.Data, Control);
        }

        return Control.ItemTemplate;
    }

    public virtual List<(CellHolder cell, DataTemplate template)> CreateCellsPool(int poolSize)
    {
        List<DataTemplate> templates = [];
        if (Control.HeaderTemplate is not null)
        {
            templates.AddRange(GetAllTemplatesByTemplate(Control.HeaderTemplate));
        }
        if (Control.FooterTemplate is not null)
        {
            templates.AddRange(GetAllTemplatesByTemplate(Control.FooterTemplate));
        }

        templates.AddRange(GetAllTemplatesByTemplate(Control.ItemTemplate));

        List<(CellHolder cell, DataTemplate template)> result = [];

        for (int i = 0; i < templates.Count; i++)
        {
            var template = templates[i];

            for (int j = 0; j < poolSize; j++)
            {
                result.Add((CreateEmptyCellForTemplate(template), template));
            }
        }

        return result;
    }

    protected virtual List<DataTemplate> GetAllTemplatesByTemplate(DataTemplate template)
    {
        if (template is not DataTemplateSelector selector)
        {
            return [template];
        }

        var templates = selector.GetType().GetProperties();
        List<DataTemplate> result = new(templates.Length);
        for (int i = 0; i < templates.Length; i++)
        {
            var info = templates[i];

            if (info.PropertyType == typeof(DataTemplate)
                && info.GetValue(selector) is DataTemplate t
                && !result.Contains(t))
            {
                result.Add(t);
            }
        }

        return result;
    }

    protected virtual CellHolder CreateEmptyCellForTemplate(DataTemplate template)
    {
        var content = template.CreateContent() as View;
        var holder = new CellHolder()
        {
            /*Content =*/ content
        };
        return holder;
    }

    public virtual CellHolder OnCreateCell(DataTemplate template, int position)
    {
        var holder = CreateEmptyCellForTemplate(template);
        var content = holder[0];

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

        return holder;
    }

    protected virtual bool IsOneOf(DataTemplate itemTemplate, DataTemplate currentTemplate, int position)
    {
        var item = Items[position];

        if (itemTemplate is DataTemplateSelector selector)
        {
            itemTemplate = selector.SelectTemplate(item.Data, Control);
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

    public virtual void OnBindCell(CellHolder holder, AdapterItem item, int position)
    {
        holder.BindingContext = item.Data;

        if (holder.Children[0] is not VirtualizeListViewCell cell) return;

        cell.SendAppearing();
        OnItemAppearing(item, position);
    }

    public virtual void OnCellRecycled(CellHolder holder, AdapterItem item, int position)
    {
        var content = holder.Children[0];

        try
        {
            if (content is not VirtualizeListViewCell cell) return;

            cell.SendDisappearing();
            OnItemDisappearing(item, position);
        }
        finally
        {
            // commented out for better perfomance
            // theoretically bindingcontext should be nullified
            // but practically performance getting worse if uncommented
            //holder.BindingContext = null;
        }
    }

    protected virtual void OnItemAppearing(AdapterItem item, int position)
    {
        var (realPosition, realItemsCount) = GetRealPositionAndCount(item, position);

        if (realItemsCount == 0) return;

        Control.OnItemAppearing(item.Data, realPosition, realItemsCount);
    }

    protected virtual void OnItemDisappearing(AdapterItem item, int position)
    {
        var (realPosition, realItemsCount) = GetRealPositionAndCount(item, position);

        if (realItemsCount == 0) return;

        Control.OnItemDisappearing(item.Data, realPosition, realItemsCount);
    }

    public virtual (int RealPosition, int RealItemsCount) GetRealPositionAndCount(AdapterItem item, int position)
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
                OnCollectionChangedAdd(e);
                break;
            case NotifyCollectionChangedAction.Remove:
                OnCollectionChangedRemove(e);
                break;
            case NotifyCollectionChangedAction.Replace:
                OnCollectionChangedReplace(e);
                break;
            case NotifyCollectionChangedAction.Move:
                OnCollectionChangedMove(e);
                break;
            case NotifyCollectionChangedAction.Reset:
                OnCollectionChangedReset(itemsSource);
                break;
        }
    }

    protected virtual void OnCollectionChangedAdd(NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems?.Count is null or 0) return;

        var index = e.NewStartingIndex + HasHeader.ToInt();

        InternalItems.InsertRange(index, e.NewItems.Cast<object>().Select(d => new AdapterItem(d)));
        NotifyItemRangeInserted(index, e.NewItems.Count);
    }

    protected virtual void OnCollectionChangedRemove(NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems?.Count is null or 0) return;

        var index = e.OldStartingIndex + HasHeader.ToInt();
        var count = e.OldItems.Count;

        InternalItems.RemoveRange(index, count);
        NotifyItemRangeRemoved(index, count);
    }

    protected virtual void OnCollectionChangedReplace(NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems?.Count is null or 0 || e.OldItems?.Count is null or 0) return;

        var index = e.NewStartingIndex + HasHeader.ToInt();

        InternalItems.RemoveRange(index, e.OldItems.Count);
        InternalItems.InsertRange(index, e.NewItems.Cast<object>().Select(d => new AdapterItem(d)));

        NotifyItemRangeChanged(index, e.OldItems.Count, e.NewItems.Count);
    }

    protected virtual void OnCollectionChangedMove(NotifyCollectionChangedEventArgs e)
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
        List<AdapterItem> items = itemsSource is null ? [] : new(itemsSource.Cast<object>().Select(d => new AdapterItem(d)));

        if (HasHeader)
        {
            items.Insert(0, new HeaderItem(Control.Header));
        }

        if (HasFooter)
        {
            items.Add(new FooterItem(Control.Footer));
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