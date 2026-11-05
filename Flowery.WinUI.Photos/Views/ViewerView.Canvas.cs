using System.Collections.Immutable;
using Flowery.WinUI.Photos.Editing;
using Flowery.WinUI.Photos.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using static Flowery.WinUI.Photos.Views.UiHelpers;

namespace Flowery.WinUI.Photos.Views
{
    /// <summary>
    /// Viewer canvas: rendering, zoom/pan, before/after split, crop handles and markup drawing.
    /// </summary>
    public sealed partial class ViewerView
    {
        private enum CropHandle
        {
            None,
            Move,
            TopLeft,
            Top,
            TopRight,
            Right,
            BottomRight,
            Bottom,
            BottomLeft,
            Left
        }

        private readonly record struct ViewTransform(CanvasBitmap Source, Size SourceSize, Size ImageSize, double Scale, Vector2 Offset)
        {
            public Rect ScreenRect => new(Offset.X, Offset.Y, ImageSize.Width * Scale, ImageSize.Height * Scale);
        }

        private const float HandleHitRadius = 16;

        /// <summary>Zoom in DIPs per full-resolution pixel; null means "fit to window".</summary>
        private double? _zoom;
        private Vector2 _pan;
        private bool _panning;
        private Point _pointerStart;
        private Vector2 _panStart;
        private bool _compareSplit;
        private double _split = 0.5;
        private bool _draggingSplit;
        private CropHandle _cropHandle;
        private Rect _cropStart;
        private Rect? _liveCrop;
        private CropAspectLock _cropAspect;
        private List<Vector2>? _currentStroke;
        private Color _penColor = Color.FromArgb(255, 239, 68, 68);
        private Color _currentStrokeColor;
        private double _penSize = 6;
        private bool _highlighter;

        private bool IsCropMode => _panelOpen && _tab == EditTab.Crop && !_slideshow;

        private bool IsMarkupMode => _panelOpen && _tab == EditTab.Markup && !_slideshow;

        private EditState DisplayState => _liveCrop is { } crop ? State with { Crop = crop } : State;

        private double RasterScale => XamlRoot?.RasterizationScale ?? 1.0;

        private void Canvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            // Bitmaps are created on the shared device; after a device loss reload the current image.
            if (args.Reason == CanvasCreateResourcesReason.NewDevice && _full != null && IsOpen)
            {
                DisposeBitmaps();
                _ = LoadIndexAsync(_index, checkUnsaved: false);
            }
        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ClampPan();
            UpdateZoomUi();
        }

        #region Transform & zoom

