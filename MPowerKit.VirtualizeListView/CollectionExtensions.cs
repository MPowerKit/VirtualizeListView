using System.Collections;

namespace MPowerKit.VirtualizeListView;

public static class CollectionExtensions
{
    /// <summary>
    /// Finds the parent of the specified element that is of the specified type.
    /// </summary>
    /// <param name="element">The element to find the parent of.</param>
    /// <param name="includeThis">Whether to include the element itself in the search.</param>
    /// <typeparam name="T">The type of the parent to find.</typeparam>
    /// <returns>The parent of the specified type, or null if no such parent exists.</returns>
    public static T? FindParentOfType<T>(this Element? element, bool includeThis = false)
        where T : IElement
    {
        if (element is null) return default;
        if (includeThis && element is T view) return view;

        while (element.Parent is not null)
        {
            if (element.Parent is T parent) return parent;

            element = element.Parent;
        }

        return default;
    }

    /// <summary>
    /// Determines the index of a specific item in the <see cref="IEnumerable"/>.
    /// </summary>
    /// <param name="enumerable">The IEnumerable to locate the item in.</param>
    /// <param name="item">The object to locate in the IEnumerable.</param>
    /// <returns>The index of item if found in the list; otherwise, -1.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enumerable"/> is null.</exception>
    public static int IndexOf(this IEnumerable enumerable, object item)
    {
        ArgumentNullException.ThrowIfNull(enumerable);

        var i = 0;
        foreach (var element in enumerable)
        {
            if (Equals(element, item)) return i;

            i++;
        }

        return -1;
    }

    /// <summary>
    /// Determines the index of a specific item in the <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <param name="enumerable">The IEnumerable to locate the item in.</param>
    /// <param name="item">The object to locate in the IEnumerable.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <returns>The index of item if found in the list; otherwise, -1.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enumerable"/> is null.</exception>
    public static int IndexOf<T>(this IEnumerable<T> enumerable, T item)
    {
        ArgumentNullException.ThrowIfNull(enumerable);

        var i = 0;
        foreach (T element in enumerable)
        {
            if (Equals(element, item)) return i;

            i++;
        }

        return -1;
    }

    /// <summary>
    /// Moves an item in the list from one index to another.
    /// </summary>
    /// <param name="list">The list where the item is to be moved.</param>
    /// <param name="oldIndex">The zero-based index specifying the location of the item before the move.</param>
    /// <param name="newIndex">The zero-based index specifying the location of the item after the move.</param>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> is null.</exception>
    public static void Move<T>(this IList<T> list, int oldIndex, int newIndex)
    {
        ArgumentNullException.ThrowIfNull(list);

        var item = list[oldIndex];
        list.RemoveAt(oldIndex);
        list.Insert(newIndex, item);
    }

    /// <summary>
    /// Counts the number of elements in the specified enumerable collection.
    /// </summary>
    /// <param name="enumerable">The enumerable collection to count the elements of.</param>
    /// <returns>The number of elements in the enumerable collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enumerable"/> is null.</exception>
    public static int Count(this IEnumerable enumerable)
    {
        ArgumentNullException.ThrowIfNull(enumerable);

        var i = 0;
        foreach (var element in enumerable)
        {
            i++;
        }

        return i;
    }

    /// <summary>
    /// Converts a boolean value to an integer.
    /// </summary>
    /// <param name="value">The boolean value to convert.</param>
    /// <returns>Returns 1 if the value is true, otherwise 0.</returns>
    public static int ToInt(this bool value)
    {
        return value ? 1 : 0;
    }

    /// <summary>
    /// Replaces old item by new item in the IList collection
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    /// <returns>Index of replaced item, otherwise -1</returns>
    public static int Replace<T>(IList<T> source, T oldValue, T newValue)
    {
        ArgumentNullException.ThrowIfNull(source);

        var index = source.IndexOf(oldValue);
        if (index != -1) source[index] = newValue;
        return index;
    }
}