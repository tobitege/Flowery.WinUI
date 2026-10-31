using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Flowery.WinUI.Kanban.Converters;

/// <summary>
/// Returns the ListView padding used for the tasks list.
/// Adds a right inset so the Windows overlay scrollbar does not cover cards.
/// </summary>
public sealed partial class KanbanTasksListViewPaddingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Preserve the existing XAML intent: add a small right inset where scrollbars can overlap content.
        return new Thickness(0, 0, 12, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