        private bool TryGetTransform(out ViewTransform transform)
        {
            transform = default;
            if (_full == null || _preview == null)
                return false;

            var source = ChooseSource();
            var sourceSize = new Size(source.SizeInPixels.Width, source.SizeInPixels.Height);
            var state = DisplayState;
            var imageSize = IsCropMode ? EditRenderer.GetTransformedSize(sourceSize, state) : EditRenderer.GetOutputSize(sourceSize, state);

            var viewW = Canvas.ActualWidth;
            var viewH = Canvas.ActualHeight;
            if (viewW <= 0 || viewH <= 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
                return false;

            var inset = RulerInset;
            var areaW = viewW - inset;
            var areaH = viewH - inset;
            var margin = IsCropMode ? 40 : _slideshow ? 0 : 16;
            var fit = Math.Min((areaW - 2 * margin) / imageSize.Width, (areaH - 2 * margin) / imageSize.Height);
            fit = Math.Max(fit, 0.001);
            var sourceToFull = (double)_full.SizeInPixels.Width / source.SizeInPixels.Width;
            var scale = _zoom is { } z && !IsCropMode ? z * sourceToFull : fit;

            var pan = IsCropMode ? Vector2.Zero : _pan;
            var offset = new Vector2(
                (float)(inset + areaW / 2 + pan.X - imageSize.Width * scale / 2),
                (float)(inset + areaH / 2 + pan.Y - imageSize.Height * scale / 2));

            transform = new ViewTransform(source, sourceSize, imageSize, scale, offset);
            return true;
        }

        private CanvasBitmap ChooseSource()
        {
            if (_full == null || _preview == null || ReferenceEquals(_full, _preview) || _zoom == null)
                return _preview!;
            var previewScale = _zoom.Value * _full.SizeInPixels.Width / _preview.SizeInPixels.Width * RasterScale;
            return previewScale > 1.05 ? _full : _preview;
        }

        /// <summary>Current zoom in DIPs per full-resolution pixel (fit resolved).</summary>
        private double GetEffectiveZoom()
        {
            if (_zoom is { } z)
                return z;
            if (!TryGetTransform(out var t) || _full == null)
                return 1;
            return t.Scale * t.Source.SizeInPixels.Width / _full.SizeInPixels.Width;
        }

        private void ResetView()
        {
            _zoom = null;
            _pan = Vector2.Zero;
            _liveCrop = null;
            _currentStroke = null;
            // The aspect lock belongs to the photo it was chosen for.
            _cropAspect = default;
        }

        private void ZoomToFit()
        {
            _zoom = null;
            _pan = Vector2.Zero;
            UpdateZoomUi();
            Canvas.Invalidate();
        }

        private void SetZoomPercent(double percent)
        {
            SetZoom(percent / 100 / RasterScale, null);
        }

        /// <summary>
        /// Zooms one step in or out (wheel notch, +/- keys, zoom buttons) and lands on round percentages:
        /// every 1 % below 10 %, every 10 % below 100 %, and from 100 % about 20 % per step rounded to 25 %.
        /// </summary>
        private void StepZoom(int direction, Point? anchor = null)
        {
            var current = GetEffectiveZoom() * RasterScale * 100;
            var belowHundred = direction > 0 ? current < 100 - 0.01 : current <= 100 + 0.01;
            var target = belowHundred
                ? (direction > 0 ? NextZoomPercentUp(current) : NextZoomPercentDown(current))
                : SnapZoomPercent(direction > 0 ? current * ZoomStepFactor : current / ZoomStepFactor);
            if (direction > 0 && target <= current + 0.01)
                target = NextZoomPercentUp(current);
            else if (direction < 0 && target >= current - 0.01)
                target = NextZoomPercentDown(current);
            SetZoom(target / 100 / RasterScale, anchor);
        }

        private const double ZoomStepFactor = 1.2;

        private static double ZoomGrid(double percent) => percent < 10 ? 1 : percent < 100 ? 10 : 25;

        private static double SnapZoomPercent(double percent)
        {
            var grid = ZoomGrid(percent);
            return Math.Max(1, Math.Round(percent / grid, MidpointRounding.AwayFromZero) * grid);
        }

        private static double NextZoomPercentUp(double percent)
        {
            var grid = ZoomGrid(percent);
            return Math.Floor(percent / grid + 1e-6) * grid + grid;
        }

        private static double NextZoomPercentDown(double percent)
        {
            var grid = ZoomGrid(percent - 1e-6);
            return Math.Max(1, Math.Ceiling(percent / grid - 1e-6) * grid - grid);
        }

        /// <summary>
        /// Sets the zoom, keeping the image point under <paramref name="anchor"/> (screen DIPs) stationary.
        /// </summary>
        private void SetZoom(double zoom, Point? anchor)
        {
            if (_full == null || IsCropMode)
                return;

            var before = TryGetTransform(out var t0) ? t0 : (ViewTransform?)null;
            var minZoom = 0.02;
            var maxZoom = 16.0 / RasterScale;
            _zoom = Math.Clamp(zoom, minZoom, maxZoom);

            if (before is { } t && TryGetTransform(out var t1))
            {
                var inset = RulerInset;
                var viewCenter = new Vector2((float)(inset + (Canvas.ActualWidth - inset) / 2), (float)(inset + (Canvas.ActualHeight - inset) / 2));
                var a = anchor is { } p ? new Vector2((float)p.X, (float)p.Y) : viewCenter;
                // Image point (in full-res pixels) under the anchor before zooming.
                var fullPerSourceBefore = (double)_full.SizeInPixels.Width / t.Source.SizeInPixels.Width;
                var imagePoint = (a - t.Offset) / (float)(t.Scale / fullPerSourceBefore);
                var newScaleFull = (float)_zoom.Value;
                var newOffset = a - imagePoint * newScaleFull;
                var imageScreen = new Vector2((float)(t1.ImageSize.Width * t1.Scale), (float)(t1.ImageSize.Height * t1.Scale));
                _pan = newOffset + imageScreen / 2 - viewCenter;
            }

            ClampPan();
            UpdateZoomUi();
            Canvas.Invalidate();
        }

        private void ClampPan()
        {
            if (!TryGetTransform(out var t))
                return;
            var imageW = t.ImageSize.Width * t.Scale;
            var imageH = t.ImageSize.Height * t.Scale;
            var areaW = Canvas.ActualWidth - RulerInset;
            var areaH = Canvas.ActualHeight - RulerInset;
            var maxX = Math.Max(0, (imageW - areaW) / 2 + 40);
            var maxY = Math.Max(0, (imageH - areaH) / 2 + 40);
            _pan = new Vector2(
                imageW <= areaW ? 0 : (float)Math.Clamp(_pan.X, -maxX, maxX),
                imageH <= areaH ? 0 : (float)Math.Clamp(_pan.Y, -maxY, maxY));
        }

        private void UpdateZoomUi()
        {
            var percent = GetEffectiveZoom() * RasterScale * 100;
            ZoomPercentButton.Content = _zoom == null
                ? $"{L("Photos_ZoomFitShort")} · {percent:0} %"
                : $"{percent:0} %";
            _syncingZoom = true;
            try
            {
                ZoomRange.Value = Math.Clamp(percent, ZoomRange.Minimum, ZoomRange.Maximum);
            }
            finally
            {
                _syncingZoom = false;
            }
            FitButton.IsEnabled = _zoom != null;
        }

        private void OnZoomRangeChanged()
        {
            if (_syncingZoom || _full == null)
                return;
            // The range may re-report (coerce) the value we just pushed; only react to real user changes.
            if (Math.Abs(ZoomRange.Value - GetEffectiveZoom() * RasterScale * 100) < 0.75)
                return;
            SetZoomPercent(ZoomRange.Value);
        }

        private void FitButton_Click(object sender, RoutedEventArgs e) => ZoomToFit();

        private void ZoomInButton_Click(object sender, RoutedEventArgs e) => StepZoom(1);

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => StepZoom(-1);

        #endregion

        #region Drawing

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (!TryGetTransform(out var t))
                return;

            var ds = args.DrawingSession;
            var state = DisplayState;
            var cropMode = IsCropMode;
            var interpolation = t.Scale < 1 ? CanvasImageInterpolation.HighQualityCubic : CanvasImageInterpolation.Linear;

            var geometry = EditRenderer.BuildGeometry(t.Source, t.SourceSize, state, applyCrop: !cropMode, CanvasImageInterpolation.Linear);
            var edited = EditRenderer.BuildAdjustments(geometry, state, t.ImageSize);
            var imageRect = new Rect(0, 0, t.ImageSize.Width, t.ImageSize.Height);

            ds.Transform = Matrix3x2.CreateScale((float)t.Scale) * Matrix3x2.CreateTranslation(t.Offset);

            if (_compareSplit && !cropMode)
            {
                var splitX = t.ImageSize.Width * _split;
                using (ds.CreateLayer(1f, new Rect(0, 0, splitX, t.ImageSize.Height)))
                    ds.DrawImage(geometry, 0, 0, imageRect, 1f, interpolation);
                using (ds.CreateLayer(1f, new Rect(splitX, 0, t.ImageSize.Width - splitX, t.ImageSize.Height)))
                {
                    ds.DrawImage(edited, 0, 0, imageRect, 1f, interpolation);
                    DrawAllStrokes(ds, t, state, cropMode);
                }
            }
            else
            {
                ds.DrawImage(edited, 0, 0, imageRect, 1f, interpolation);
                DrawAllStrokes(ds, t, state, cropMode);
            }

            ds.Transform = Matrix3x2.Identity;

            if (PhotosSettings.ShowGrid && !cropMode && !_slideshow)
                DrawGridlines(ds, t);
            if (cropMode)
                DrawCropOverlay(ds, t, state.Crop);
            if (_compareSplit && !cropMode)
                DrawSplitOverlay(ds, t);
            DrawNavigator(ds, t, edited);
            if (RulersVisible)
                DrawRulers(ds, t);
        }

