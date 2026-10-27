using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Flowery.WinUI.Kanban.Controls;
using Microsoft.UI.Xaml;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    internal static class ControlCatalog
    {
        private static readonly Type ControlBaseType = typeof(FrameworkElement);

        public static IReadOnlyList<Type> GetAllControls()
        {
            return GetControlsByNamespace("Flowery.Controls");
        }

        public static IReadOnlyList<Type> GetKanbanControls()
        {
            return GetControlsByNamespace("Flowery.WinUI.Kanban.Controls", typeof(FlowKanban).Assembly);
        }

        private static IReadOnlyList<Type> GetControlsByNamespace(string rootNamespace, Assembly? assembly = null)
        {
            assembly ??= typeof(Flowery.Controls.DaisyButton).Assembly;
            return assembly.GetTypes()
                .Where(type => type.IsPublic)
                .Where(type => !type.IsAbstract)
                .Where(type => type.Namespace != null && type.Namespace.StartsWith(rootNamespace, StringComparison.Ordinal))
                .Where(type => ControlBaseType.IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();
        }
    }
}
