using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Vector2 = System.Numerics.Vector2;

namespace BlackThunder.UniEngine.Samples.GeoSystem
{
    internal sealed class GraphViewport : Control
    {
        // Front
        public int SelectedNodeIndex { get; private set; }
        public event Action SelectionChanged;

        // Internal
        private readonly GeoDemoScene _scene;
        private Point _origin;
        private double _scale = 45d;
        private bool _initialized;
        private int _dragging = -1;
        private Vector _dragOffset;
        private bool _panning;
        private Point _lastPointer;
        private static readonly IBrush _background = new SolidColorBrush(Color.Parse("#101B2B"));
        private static readonly IBrush _grid = new SolidColorBrush(Color.Parse("#1A2A3D"));
        private static readonly IBrush _axis = new SolidColorBrush(Color.Parse("#30455C"));
        private static readonly IBrush _text = new SolidColorBrush(Color.Parse("#E9F1FA"));
        private static readonly IBrush _muted = new SolidColorBrush(Color.Parse("#8499B1"));
        private static readonly IBrush _route = new SolidColorBrush(Color.Parse("#64CDE5"));
        private static readonly IBrush _clip = new SolidColorBrush(Color.Parse("#F5B866"));
        private static readonly IBrush _hold = new SolidColorBrush(Color.Parse("#BFA3FF"));
        private static readonly Typeface _typeface = new("Segoe UI");


        // Content
        public GraphViewport(GeoDemoScene scene)
        {
            _scene = scene;
            Focusable = true;
            ClipToBounds = true;
            MinWidth = 440;
            MinHeight = 360;
            SizeChanged += (_, _) =>
            {
                if (!_initialized) FitToScene();
            };
        }

        public void FitToScene()
        {
            if (Bounds.Width < 1d || Bounds.Height < 1d) return;
            var min = _scene.Nodes[0].Position;
            var max = min;
            foreach (var node in _scene.Nodes)
            {
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position);
            }
            var center = (min + max) / 2f;
            var width = Math.Max(1d, max.X - min.X);
            var height = Math.Max(1d, max.Y - min.Y);
            _scale = Math.Clamp(Math.Min((Bounds.Width - 140d) / width, (Bounds.Height - 180d) / height), 18d, 100d);
            _origin = new Point(Bounds.Width / 2d - center.X * _scale, Bounds.Height / 2d + center.Y * _scale);
            _initialized = true;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.FillRectangle(_background, new Rect(Bounds.Size));
            if (!_initialized || _scene.IsDisposed) return;
            var gridPen = new Pen(_grid, 1d);
            var axisPen = new Pen(_axis, 1d);
            for (var x = _origin.X % _scale; x < Bounds.Width; x += _scale)
                context.DrawLine(gridPen, new Point(x, 0), new Point(x, Bounds.Height));
            for (var y = _origin.Y % _scale; y < Bounds.Height; y += _scale)
                context.DrawLine(gridPen, new Point(0, y), new Point(Bounds.Width, y));
            context.DrawLine(axisPen, new Point(0, _origin.Y), new Point(Bounds.Width, _origin.Y));
            context.DrawLine(axisPen, new Point(_origin.X, 0), new Point(_origin.X, Bounds.Height));

            for (var i = 0; i < _scene.Segments.Count; i++)
            {
                var segment = _scene.Segments[i];
                var start = ToScreen(segment.From.Position);
                var end = ToScreen(segment.To.Position);
                context.DrawLine(new Pen(_route, 3d), start, end);
                var middle = new Point((start.X + end.X) / 2d, (start.Y + end.Y) / 2d);
                var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
                DrawArrow(context, middle, angle, _route, 6d);
                Label(context, $"S{i + 1}  {segment.Length:F2}", middle + new Vector(10d, -28d), _muted, 12d);
            }

            if (_scene.Clip.IsValid)
            {
                context.DrawLine(
                    new Pen(_clip, 8d),
                    ToScreen(_scene.ClipStart.Position),
                    ToScreen(_scene.ClipEnd.Position));
                var middle = (_scene.ClipStart.Position + _scene.ClipEnd.Position) / 2f;
                var direction = _scene.ClipEnd.Position - _scene.ClipStart.Position;
                DrawArrow(context, ToScreen(middle), Math.Atan2(-direction.Y, direction.X), _clip, 11d);
            }

            if (_scene.ClipStart.IsValid) DrawPin(context, _scene.ClipStart.Position, "From pin", -36d);
            if (_scene.ClipEnd.IsValid) DrawPin(context, _scene.ClipEnd.Position, "To pin", 14d);