        private void DrawAllStrokes(CanvasDrawingSession ds, ViewTransform t, EditState state, bool cropMode)
        {
            var strokes = state.Strokes;
            if (_currentStroke is { Count: > 0 } current)
            {
                strokes = strokes.Add(new MarkupStroke(_currentStrokeColor, (float)(_penSize / 1000), _highlighter, [.. current]));
            }
            if (strokes.IsEmpty)
                return;

            var transformed = EditRenderer.GetTransformedSize(t.SourceSize, state);
            var visible = cropMode ? EditState.FullCrop : state.Crop;
            EditRenderer.DrawStrokes(ds, strokes, transformed, visible, t.ImageSize);
        }

        private static void DrawCropOverlay(CanvasDrawingSession ds, ViewTransform t, Rect crop)
        {
            var r = t.ScreenRect;
            var c = new Rect(r.X + crop.X * r.Width, r.Y + crop.Y * r.Height, crop.Width * r.Width, crop.Height * r.Height);
            var shade = Color.FromArgb(150, 0, 0, 0);

            ds.FillRectangle(new Rect(r.X, r.Y, r.Width, Math.Max(0, c.Y - r.Y)), shade);
            ds.FillRectangle(new Rect(r.X, c.Bottom, r.Width, Math.Max(0, r.Bottom - c.Bottom)), shade);
            ds.FillRectangle(new Rect(r.X, c.Y, Math.Max(0, c.X - r.X), c.Height), shade);
            ds.FillRectangle(new Rect(c.Right, c.Y, Math.Max(0, r.Right - c.Right), c.Height), shade);

            var white = Color.FromArgb(255, 255, 255, 255);
            var grid = Color.FromArgb(90, 255, 255, 255);
            for (var i = 1; i < 3; i++)
            {
                var x = (float)(c.X + c.Width * i / 3);
                var y = (float)(c.Y + c.Height * i / 3);
                ds.DrawLine(x, (float)c.Y, x, (float)c.Bottom, grid, 1);
                ds.DrawLine((float)c.X, y, (float)c.Right, y, grid, 1);
            }
            ds.DrawRectangle(c, white, 1.5f);

            const float len = 20;
            const float thick = 4;
            var l = (float)c.X;
            var tp = (float)c.Y;
            var rt = (float)c.Right;
            var b = (float)c.Bottom;
            // Corners
            ds.FillRectangle(l - thick / 2, tp - thick / 2, len, thick, white);
            ds.FillRectangle(l - thick / 2, tp - thick / 2, thick, len, white);
            ds.FillRectangle(rt - len + thick / 2, tp - thick / 2, len, thick, white);
            ds.FillRectangle(rt - thick / 2, tp - thick / 2, thick, len, white);
            ds.FillRectangle(l - thick / 2, b - thick / 2, len, thick, white);
            ds.FillRectangle(l - thick / 2, b - len + thick / 2, thick, len, white);
            ds.FillRectangle(rt - len + thick / 2, b - thick / 2, len, thick, white);
            ds.FillRectangle(rt - thick / 2, b - len + thick / 2, thick, len, white);
            // Edge midpoints
            var mx = (l + rt) / 2;
            var my = (tp + b) / 2;
            ds.FillRectangle(mx - len / 2, tp - thick / 2, len, thick, white);
            ds.FillRectangle(mx - len / 2, b - thick / 2, len, thick, white);
            ds.FillRectangle(l - thick / 2, my - len / 2, thick, len, white);
            ds.FillRectangle(rt - thick / 2, my - len / 2, thick, len, white);
        }

