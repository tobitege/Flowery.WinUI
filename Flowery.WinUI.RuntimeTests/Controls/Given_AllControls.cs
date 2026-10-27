using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_AllControls
    {
        [TestMethod]
        public void When_Constructing_AllControls_NoExceptions()
        {
            if (RuntimeTestContext.HostPanel is not Panel hostPanel)
            {
                Assert.Inconclusive("Runtime test host panel is not initialized.");
                return;
            }

            // The core assembly includes the ColorPicker namespace; Kanban lives in its own assembly.
            var cases = new (string Label, IReadOnlyList<Type> ControlTypes)[]
            {
                ("All", ControlCatalog.GetAllControls()),
                ("Kanban", ControlCatalog.GetKanbanControls())
            };

            var failures = new StringBuilder();
            var failuresCount = 0;

            foreach (var (label, type) in cases.SelectMany(static group => group.ControlTypes, static (group, controlType) => (group.Label, controlType)))
            {
                try
                {
                    Construct(hostPanel, type);
                }
                catch (Exception ex)
                {
                    failuresCount++;
                    failures.AppendLine($"[{label}] {type.FullName}: {ex.GetType().Name} - {ex.Message}");
                }
            }

            if (failuresCount > 0)
            {
                throw new InvalidOperationException(failures.ToString());
            }
        }

        private static void Construct(Panel hostPanel, Type type)
        {
            hostPanel.Children.Clear();

            if (type.GetConstructor(Type.EmptyTypes) is null)
                throw new InvalidOperationException("No parameterless constructor found.");

            if (Activator.CreateInstance(type) is not FrameworkElement instance)
                throw new InvalidOperationException("Instance is not a FrameworkElement.");

            hostPanel.Children.Add(instance);
            instance.UpdateLayout();
        }
    }
}
