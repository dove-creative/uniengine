using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using BlackThunder.GeoSystem.RouteSystem;

namespace BlackThunder.UniEngine.Samples.GeoSystem
{
    internal sealed class TracerWindow : Window
    {
        private readonly TracerDemoScene _scene = new();
        private readonly GraphViewport _viewport;
        private readonly TextBlock _relative = new();
        private readonly TextBlock _absolute = new();
        private readonly TextBlock _reservations = new() { TextWrapping = TextWrapping.Wrap };


        public TracerWindow()
        {
            Title = "GeoSystem · Tracer와 Wheel";
            Width = 1120;
            Height = 730;
            MinWidth = 880;
            MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brush.Parse("#0B1422");
            FontFamily = new FontFamily("Segoe UI, Malgun Gothic");
            _viewport = new GraphViewport(_scene.Graph, _scene);

            var layout = new Grid
            {
                Margin = new Thickness(20),
                RowDefinitions = new RowDefinitions("Auto,14,*,10,Auto"),
                ColumnDefinitions = new ColumnDefinitions("*,20,290")
            };
            var intro = new TextBlock
            {
                Text = "Tracer와 Wheel",
                FontSize = 25,
                FontWeight = FontWeight.SemiBold
            };
            layout.Children.Add(intro);
            Grid.SetRow(_viewport, 2);
            layout.Children.Add(_viewport);

            var panel = new StackPanel { Spacing = 9d, Margin = new Thickness(0, 0, 8, 0) };
            panel.Children.Add(Heading("○  RouteBased", "#93F0BC"));
            panel.Children.Add(_relative);
            panel.Children.Add(Heading("▶  Absolute", "#FFBD70"));
            panel.Children.Add(_absolute);
            panel.Children.Add(ActionButton("두 Tracer 1 unit 이동", () => _scene.Move(1f)));
            panel.Children.Add(ActionButton("두 Tracer의 Route 해제", _scene.Detach));
            panel.Children.Add(ActionButton("B–C 중간에 다시 배치", _scene.Place));
            panel.Children.Add(Heading("Absolute의 Wheel", "#E9F1FA"));
            panel.Children.Add(ActionButton("다음 분기 1번 예약  ·  C → E", _scene.ReserveBranch));
            panel.Children.Add(_reservations);
            var strict = new CheckBox { Content = "TrackingFailure로 이동 제한" };
            strict.IsCheckedChanged += (_, _) => _scene.SetFailureOption(strict.IsChecked == true);
            panel.Children.Add(strict);
            panel.Children.Add(new TextBlock
            {
                Text = "A를 드래그해 두 위치를 비교하세요. 분기를 보려면 Route를 해제한 뒤 C를 지나도록 이동하세요.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });
            var scroll = new ScrollViewer { Content = panel };
            Grid.SetRow(scroll, 2);
            Grid.SetColumn(scroll, 2);
            layout.Children.Add(scroll);
            var footer = new TextBlock
            {
                Text = "그래프에서 Space로 1 unit 이동  ·  F로 전체 보기  ·  휠로 확대/축소",
                Foreground = Brush.Parse("#A8BBD0")
            };
            Grid.SetRow(footer, 4);
            Grid.SetColumnSpan(footer, 3);
            layout.Children.Add(footer);
            Content = layout;

            _scene.Changed += Refresh;
            Closed += (_, _) =>
            {
                _scene.Changed -= Refresh;
                _scene.Dispose();
            };
            Refresh();
        }

        private static TextBlock Heading(string text, string color) => new()
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse(color),
            Margin = new Thickness(0, 4, 0, 0)
        };

        private static Button ActionButton(string caption, Action action)
        {
            var button = new Button { Content = caption, HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) => action();
            return button;
        }

        private void Refresh()
        {
            _relative.Text = Describe(_scene.Relative, _scene.RelativeMove);
            _absolute.Text = Describe(_scene.Absolute, _scene.AbsoluteMove);
            var queue = _scene.Absolute.Wheel.ReservedRoutes;
            _reservations.Text = queue.Count == 0 ? "예약 없음" : $"예약 순서  {string.Join(" → ", queue)}";
            _viewport.InvalidateVisual();
        }

        private string Describe(ITracer<Vector2, float> tracer, (bool Success, float Remaining)? last)
        {
            var state = $"{tracer.State}   ·   구간 {_scene.SegmentName(tracer.Segment)}";
            var pose = tracer.TryGetTransform(out var position, out var rotation)
                ? $"X {position.X:F2}   Y {position.Y:F2}   {rotation:F0}°" : "위치 없음";
            var result = last is { } move
                ? $"직전 이동 {(move.Success ? "성공" : "실패")}   ·   남은 거리 {move.Remaining:F2}" : "아직 이동하지 않음";
            return $"{state}\n{pose}\n{result}";
        }
    }
}
