using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MPowerKit.VirtualizeListView;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that supports bulk operations to avoid frequent update notification events.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// Initializes a new instance of <see cref="ObservableRangeCollection{T}"/> that is empty and has default initial capacity.
    /// </summary>
    public ObservableRangeCollection()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/> class that contains
    /// items copied from the specified collection and has sufficient capacity
    /// to accommodate the number of items copied.
    /// </summary>
    /// <param name="collection">The collection whose items are copied to the new list.</param>
    /// <remarks>
    /// The items are copied onto the <see cref="ObservableRangeCollection{T}"/> in the
    /// same order they are read by the enumerator of the collection.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is a null reference.</exception>
    public ObservableRangeCollection(IEnumerable<T> collection) : base(collection)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/> class
    /// that contains items copied from the specified list.
    /// </summary>
    /// <param name="list">The list whose items are copied to the new list.</param>
    /// <remarks>
    /// The items are copied onto the <see cref="ObservableRangeCollection{T}"/> in the
    /// same order they are read by the enumerator of the list.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> is a null reference.</exception>
    public ObservableRangeCollection(List<T> list) : base(list)
    {
    }

    /// <summary>
    /// Adds the items of the specified collection to the end of this <see cref="ObservableCollection{T}"/>.
    /// </summary>
    /// <param name="collection">
    /// The collection whose items should be added to the end of this <see cref="ObservableCollection{T}"/>.
    /// The collection itself cannot be null, but it can contain items that are null, if type T is a reference type.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is null.</exception>
    public void AddRange(IEnumerable<T> collection)
    {
        InsertRange(Count, collection);
    }

    /// <summary>
    /// Inserts the items of a collection into this <see cref="ObservableCollection{T}"/> at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which the new items should be inserted.</param>
    /// <param name="collection">
    /// The collection whose items should be inserted into the <see cref="List{T}"/>.
    /// The collection itself cannot be null, but it can contain items that are null, if type T is a reference type.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not in the collection range.</exception>
    public void InsertRange(int index, IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        ArgumentOutOfRangeException.ThrowIfNegative(index);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, Count);

        var changedItems = collection.ToList();

        if (changedItems.Count == 0) return;

        if (changedItems.Count == 1)
        {
            Insert(index, changedItems[0]);
            return;
        }

        CheckReentrancy();

        // Items will always be List<T>, see constructors.
        var items = (List<T>)Items;
        items.InsertRange(index, changedItems);

        OnEssentialPropertiesChanged();

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, changedItems, index));
    }

    /// <summary>
    /// Removes consecutive range of items in the specified collection from this <see cref="ObservableCollection{T}"/>.
    /// </summary>
    /// <param name="collection">The items to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="collection"/>'s first item is not in the collection.</exception>
    public void RemoveRange(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var itemsToRemove = collection.ToList();

        if (Count == 0 || itemsToRemove.Count == 0) return;

        var startingItem = itemsToRemove[0];

        var startingIndex = Items.IndexOf(startingItem);

        RemoveRange(startingIndex, itemsToRemove.Count);
    }

    /// <summary>
    /// Removes a range of items from this <see cref="ObservableCollection{T}"/>.
    /// </summary>
    /// <param name="index">The zero-based starting index of the range of items to remove.</param>
    /// <param name="count">The number of items to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">The specified range is exceeding the collection.</exception>
    public void RemoveRange(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        ArgumentOutOfRangeException.ThrowIfNegative(count);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Count - index);

        if (count == 0) return;

        if (count == 1)
        {
            RemoveItem(index);
            return;
        }

        if (index == 0 && count == Count)
        {
            Clear();
            return;
        }

        // Items will always be List<T>, see constructors.
        var items = (List<T>)Items;
        var removedItems = items.GetRange(index, count);

        CheckReentrancy();

        items.RemoveRange(index, count);

        OnEssentialPropertiesChanged();

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems, index));
    }

    /// <summary>
    /// Clears the current collection and replaces it with the specified item.
    /// </summary>
    /// <param name="item">The item to fill the collection with, after clearing it.</param>
    public void Replace(T item)
    {
        ReplaceRange(0, Count, [item]);
    }

    /// <summary>
    /// Clears the current collection and replaces it with the specified collection.
    /// </summary>
    /// <param name="collection">The items to fill the collection with, after clearing it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is null.</exception>
    public void ReplaceRange(IEnumerable<T> collection)
    {
        ReplaceRange(0, Count, collection);
    }

    /// <summary>
    /// Replaces old item by new item.
    /// </summary>
    /// <param name="oldItem">The item to be replaced in the collection.</param>
    /// <param name="newItem">The item to replace old one in the collection.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="oldItem"/> not found in the collection.</exception>
    public void Replace(T oldItem, T newItem)
    {
        var index = Items.IndexOf(oldItem);

        ArgumentOutOfRangeException.ThrowIfNegative(index);

        this[index] = newItem;
    }

    /// <summary>
    /// Removes the specified range and inserts the specified collection in its position, leaving equal items in equal positions intact.
    /// <para>When both index and count are equal to 0, it is equivalent to InsertRange(0, collection).</para>
    /// </summary>
    /// <remarks>This method is roughly equivalent to <see cref="RemoveRange(Int32, Int32)"/> then <see cref="InsertRange(Int32, IEnumerable{T})"/>.</remarks>
    /// <param name="index">The index of where to start the replacement.</param>
    /// <param name="count">The number of items to be replaced.</param>
    /// <param name="collection">The collection to insert in that location.</param>
    /// <exception cref="ArgumentOutOfRangeException">The specified range is exceeding the collection.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is null.</exception>
    public void ReplaceRange(int index, int count, IEnumerable<T> collection)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Count - index);

        ArgumentNullException.ThrowIfNull(collection);

        var newRange = collection.ToList();

        if (newRange.Count == 0)
        {
            RemoveRange(index, count);
            return;
        }

        if (index + count == 0)
        {
            InsertRange(0, newRange);
            return;
        }

        // Items will always be List<T>, see constructors.
        var items = (List<T>)Items;
        var oldRange = items.GetRange(index, count);

        CheckReentrancy();

        items.RemoveRange(index, count);
        items.InsertRange(index, newRange);

        OnEssentialPropertiesChanged();

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newRange, oldRange, index));
    }

    /// <summary>
    /// Called by base class <see cref="Collection{T}"/> when the list is being cleared;
    /// raises a <see cref="ObservableCollection{T}.CollectionChanged"/> event to any listeners.
    /// </summary>
    protected override void ClearItems()
    {
        if (Count == 0) return;

        base.ClearItems();
    }

    /// <summary>
    /// Helper to raise Count property and the Indexer property.
    /// </summary>
    private void OnEssentialPropertiesChanged()
    {
        OnPropertyChanged(EventArgsCache.CountPropertyChanged);
        OnIndexerPropertyChanged();
    }

    /// <summary>
    /// Helper to raise a PropertyChanged event for the Indexer property.
    /// </summary>
    private void OnIndexerPropertyChanged()
    {
        OnPropertyChanged(EventArgsCache.IndexerPropertyChanged);
    }
}

/// <remarks>
/// To be kept outside <see cref="ObservableCollection{T}"/>, since otherwise, a new instance will be created for each generic type used.
/// </remarks>
internal static class EventArgsCache
{
    internal static readonly PropertyChangedEventArgs CountPropertyChanged = new("Count");

    internal static readonly PropertyChangedEventArgs IndexerPropertyChanged = new("Item[]");

    internal static readonly NotifyCollectionChangedEventArgs ResetCollectionChanged = new(NotifyCollectionChangedAction.Reset);
}