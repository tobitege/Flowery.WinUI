using System.Collections.Specialized;

namespace Flowery.Helpers
{
    /// <summary>
    /// Prepares an items source that a Flowery control receives as <see cref="object"/> for a WinUI items control.
    /// WinUI reads a list only through marshalling code that CsWinRT generates for the list's type. In a NativeAOT app
    /// it generates that code only where it sees the type reach WinUI, never behind an <see cref="object"/> property, so
    /// a list of the app's own items fails there with "Value does not fall within the expected range". A
    /// <see cref="List{T}"/> of <see cref="object"/> always has that code, and WinUI shows any item in it.
    /// </summary>
    internal static class WinUIItemsSource
    {
        /// <summary>
        /// Returns a copy of a sequence (any <see cref="IEnumerable"/>, such as an array, a list or a query) as a
        /// <see cref="List{T}"/> of <see cref="object"/>, with the same items in the same order; WinUI follows no change
        /// of such a sequence anyway. A collection that reports its changes, a string and a value that is no sequence
        /// stay as they are.
        /// </summary>
        public static object? For(object? source) => source switch
        {
            null or string or INotifyCollectionChanged => source,
            IEnumerable items => new List<object>(items.Cast<object>()),
            _ => source
        };
    }
}