        private void DrawSplitOverlay(CanvasDrawingSession ds, ViewTransform t)
        {
            var r = t.ScreenRect;
            var x = (float)(r.X + r.Width * _split);
            var top = (float)Math.Max(r.Y, 0);
            var bottom = (float)Math.Min(r.Bottom, Canvas.ActualHeight);
            ds.DrawLine(x, top, x, bottom, Color.FromArgb(220, 255, 255, 255), 2);
            var cy = (top + bottom) / 2;
            ds.FillCircle(x, cy, 16, Color.FromArgb(230, 255, 255, 255));
            ds.DrawLine(x - 6, cy - 5, x - 10, cy, Color.FromArgb(255, 30, 30, 30), 2);
            ds.DrawLine(x - 10, cy, x - 6, cy + 5, Color.FromArgb(255, 30, 30, 30), 2);
            ds.DrawLine(x + 6, cy - 5, x + 10, cy, Color.FromArgb(255, 30, 30, 30), 2);
            ds.DrawLine(x + 10, cy, x + 6, cy + 5, Color.FromArgb(255, 30, 30, 30), 2);

            using var format = new CanvasTextFormat { FontSize = 13, FontWeight = FontWeights.SemiBold };
            DrawLabel(ds, L("Photos_Before"), new Vector2(x - 12, top + 12), format, rightAligned: true);
            DrawLabel(ds, L("Photos_After"), new Vector2(x + 12, top + 12), format, rightAligned: false);
        }

