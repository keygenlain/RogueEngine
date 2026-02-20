using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using RogueEngine.ViewModels;

namespace RogueEngine;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const double NodeWidth = 180;
    private const double NodeHeight = 96;
    private const double PinRadius = 6;

    private sealed record ConnectorInfo(Guid NodeId, bool IsInput);

    private readonly Dictionary<Guid, Border> _nodeViews = new();
    private ScriptCanvasNode? _draggingNode;
    private Point _dragOffset;
    private bool _isDraggingNode;

    private ScriptCanvasNode? _linkSourceNode;
    private bool _isConnecting;
    private Point _currentMousePoint;
    private bool _isPanning;
    private Point _panStart;

    private readonly ScaleTransform _zoomTransform = new(1, 1);
    private readonly TranslateTransform _panTransform = new(0, 0);

    private MainViewModel? Vm => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(_zoomTransform);
        transformGroup.Children.Add(_panTransform);
        GraphSurface.RenderTransform = transformGroup;

        RedrawCanvas();
    }

    private void AddNodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null)
            return;

        var node = Vm.AddSelectedNode();
        Vm.SelectNode(node);
        RedrawCanvas();
    }

    private void NodeBrowserListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        AddNodeButton_Click(sender, e);
    }

    private void RedrawCanvas()
    {
        if (Vm is null)
            return;

        _nodeViews.Clear();
        NodeCanvas.Children.Clear();
        ConnectionCanvas.Children.Clear();

        foreach (var node in Vm.CanvasNodes)
        {
            var view = BuildNodeView(node);
            _nodeViews[node.Id] = view;
            Canvas.SetLeft(view, node.X);
            Canvas.SetTop(view, node.Y);
            NodeCanvas.Children.Add(view);
        }

        RedrawConnections();
    }

    private Border BuildNodeView(ScriptCanvasNode node)
    {
        var card = new Border
        {
            Width = NodeWidth,
            Height = NodeHeight,
            Background = new SolidColorBrush(Color.FromRgb(32, 32, 40)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(84, 84, 96)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Tag = node.Id,
        };

        card.MouseLeftButtonDown += NodeCard_MouseLeftButtonDown;
        card.MouseLeftButtonUp += NodeCard_MouseLeftButtonUp;
        card.MouseRightButtonDown += NodeCard_MouseRightButtonDown;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(47, 76, 139)),
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Padding = new Thickness(8, 4, 8, 4)
        };
        header.Child = new TextBlock
        {
            Text = node.Title,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetRow(header, 0);

        var body = new Grid { Margin = new Thickness(8, 6, 8, 8) };
        body.Children.Add(new TextBlock
        {
            Text = $"({node.X:0}, {node.Y:0})",
            Foreground = new SolidColorBrush(Color.FromRgb(190, 190, 196)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var inputPin = new Ellipse
        {
            Width = PinRadius * 2,
            Height = PinRadius * 2,
            Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(-PinRadius, 0, 0, 0),
            Cursor = Cursors.Hand,
            Tag = new ConnectorInfo(node.Id, true)
        };
        inputPin.MouseLeftButtonUp += InputPin_MouseLeftButtonUp;

        var outputPin = new Ellipse
        {
            Width = PinRadius * 2,
            Height = PinRadius * 2,
            Fill = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, -PinRadius, 0),
            Cursor = Cursors.Cross,
            Tag = new ConnectorInfo(node.Id, false)
        };
        outputPin.MouseLeftButtonDown += OutputPin_MouseLeftButtonDown;

        body.Children.Add(inputPin);
        body.Children.Add(outputPin);

        Grid.SetRow(body, 1);
        grid.Children.Add(header);
        grid.Children.Add(body);
        card.Child = grid;

        var contextMenu = new ContextMenu();
        var duplicateItem = new MenuItem { Header = "Duplicate Node", Tag = node.Id };
        duplicateItem.Click += NodeDuplicateMenuItem_Click;
        var deleteItem = new MenuItem { Header = "Delete Node", Tag = node.Id };
        deleteItem.Click += NodeDeleteMenuItem_Click;
        contextMenu.Items.Add(duplicateItem);
        contextMenu.Items.Add(deleteItem);
        card.ContextMenu = contextMenu;

        return card;
    }

    private void NodeCard_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null)
            return;
        if (sender is not Border card)
            return;
        if (card.Tag is not Guid nodeId)
            return;

        var node = Vm.CanvasNodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null)
            return;

        Vm.SelectNode(node);
    }

    private void NodeCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null)
            return;
        if (sender is not Border card)
            return;
        if (card.Tag is not Guid nodeId)
            return;
        if (e.OriginalSource is Ellipse)
            return;

        _draggingNode = Vm.CanvasNodes.FirstOrDefault(n => n.Id == nodeId);
        if (_draggingNode is null)
            return;

        _isDraggingNode = true;
        var mouse = e.GetPosition(NodeCanvas);
        _dragOffset = new Point(mouse.X - _draggingNode.X, mouse.Y - _draggingNode.Y);

        Vm.SelectNode(_draggingNode);
        card.CaptureMouse();
        e.Handled = true;
    }

    private void NodeCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border card && card.IsMouseCaptured)
            card.ReleaseMouseCapture();

        _isDraggingNode = false;
        _draggingNode = null;
    }

    private void NodeCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (Vm is null)
            return;

        _currentMousePoint = e.GetPosition(NodeCanvas);
        Vm.CursorX = Math.Max(0, (int)_currentMousePoint.X);
        Vm.CursorY = Math.Max(0, (int)_currentMousePoint.Y);

        if (_isPanning)
        {
            var current = e.GetPosition(GraphViewport);
            var delta = current - _panStart;
            _panTransform.X += delta.X;
            _panTransform.Y += delta.Y;
            _panStart = current;
            return;
        }

        if (_isDraggingNode && _draggingNode is not null)
        {
            _draggingNode.X = Math.Max(0, _currentMousePoint.X - _dragOffset.X);
            _draggingNode.Y = Math.Max(0, _currentMousePoint.Y - _dragOffset.Y);

            if (_nodeViews.TryGetValue(_draggingNode.Id, out var view))
            {
                Canvas.SetLeft(view, _draggingNode.X);
                Canvas.SetTop(view, _draggingNode.Y);
            }

            Vm.SelectNode(_draggingNode);
            RedrawConnections();
        }
        else if (_isConnecting)
        {
            RedrawConnections();
        }
    }

    private void NodeCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingNode = false;
        _draggingNode = null;

        if (_isConnecting)
        {
            _isConnecting = false;
            _linkSourceNode = null;
            RedrawConnections();
        }
    }

    private void NodeCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
            return;

        _isPanning = true;
        _panStart = e.GetPosition(GraphViewport);
        NodeCanvas.CaptureMouse();
        Cursor = Cursors.Hand;
        e.Handled = true;
    }

    private void NodeCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
            return;

        _isPanning = false;
        if (NodeCanvas.IsMouseCaptured)
            NodeCanvas.ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    private void NodeCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var oldScale = _zoomTransform.ScaleX;
        var zoomDelta = e.Delta > 0 ? 1.1 : 0.9;
        var newScale = Math.Clamp(oldScale * zoomDelta, 0.35, 2.8);
        if (Math.Abs(newScale - oldScale) < 0.0001)
            return;

        var mouse = e.GetPosition(GraphViewport);
        _panTransform.X = mouse.X - (mouse.X - _panTransform.X) * (newScale / oldScale);
        _panTransform.Y = mouse.Y - (mouse.Y - _panTransform.Y) * (newScale / oldScale);
        _zoomTransform.ScaleX = newScale;
        _zoomTransform.ScaleY = newScale;
        e.Handled = true;
    }

    private void NodeDuplicateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null)
            return;
        if (sender is not MenuItem { Tag: Guid nodeId })
            return;

        var duplicated = Vm.DuplicateNode(nodeId);
        if (duplicated is null)
            return;

        Vm.SelectNode(duplicated);
        RedrawCanvas();
    }

    private void NodeDeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null)
            return;
        if (sender is not MenuItem { Tag: Guid nodeId })
            return;

        if (!Vm.RemoveNode(nodeId))
            return;

        RedrawCanvas();
    }

    private void OutputPin_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null)
            return;
        if (sender is not Ellipse pin)
            return;
        if (pin.Tag is not ConnectorInfo info)
            return;
        if (info.IsInput)
            return;

        _linkSourceNode = Vm.CanvasNodes.FirstOrDefault(n => n.Id == info.NodeId);
        if (_linkSourceNode is null)
            return;

        _isConnecting = true;
        _currentMousePoint = e.GetPosition(NodeCanvas);
        Vm.AppendConsole($"[LINK] Dragging from '{_linkSourceNode.Title}' output.");
        RedrawConnections();
        e.Handled = true;
    }

    private void InputPin_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null)
            return;
        if (!_isConnecting || _linkSourceNode is null)
            return;
        if (sender is not Ellipse pin)
            return;
        if (pin.Tag is not ConnectorInfo info)
            return;
        if (!info.IsInput)
            return;

        var targetNode = Vm.CanvasNodes.FirstOrDefault(n => n.Id == info.NodeId);
        if (targetNode is null || targetNode.Id == _linkSourceNode.Id)
        {
            _isConnecting = false;
            _linkSourceNode = null;
            RedrawConnections();
            return;
        }

        var exists = Vm.CanvasConnections.Any(c => c.SourceNodeId == _linkSourceNode.Id && c.TargetNodeId == targetNode.Id);
        if (!exists)
        {
            Vm.CanvasConnections.Add(new ScriptCanvasConnection(_linkSourceNode.Id, targetNode.Id));
            Vm.AppendConsole($"[LINK] Connected '{_linkSourceNode.Title}' -> '{targetNode.Title}'.");
        }

        _isConnecting = false;
        _linkSourceNode = null;
        RedrawConnections();
        e.Handled = true;
    }

    private void RedrawConnections()
    {
        if (Vm is null)
            return;

        ConnectionCanvas.Children.Clear();

        foreach (var connection in Vm.CanvasConnections)
        {
            var source = Vm.CanvasNodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
            var target = Vm.CanvasNodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);
            if (source is null || target is null)
                continue;

            var path = CreateBezierPath(GetOutputPoint(source), GetInputPoint(target), Brushes.DeepSkyBlue, 2.2);
            ConnectionCanvas.Children.Add(path);
        }

        if (_isConnecting && _linkSourceNode is not null)
        {
            var temp = CreateBezierPath(GetOutputPoint(_linkSourceNode), _currentMousePoint, Brushes.Orange, 2);
            temp.StrokeDashArray = [4, 4];
            ConnectionCanvas.Children.Add(temp);
        }
    }

    private static Point GetInputPoint(ScriptCanvasNode node)
    {
        return new Point(node.X, node.Y + NodeHeight / 2);
    }

    private static Point GetOutputPoint(ScriptCanvasNode node)
    {
        return new Point(node.X + NodeWidth, node.Y + NodeHeight / 2);
    }

    private static Path CreateBezierPath(Point start, Point end, Brush brush, double thickness)
    {
        var deltaX = Math.Abs(end.X - start.X);
        var controlOffset = Math.Max(60, deltaX * 0.5);

        var figure = new PathFigure
        {
            StartPoint = start,
            Segments =
            {
                new BezierSegment(
                    new Point(start.X + controlOffset, start.Y),
                    new Point(end.X - controlOffset, end.Y),
                    end,
                    true)
            }
        };

        return new Path
        {
            Data = new PathGeometry([figure]),
            Stroke = brush,
            StrokeThickness = thickness,
            SnapsToDevicePixels = true
        };
    }
}