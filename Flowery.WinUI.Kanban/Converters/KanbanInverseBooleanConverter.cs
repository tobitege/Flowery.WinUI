using System;
using Microsoft.UI.Xaml.Data;

namespace Flowery.WinUI.Kanban.Converters;

/// <summary>
/// Negates a boolean, for example to show the avatar placeholder while a card has no avatar image.
/// </summary>
public sealed class KanbanInverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is not true;
}