        private static void DrawLabel(CanvasDrawingSession ds, string text, Vector2 position, CanvasTextFormat format, bool rightAligned)
        {
            using var layout = new CanvasTextLayout(ds, text, format, 400, 40);
            var w = (float)layout.LayoutBounds.Width;
            var h = (float)layout.LayoutBounds.Height;
            var x = rightAligned ? position.X - w - 16 : position.X;
            ds.FillRoundedRectangle(x, position.Y, w + 16, h + 8, 10, 10, Color.FromArgb(160, 0, 0, 0));
            ds.DrawTextLayout(layout, x + 8, position.Y + 4, Color.FromArgb(255, 255, 255, 255));
        }

        private void ToggleCompare()
        {
            _compareSplit = !_compareSplit;
            _split = 0.5;
            // Comparing only makes sense with edits at hand: show the edit panel alongside the split.
            if (_compareSplit && !_panelOpen && !_slideshow)
                SetPanelOpen(true, _lastEditTab);
            CompareButton.Variant = _compareSplit ? DaisyButtonVariant.Primary : DaisyButtonVariant.Ghost;
            CompareButton.ButtonStyle = _compareSplit ? DaisyButtonStyle.Soft : DaisyButtonStyle.Default;
            Canvas.Invalidate();
        }

        #endregion

        #region Pointer interaction

        private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Focus(FocusState.Pointer);
            if (_slideshow)
            {
                StopSlideshow();
                e.Handled = true;
                return;
            }

            if (!TryGetTransform(out var t))
                return;

            var point = e.GetCurrentPoint(Canvas);
            var p = point.Position;
            _pointerStart = p;
            var right = point.Properties.IsRightButtonPressed;

            var press = ViewerMath.ChoosePress(new CanvasPressInput(
                Left: point.Properties.IsLeftButtonPressed,
                Right: right,
                Middle: point.Properties.IsMiddleButtonPressed,
                Eyedropper: IsEyedropperActive,
                OnNavigator: IsInNavigator(t, p),
                CropMode: IsCropMode,
                OnSplitHandle: IsOnSplitHandle(t, p),
                MarkupMode: IsMarkupMode,
                ImageLargerThanView: t.ScreenRect.Width > Canvas.ActualWidth || t.ScreenRect.Height > Canvas.ActualHeight));