            for (var i = 0; i < _scene.Nodes.Count; i++)
            {
                var point = ToScreen(_scene.Nodes[i].Position);
                if (i == SelectedNodeIndex) context.DrawEllipse(null, new Pen(_route, 2d), point, 19d, 19d);
                context.DrawEllipse(_background, new Pen(_text, 2d), point, 11d, 11d);
                Label(context, ((char)('A' + i)).ToString(), point + new Vector(-5d, -9d), _text, 12d);
                Label(
                    context,
                    $"{_scene.Nodes[i].Position.X:F1}, {_scene.Nodes[i].Position.Y:F1}",
                    point + new Vector(-24d, 25d),
                    _muted,
                    11d);
            }

            if (_scene.TryGetPose(out var pose))
            {
                var point = ToScreen(pose.Position);
                context.DrawEllipse(null, new Pen(_hold, 2d), ToScreen(_scene.Hold.Position), 18d, 18d);
                context.DrawEllipse(_background, null, point, 13d, 13d);
                DrawArrow(context, point, -pose.Rotation * Math.PI / 180d, _text, 12d);
            }

            Label(context, "WORLD SPACE  /  +Y UP", new Point(22d, 18d), _muted, 11d);
            Label(context, $"1 unit = {_scale:F0} px", new Point(22d, Bounds.Height - 32d), _muted, 11d);
        }

        private void DrawPin(DrawingContext context, Vector2 position, string name, double labelOffset)
        {
            var point = ToScreen(position);
            context.DrawEllipse(_clip, new Pen(_background, 2d), point, 6d, 6d);
            Label(context, name, point + new Vector(10d, labelOffset), _clip, 11d);
        }

        private void DrawArrow(DrawingContext context, Point point, double angle, IBrush brush, double radius)
        {
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                path.BeginFigure(point + new Vector(Math.Cos(angle) * radius, Math.Sin(angle) * radius), true);
                path.LineTo(point + new Vector(Math.Cos(angle + 2.5d) * radius, Math.Sin(angle + 2.5d) * radius));
                path.LineTo(point + new Vector(Math.Cos(angle - 2.5d) * radius, Math.Sin(angle - 2.5d) * radius));
                path.EndFigure(true);
            }
            context.DrawGeometry(brush, null, geometry);
        }

        private void Label(DrawingContext context, string text, Point position, IBrush brush, double size)
        {
            var formatted = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _typeface,
                size,
                brush);
            context.DrawText(formatted, position);
        }

        private Point ToScreen(Vector2 world) => new(_origin.X + world.X * _scale, _origin.Y - world.Y * _scale);
        private Vector2 ToWorld(Point screen) =>
            new((float)((screen.X - _origin.X) / _scale), (float)((_origin.Y - screen.Y) / _scale));

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus();
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsMiddleButtonPressed)
            {
                _panning = true;
                _lastPointer = point.Position;
                Cursor = new Cursor(StandardCursorType.SizeAll);
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
            if (!point.Properties.IsLeftButtonPressed) return;
            for (var i = _scene.Nodes.Count - 1; i >= 0; i--)
            {
                var nodePoint = ToScreen(_scene.Nodes[i].Position);
                var offset = nodePoint - point.Position;
                if (offset.X * offset.X + offset.Y * offset.Y > 400d) continue;
                SelectedNodeIndex = _dragging = i;
                _dragOffset = nodePoint - point.Position;
                Cursor = new Cursor(StandardCursorType.Hand);
                e.Pointer.Capture(this);
                SelectionChanged?.Invoke();
                InvalidateVisual();
                e.Handled = true;
                break;
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var point = e.GetPosition(this);
            if (_dragging >= 0) _scene.MoveNode(_dragging, ToWorld(point + _dragOffset));
            else if (_panning)
            {
                _origin += point - _lastPointer;
                _lastPointer = point;
                InvalidateVisual();
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _dragging = -1;
            _panning = false;
            Cursor = Cursor.Default;
            e.Pointer.Capture(null);
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            _dragging = -1;
            _panning = false;
            Cursor = Cursor.Default;
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            var point = e.GetPosition(this);
            var world = ToWorld(point);
            _scale = Math.Clamp(_scale * Math.Pow(1.12d, e.Delta.Y), 18d, 140d);
            _origin = new Point(point.X - world.X * _scale, point.Y + world.Y * _scale);
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.F) FitToScene();
            else if (e.Key == Key.Space) _scene.SetPlaying(!_scene.IsPlaying);
            else return;
            e.Handled = true;
        }
    }
}
