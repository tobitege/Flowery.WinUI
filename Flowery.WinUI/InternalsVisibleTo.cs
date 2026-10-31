using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Flowery.WinUI.RuntimeTests")]
// The Kiosk smoke checks set the motion preference (Services/DaisySystemAppearance.cs) without reflection, which trimming breaks.
[assembly: InternalsVisibleTo("Flowery.WinUI.Kiosk")]
// The pinch helpers (Helpers/FloweryPinchTracker.cs, FloweryScrollZoom.cs), which the board shares with the scheduler.
[assembly: InternalsVisibleTo("Flowery.WinUI.Kanban")]