            switch (press)
            {
                case CanvasPress.PickColor:
                    // Left click picks the primary color, right click the secondary color.
                    if (SampleColor(t, p) is { } sampled)
                    {
                        if (right)
                            SetSecondaryColor(sampled);
                        else
                            SetPenColor(sampled);
                        SetEyedropper(false);
                    }
                    e.Handled = true;
                    break;

                case CanvasPress.Navigator:
                    _draggingNavigator = Canvas.CapturePointer(e.Pointer);
                    PanFromNavigator(t, p);
                    e.Handled = true;
                    break;

                case CanvasPress.Crop:
                    _cropHandle = HitTestCrop(t, State.Crop, p);
                    if (_cropHandle != CropHandle.None)
                    {
                        _cropStart = State.Crop;
                        _liveCrop = State.Crop;
                        Canvas.CapturePointer(e.Pointer);
                        e.Handled = true;
                    }
                    break;

                case CanvasPress.Split:
                    _draggingSplit = true;
                    Canvas.CapturePointer(e.Pointer);
                    e.Handled = true;
                    break;

                case CanvasPress.Markup:
                    if (ScreenToTransformedNormalized(t, p) is { } n)
                    {
                        // Like Paint Shop Pro: the right mouse button draws with the secondary color.
                        _currentStrokeColor = right ? _secondaryColor : _penColor;
                        _currentStroke = [n];
                        Canvas.CapturePointer(e.Pointer);
                        Canvas.Invalidate();
                        e.Handled = true;
                    }
                    break;

                case CanvasPress.Pan:
                    _panning = true;
                    _panStart = _pan;
                    Canvas.CapturePointer(e.Pointer);
                    CanvasHost.SetCursor(InputSystemCursorShape.SizeAll);
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>Whether <paramref name="p"/> is on the before/after divider; false while the split is not shown.</summary>
        private bool IsOnSplitHandle(ViewTransform t, Point p) =>
            _compareSplit && !IsCropMode && ViewerMath.IsOnSplitHandle(t.ScreenRect.X, t.ScreenRect.Width, _split, p.X);

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!TryGetTransform(out var t))
                return;
            var p = e.GetCurrentPoint(Canvas).Position;
            _lastPointer = p;
            if (RulersVisible)
                Canvas.Invalidate();

            if (_draggingNavigator)
            {
                PanFromNavigator(t, p);
                return;
            }

            if (_cropHandle != CropHandle.None)
            {
                var dx = (p.X - _pointerStart.X) / t.ScreenRect.Width;
                var dy = (p.Y - _pointerStart.Y) / t.ScreenRect.Height;
                _liveCrop = ResizeCrop(_cropStart, _cropHandle, dx, dy, t.ImageSize);
                Canvas.Invalidate();
                return;
            }

            if (_currentStroke != null)
            {
                if (ScreenToTransformedNormalized(t, p, clamp: true) is { } n)
                {
                    var last = _currentStroke[^1];
                    var minDistance = 1.5 / Math.Max(t.ScreenRect.Width, 1);
                    if (Vector2.Distance(last, n) > minDistance)
                    {
                        _currentStroke.Add(n);
                        Canvas.Invalidate();
                    }
                }
                return;
            }

            if (_draggingSplit)
            {
                _split = Math.Clamp((p.X - t.ScreenRect.X) / t.ScreenRect.Width, 0.02, 0.98);
                Canvas.Invalidate();
                return;
            }

            if (_panning)
            {
                _pan = _panStart + new Vector2((float)(p.X - _pointerStart.X), (float)(p.Y - _pointerStart.Y));
                ClampPan();
                Canvas.Invalidate();
                return;
            }

