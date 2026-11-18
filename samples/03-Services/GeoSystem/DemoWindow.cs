using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace BlackThunder.UniEngine.Samples.GeoSystem
{
    internal sealed class DemoWindow : Window
    {
        // Internal
        private readonly GeoDemoScene _scene = new();
        private readonly GraphViewport _viewport;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33d) };
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private TimeSpan _previousTime;
        private bool _refreshing;
        private readonly Button _play = new();
        private readonly Slider _progress = new()
        {
            Minimum = 0d,
            Maximum = 1d,
            SmallChange = 0.01d,
            LargeChange = 0.1d
        };
        private readonly Slider _start = new()
        {
            Minimum = 0d,
            Maximum = 1d,
            SmallChange = 0.01d,
            LargeChange = 0.1d
        };
        private readonly Slider _end = new()
        {
            Minimum = 0d,
            Maximum = 1d,
            SmallChange = 0.01d,
            LargeChange = 0.1d
        };
        private readonly Slider _speed = new()
        {
            Minimum = 0.25d,
            Maximum = 5d,
            SmallChange = 0.25d,
            LargeChange = 0.5d
        };
        private readonly CheckBox _reverse = new() { Content = "역방향 조회  ·  To → From" };
        private readonly TextBlock _progressLabel = new();
        private readonly TextBlock _startLabel = new();
        private readonly TextBlock _endLabel = new();
        private readonly TextBlock _speedLabel = new();
        private readonly TextBlock _selection = new();
        private readonly TextBlock _routeStats = new();
        private readonly TextBlock _clipStats = new();
        private readonly TextBlock _pose = new();
        private readonly TextBlock _eventLog = new();
        private static readonly IBrush _muted = new SolidColorBrush(Color.Parse("#97A9BE"));
        private static readonly IBrush _panel = new SolidColorBrush(Color.Parse("#182538"));
        private static readonly IBrush _border = new SolidColorBrush(Color.Parse("#2B3D53"));


        // Content
        public DemoWindow()
        {
            Title = "UniEngine · GeoSystem Lab";
            Width = 1280;
            Height = 860;
            MinWidth = 1060;
            MinHeight = 740;
            Background = new SolidColorBrush(Color.Parse("#0B1422"));
            FontFamily = new FontFamily("Segoe UI, Malgun Gothic");
            FontSize = 13d;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _viewport = new GraphViewport(_scene);

            var root = new Grid
            {
                Margin = new Thickness(24d, 20d, 24d, 14d),
                RowDefinitions = new RowDefinitions("72,*,30")
            };
            var heading = new StackPanel { Spacing = 5d };
            heading.Children.Add(new TextBlock
            {
                Text = "GeoSystem Lab",
                FontSize = 27d,
                FontWeight = FontWeight.SemiBold
            });
            heading.Children.Add(new TextBlock
            {
                Text = "노드를 움직이고, 구간 위의 위치와 방향이 함께 바뀌는 모습을 확인하세요.",
                Foreground = _muted
            });
            root.Children.Add(heading);
            var openTracer = new Button
            {
                Content = "Tracer와 Wheel 열기",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            openTracer.Click += (_, _) => new TracerWindow().Show(this);
            root.Children.Add(openTracer);

            var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,20,304") };
            Grid.SetRow(body, 1);
            root.Children.Add(body);
            var graphCard = new Grid { RowDefinitions = new RowDefinitions("*,48") };
            graphCard.Children.Add(_viewport);
            var legend = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 22d,
                Margin = new Thickness(18d, 0d),
                VerticalAlignment = VerticalAlignment.Center
            };
            legend.Children.Add(ColoredLabel("●  Node", "#E9F1FA"));
            legend.Children.Add(ColoredLabel("━  Route", "#64CDE5"));
            legend.Children.Add(ColoredLabel("━  Clip", "#F5B866"));
            legend.Children.Add(ColoredLabel("◯  Hold", "#BFA3FF"));
            legend.Children.Add(ColoredLabel("▶  Traveller", "#E9F1FA"));
            Grid.SetRow(legend, 1);
            graphCard.Children.Add(legend);
            body.Children.Add(new Border
            {
                Background = _panel,
                BorderBrush = _border,
                BorderThickness = new Thickness(1d),
                CornerRadius = new CornerRadius(10d),
                ClipToBounds = true,
                Child = graphCard
            });

            var sidebar = new StackPanel { Spacing = 12d };
            var controls = new StackPanel { Spacing = 7d };
            controls.Children.Add(SectionTitle("ROUTE  /  경로 따라가기"));
            var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*") };
            _play.HorizontalAlignment = HorizontalAlignment.Stretch;
            _play.Click += (_, _) => _scene.SetPlaying(!_scene.IsPlaying);
            actions.Children.Add(_play);
            var reset = new Button { Content = "처음으로", HorizontalAlignment = HorizontalAlignment.Stretch };
            reset.Click += (_, _) => { _scene.Reset(); _viewport.FitToScene(); };
            Grid.SetColumn(reset, 2);
            actions.Children.Add(reset);
            controls.Children.Add(actions);
            controls.Children.Add(_progressLabel);
            controls.Children.Add(_progress);
            controls.Children.Add(_reverse);
            controls.Children.Add(_speedLabel);
            controls.Children.Add(_speed);
            controls.Children.Add(_routeStats);
            controls.Children.Add(_pose);
            sidebar.Children.Add(Card(controls));

            var clip = new StackPanel { Spacing = 6d };
            clip.Children.Add(SectionTitle("CLIP  /  B–C의 부분 구간"));
            clip.Children.Add(_startLabel);
            clip.Children.Add(_start);
            clip.Children.Add(_endLabel);
            clip.Children.Add(_end);
            clip.Children.Add(_clipStats);
            clip.Children.Add(new TextBlock
            {
                Text = "시작 핀이 끝 핀을 지나면 Clip 방향이 반전됩니다.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11d,
                Foreground = _muted
            });
            sidebar.Children.Add(Card(clip));

            var selected = new StackPanel { Spacing = 8d };
            selected.Children.Add(SectionTitle("NODE  /  선택한 노드"));
            selected.Children.Add(_selection);
            var fit = new Button { Content = "전체 보기  ·  F", HorizontalAlignment = HorizontalAlignment.Stretch };
            fit.Click += (_, _) => _viewport.FitToScene();
            selected.Children.Add(fit);
            sidebar.Children.Add(Card(selected));

            var events = new StackPanel { Spacing = 8d };
            events.Children.Add(SectionTitle("EVENTS  /  최근 변경"));
            _eventLog.FontFamily = new FontFamily("Consolas");
            _eventLog.FontSize = 10d;
            _eventLog.Foreground = _muted;
            _eventLog.TextWrapping = TextWrapping.Wrap;
            events.Children.Add(_eventLog);
            sidebar.Children.Add(Card(events));
            var scroll = new ScrollViewer
            {
                Content = sidebar,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetColumn(scroll, 2);
            body.Children.Add(scroll);

            var footer = new TextBlock
            {
                Text = "노드 드래그  ·  휠 확대/축소  ·  가운데 버튼으로 화면 이동  ·  화면에서 Space 재생/정지",
                FontSize = 11d,
                Foreground = _muted,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            Content = root;

            _progress.PropertyChanged += (_, e) =>
            {
                if (!_refreshing && e.Property == RangeBase.ValueProperty) _scene.SetProgress((float)_progress.Value);
            };
            _start.PropertyChanged += (_, e) =>
            {
                if (!_refreshing && e.Property == RangeBase.ValueProperty)
                    _scene.SetClipRate(true, (float)_start.Value);
            };
            _end.PropertyChanged += (_, e) =>
            {
                if (!_refreshing && e.Property == RangeBase.ValueProperty) _scene.SetClipRate(false, (float)_end.Value);
            };
            _speed.PropertyChanged += (_, e) =>
            {
                if (!_refreshing && e.Property == RangeBase.ValueProperty) _scene.SetSpeed((float)_speed.Value);
            };
            _reverse.IsCheckedChanged += (_, _) =>
            {
                if (!_refreshing) _scene.SetForward(_reverse.IsChecked != true);
            };
            _scene.Changed += Refresh;
            _viewport.SelectionChanged += Refresh;
            _timer.Tick += (_, _) =>
            {
                var now = _clock.Elapsed;
                var seconds = Math.Min(0.1d, (now - _previousTime).TotalSeconds);
                _previousTime = now;
                if (_scene.IsPlaying) _scene.Advance((float)seconds);
            };
            Opened += (_, _) => { _previousTime = _clock.Elapsed; _timer.Start(); };
            Closed += (_, _) =>
            {
                _timer.Stop();
                _scene.Changed -= Refresh;
                _viewport.SelectionChanged -= Refresh;
                _scene.Dispose();
            };
            Refresh();
        }

        private void Refresh()
        {
            _refreshing = true;
            try
            {
                _play.Content = _scene.IsPlaying ? "Ⅱ  일시정지" : "▶  재생";
                _progress.Value = _scene.Progress;
                _start.Value = _scene.ClipStart.Location.Distance;
                _end.Value = _scene.ClipEnd.Location.Distance;
                _speed.Value = _scene.Speed;
                _reverse.IsChecked = !_scene.IsForward;
                _progressLabel.Text = $"진행 위치  {_scene.Progress:P0}";
                _startLabel.Text = $"시작 핀  {_start.Value:P0}";
                _endLabel.Text = $"끝 핀  {_end.Value:P0}";
                _speedLabel.Text = $"이동 속도  {_scene.Speed:F2} unit/s";
                _routeStats.Text = $"전체 길이  {_scene.Route.Length:F2} unit";
                _clipStats.Text = _scene.Clip.IsValid
                    ? $"길이  {_scene.Clip.Length:F2} unit   ·   {(_scene.Clip.IsForward ? "정방향 →" : "역방향 ←")}"
                    : "구간이 너무 짧거나 두 핀이 겹칩니다.";
                var node = _scene.Nodes[_viewport.SelectedNodeIndex];
                _selection.Text = $"{(char)('A' + _viewport.SelectedNodeIndex)}"
                    + $"    X {node.Position.X:F2}    Y {node.Position.Y:F2}";
                _pose.Text = _scene.TryGetPose(out var pose)
                    ? $"X {pose.Position.X:F2}   Y {pose.Position.Y:F2}   /   {pose.Rotation:F1}°"
                    : "현재 위치를 계산할 수 없습니다.";
                _eventLog.Text = string.Join(Environment.NewLine, _scene.Events.Take(5));
                _viewport.InvalidateVisual();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private TextBlock SectionTitle(string text) =>
            new() { Text = text, FontSize = 11d, FontWeight = FontWeight.SemiBold, Foreground = _muted };

        private TextBlock ColoredLabel(string text, string color) =>
            new() { Text = text, FontSize = 11d, Foreground = new SolidColorBrush(Color.Parse(color)) };

        private Border Card(Control content) => new()
        {
            Background = _panel,
            BorderBrush = _border,
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d),
            Padding = new Thickness(16d, 13d),
            Child = content
        };
    }
}
