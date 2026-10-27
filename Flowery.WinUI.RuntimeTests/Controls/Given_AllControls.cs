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

            foreach (var (label, controlTypes) in cases)
            {
                foreach (var type in controlTypes)
                {
                    try
                    {
                        hostPanel.Children.Clear();

                        if (type.GetConstructor(Type.EmptyTypes) == null)
                        {
                            throw new InvalidOperationException("No parameterless constructor found.");
                        }

                        var instance = Activator.CreateInstance(type) as FrameworkElement;
                        if (instance == null)
                        {
                            throw new InvalidOperationException("Instance is not a FrameworkElement.");
                        }

                        hostPanel.Children.Add(instance);
                        instance.UpdateLayout();
                    }
                    catch (Exception ex)
                    {
                        failuresCount++;
                        failures.AppendLine($"[{label}] {type.FullName}: {ex.GetType().Name} - {ex.Message}");
                    }
                }
            }

            if (failuresCount > 0)
            {
                throw new InvalidOperationException(failures.ToString());
            }
        }
    }
}