            UpdateHoverCursor(t, p);
        }

        private void UpdateHoverCursor(ViewTransform t, Point p)
        {
            InputSystemCursorShape shape = InputSystemCursorShape.Arrow;
            if (IsCropMode)
            {
                shape = HitTestCrop(t, State.Crop, p) switch
                {
                    CropHandle.TopLeft or CropHandle.BottomRight => InputSystemCursorShape.SizeNorthwestSoutheast,
                    CropHandle.TopRight or CropHandle.BottomLeft => InputSystemCursorShape.SizeNortheastSouthwest,
                    CropHandle.Left or CropHandle.Right => InputSystemCursorShape.SizeWestEast,
                    CropHandle.Top or CropHandle.Bottom => InputSystemCursorShape.SizeNorthSouth,
                    CropHandle.Move => InputSystemCursorShape.SizeAll,
                    _ => InputSystemCursorShape.Arrow
                };
            }
            else if (IsOnSplitHandle(t, p))
            {
                // Same order as a press: the divider before drawing.
                shape = InputSystemCursorShape.SizeWestEast;
            }
            else if (IsMarkupMode)
            {
                shape = InputSystemCursorShape.Cross;
            }
            else if (t.ScreenRect.Width > Canvas.ActualWidth || t.ScreenRect.Height > Canvas.ActualHeight)
            {
                shape = InputSystemCursorShape.Hand;
            }
            CanvasHost.SetCursor(shape);
        }

        private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            EndPointerInteraction();
            Canvas.ReleasePointerCapture(e.Pointer);
        }

        private void Canvas_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndPointerInteraction();

        private void EndPointerInteraction()
        {
            _draggingNavigator = false;
            if (_cropHandle != CropHandle.None)
            {
                _cropHandle = CropHandle.None;
                if (_liveCrop is { } crop)
                {
                    _liveCrop = null;
                    ApplyState(State with { Crop = crop });
                    _history.Commit();
                }
            }

            if (_currentStroke is { Count: > 0 } stroke)
            {
                _currentStroke = null;
                ApplyState(State with
                {
                    Strokes = State.Strokes.Add(new MarkupStroke(_currentStrokeColor, (float)(_penSize / 1000), _highlighter, [.. stroke]))
                });
                AddRecentColor(_currentStrokeColor);
            }
            _currentStroke = null;

            _draggingSplit = false;
            if (_panning)
            {
                _panning = false;
                CanvasHost.SetCursor(InputSystemCursorShape.Hand);
            }
            Canvas.Invalidate();
        }

        private int _wheelZoomDelta;

        private async void Canvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (_slideshow || IsCropMode)
                return;

            var point = e.GetCurrentPoint(Canvas);
            var delta = point.Properties.MouseWheelDelta;
            var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            e.Handled = true;

            if (PhotosSettings.WheelNavigates && !ctrl)
            {
                await NavigateAsync(delta > 0 ? -1 : 1);
                return;
            }

            // One zoom step per wheel notch (120); precision touchpads send smaller deltas that add up.
            _wheelZoomDelta += delta;
            while (Math.Abs(_wheelZoomDelta) >= 120)
            {
                var direction = Math.Sign(_wheelZoomDelta);
                _wheelZoomDelta -= direction * 120;
                StepZoom(direction, point.Position);
            }
        }

        private void Canvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (IsCropMode || IsMarkupMode || _slideshow)
                return;
            if (_zoom == null)
                SetZoom(1 / RasterScale, e.GetPosition(Canvas));
            else
                ZoomToFit();
        }

        private Vector2? ScreenToTransformedNormalized(ViewTransform t, Point p, bool clamp = false)
        {
            var r = t.ScreenRect;
            var nx = (p.X - r.X) / r.Width;
            var ny = (p.Y - r.Y) / r.Height;
            if (!clamp && (nx < 0 || ny < 0 || nx > 1 || ny > 1))
                return null;
            nx = Math.Clamp(nx, 0, 1);
            ny = Math.Clamp(ny, 0, 1);

            // Displayed image is the cropped output (outside crop mode); map back to the uncropped transformed space.
            var crop = IsCropMode ? EditState.FullCrop : DisplayState.Crop;
            return new Vector2((float)(crop.X + nx * crop.Width), (float)(crop.Y + ny * crop.Height));
        }

        private static CropHandle HitTestCrop(ViewTransform t, Rect crop, Point p)
        {
            var r = t.ScreenRect;
            var l = r.X + crop.X * r.Width;
            var tp = r.Y + crop.Y * r.Height;
            var rt = l + crop.Width * r.Width;
            var b = tp + crop.Height * r.Height;

            bool Near(double a, double v) => Math.Abs(a - v) <= HandleHitRadius;
            var nearL = Near(p.X, l);
            var nearR = Near(p.X, rt);
            var nearT = Near(p.Y, tp);
            var nearB = Near(p.Y, b);
            var withinX = p.X >= l - HandleHitRadius && p.X <= rt + HandleHitRadius;
            var withinY = p.Y >= tp - HandleHitRadius && p.Y <= b + HandleHitRadius;

            if (nearL && nearT) return CropHandle.TopLeft;
            if (nearR && nearT) return CropHandle.TopRight;
            if (nearL && nearB) return CropHandle.BottomLeft;
            if (nearR && nearB) return CropHandle.BottomRight;
            if (nearT && withinX) return CropHandle.Top;
            if (nearB && withinX) return CropHandle.Bottom;
            if (nearL && withinY) return CropHandle.Left;
            if (nearR && withinY) return CropHandle.Right;
            if (p.X > l && p.X < rt && p.Y > tp && p.Y < b) return CropHandle.Move;
            return CropHandle.None;
        }

        private Rect ResizeCrop(Rect start, CropHandle handle, double dx, double dy, Size transformedSize)
        {
            const double minSize = 0.04;
            double left = start.X, top = start.Y, right = start.Right, bottom = start.Bottom;

            if (handle == CropHandle.Move)
            {
                var x = Math.Clamp(start.X + dx, 0, 1 - start.Width);
                var y = Math.Clamp(start.Y + dy, 0, 1 - start.Height);
                return new Rect(x, y, start.Width, start.Height);
            }

            if (handle is CropHandle.Left or CropHandle.TopLeft or CropHandle.BottomLeft)
                left = Math.Clamp(start.X + dx, 0, right - minSize);
            if (handle is CropHandle.Right or CropHandle.TopRight or CropHandle.BottomRight)
                right = Math.Clamp(start.Right + dx, left + minSize, 1);
            if (handle is CropHandle.Top or CropHandle.TopLeft or CropHandle.TopRight)
                top = Math.Clamp(start.Y + dy, 0, bottom - minSize);
            if (handle is CropHandle.Bottom or CropHandle.BottomLeft or CropHandle.BottomRight)
                bottom = Math.Clamp(start.Bottom + dy, top + minSize, 1);

            var rect = new Rect(left, top, right - left, bottom - top);
            return _cropAspect.AspectAt(State.Rotation) is { } aspect ? EnforceAspect(rect, handle, aspect, transformedSize) : rect;
        }

        /// <summary>
        /// Adjusts a normalized crop rect to an output aspect ratio (width / height in pixels).
        /// The edge or corner opposite to the dragged handle stays fixed.
        /// </summary>
        private static Rect EnforceAspect(Rect rect, CropHandle handle, double aspect, Size transformedSize)
        {
            // Ratio of normalized width to normalized height that yields the requested pixel aspect.
            var ratio = aspect * transformedSize.Height / transformedSize.Width;
            var w = rect.Width;
            var h = rect.Height;

            if (handle is CropHandle.Top or CropHandle.Bottom)
                w = h * ratio;
            else
                h = w / ratio;

            // Fit into the unit square.
            if (w > 1) { w = 1; h = w / ratio; }
            if (h > 1) { h = 1; w = h * ratio; }

            double x, y;
            switch (handle)
            {
                case CropHandle.TopLeft:
                    x = rect.Right - w; y = rect.Bottom - h;
                    break;
                case CropHandle.TopRight:
                    x = rect.X; y = rect.Bottom - h;
                    break;
                case CropHandle.BottomLeft:
                    x = rect.Right - w; y = rect.Y;
                    break;
                case CropHandle.Left:
                case CropHandle.Right:
                    x = handle == CropHandle.Left ? rect.Right - w : rect.X;
                    y = rect.Y + (rect.Height - h) / 2;
                    break;
                case CropHandle.Top:
                case CropHandle.Bottom:
                    x = rect.X + (rect.Width - w) / 2;
                    y = handle == CropHandle.Top ? rect.Bottom - h : rect.Y;
                    break;
                default:
                    x = rect.X; y = rect.Y;
                    break;
            }

            // Shrink if pushed outside, keeping the aspect.
            if (x < 0) { w += x; x = 0; h = w / ratio; }
            if (y < 0) { h += y; y = 0; w = h * ratio; }
            if (x + w > 1) { w = 1 - x; h = w / ratio; }
            if (y + h > 1) { h = 1 - y; w = h * ratio; }

            return new Rect(x, y, Math.Max(0.02, w), Math.Max(0.02, h));
        }

        /// <summary>
        /// Largest centered crop with the given output aspect inside the current crop.
        /// </summary>
        private Rect CreateAspectCrop(double? aspect)
        {
            if (aspect is not { } a || _full == null)
                return State.Crop;
            var transformed = EditRenderer.GetTransformedSize(new Size(_full.SizeInPixels.Width, _full.SizeInPixels.Height), State);
            var ratio = a * transformed.Height / transformed.Width;
            var current = State.Crop;
            var w = current.Width;
            var h = w / ratio;
            if (h > current.Height)
            {
                h = current.Height;
                w = h * ratio;
            }
            return new Rect(current.X + (current.Width - w) / 2, current.Y + (current.Height - h) / 2, w, h);
        }

        #endregion
    }
}
