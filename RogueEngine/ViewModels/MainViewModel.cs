using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RogueEngine.Core.Engine;

namespace RogueEngine.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private int _cursorX = 0;
    private int _cursorY = 0;
    private string _consoleText = "[INFO] RogueEngine editor initialized.";
    private string _selectedItemName = "Player";
    private string _selectedGlyph = "@";
    private string _foregroundColor = "#FFFFFF";
    private string _backgroundColor = "#000000";
    private string _selectedNodeType = "SpawnEntity";
    private string _selectedNodeToAdd = "Start";
    private string _nodeSearchText = string.Empty;
    private string _selectedNodeCategory = "All";
    private NodeBrowserItem? _selectedNodeDefinition;
    private string _customFontPath = string.Empty;
    private string _nodeX = "12";
    private string _nodeY = "8";

    public MainViewModel()
    {
        SaveCommand = new RelayCommand(Save);
        PlayCommand = new RelayCommand(Play);

        var definitions = NodeFactory.AllDefinitions
            .Select(d => new NodeBrowserItem(d.Title, d.Category, d.Description))
            .ToList();

        AllNodeDefinitions = definitions;

        NodeCategories = ["All", .. definitions
            .Select(d => d.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)];

        AvailableNodeTypes = definitions
            .Select(d => d.Title)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        if (AvailableNodeTypes.Count > 0)
            _selectedNodeToAdd = AvailableNodeTypes[0];

        FilteredNodeDefinitions = new ObservableCollection<NodeBrowserItem>();
        UpdateNodeBrowserFilter();
        SelectedNodeDefinition = FilteredNodeDefinitions.FirstOrDefault();

        CanvasNodes = new ObservableCollection<ScriptCanvasNode>
        {
            new("Start", 120, 120),
            new("Create Map", 420, 120),
            new("Render Map", 740, 220)
        };

        CanvasConnections = new ObservableCollection<ScriptCanvasConnection>
        {
            new(CanvasNodes[0].Id, CanvasNodes[1].Id),
            new(CanvasNodes[1].Id, CanvasNodes[2].Id)
        };

        ProjectHierarchy = new ObservableCollection<HierarchyItem>
        {
            new("Project", [
                new HierarchyItem("Folders", [
                    new HierarchyItem("Assets"),
                    new HierarchyItem("Scripts"),
                    new HierarchyItem("Scenes")
                ]),
                new HierarchyItem("Maps", [
                    new HierarchyItem("Overworld"),
                    new HierarchyItem("Dungeon_01")
                ]),
                new HierarchyItem("Entities", [
                    new HierarchyItem("Player"),
                    new HierarchyItem("Goblin"),
                    new HierarchyItem("Chest")
                ])
            ])
        };
    }

    public ObservableCollection<HierarchyItem> ProjectHierarchy { get; }

    public ObservableCollection<ScriptCanvasNode> CanvasNodes { get; }

    public ObservableCollection<ScriptCanvasConnection> CanvasConnections { get; }

    public IReadOnlyList<NodeBrowserItem> AllNodeDefinitions { get; }

    public IReadOnlyList<string> NodeCategories { get; }

    public ObservableCollection<NodeBrowserItem> FilteredNodeDefinitions { get; }

    public IReadOnlyList<string> AvailableNodeTypes { get; }

    public ICommand PlayCommand { get; }

    public ICommand SaveCommand { get; }

    public int CursorX
    {
        get => _cursorX;
        set => SetField(ref _cursorX, value);
    }

    public int CursorY
    {
        get => _cursorY;
        set => SetField(ref _cursorY, value);
    }

    public string ConsoleText
    {
        get => _consoleText;
        set => SetField(ref _consoleText, value);
    }

    public string SelectedItemName
    {
        get => _selectedItemName;
        set => SetField(ref _selectedItemName, value);
    }

    public string SelectedGlyph
    {
        get => _selectedGlyph;
        set => SetField(ref _selectedGlyph, value);
    }

    public string ForegroundColor
    {
        get => _foregroundColor;
        set => SetField(ref _foregroundColor, value);
    }

    public string BackgroundColor
    {
        get => _backgroundColor;
        set => SetField(ref _backgroundColor, value);
    }

    public string SelectedNodeType
    {
        get => _selectedNodeType;
        set => SetField(ref _selectedNodeType, value);
    }

    public string SelectedNodeToAdd
    {
        get => _selectedNodeToAdd;
        set => SetField(ref _selectedNodeToAdd, value);
    }

    public string NodeSearchText
    {
        get => _nodeSearchText;
        set
        {
            if (SetField(ref _nodeSearchText, value))
                UpdateNodeBrowserFilter();
        }
    }

    public string SelectedNodeCategory
    {
        get => _selectedNodeCategory;
        set
        {
            if (SetField(ref _selectedNodeCategory, value))
                UpdateNodeBrowserFilter();
        }
    }

    public NodeBrowserItem? SelectedNodeDefinition
    {
        get => _selectedNodeDefinition;
        set
        {
            if (!SetField(ref _selectedNodeDefinition, value))
                return;

            if (value is not null)
                SelectedNodeToAdd = value.Title;
        }
    }

    public string CustomFontPath
    {
        get => _customFontPath;
        set => SetField(ref _customFontPath, value);
    }

    public string NodeX
    {
        get => _nodeX;
        set => SetField(ref _nodeX, value);
    }

    public string NodeY
    {
        get => _nodeY;
        set => SetField(ref _nodeY, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Save()
    {
        AppendConsole("[SAVE] Project saved.");
    }

    private void Play()
    {
        AppendConsole("[RUN] Play mode started.");
    }

    public void AppendConsole(string message)
    {
        ConsoleText += Environment.NewLine + message;
    }

    public void SelectNode(ScriptCanvasNode node)
    {
        SelectedItemName = node.Title;
        SelectedNodeType = node.Title;
        NodeX = node.X.ToString("0");
        NodeY = node.Y.ToString("0");
        SelectedGlyph = "@";
    }

    public ScriptCanvasNode AddNode(string title)
    {
        var node = new ScriptCanvasNode(title, 180 + CanvasNodes.Count * 40, 180 + CanvasNodes.Count * 28);
        CanvasNodes.Add(node);
        AppendConsole($"[NODE] Added '{title}'.");
        return node;
    }

    public ScriptCanvasNode AddSelectedNode()
    {
        var title = SelectedNodeDefinition?.Title;
        if (string.IsNullOrWhiteSpace(title))
            title = string.IsNullOrWhiteSpace(SelectedNodeToAdd) ? "Start" : SelectedNodeToAdd;

        return AddNode(title);
    }

    private void UpdateNodeBrowserFilter()
    {
        var search = NodeSearchText?.Trim() ?? string.Empty;
        var category = SelectedNodeCategory;

        var filtered = AllNodeDefinitions.Where(def =>
            (string.Equals(category, "All", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(def.Category, category, StringComparison.OrdinalIgnoreCase)) &&
            (search.Length == 0 ||
             def.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             def.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             def.Description.Contains(search, StringComparison.OrdinalIgnoreCase)));

        FilteredNodeDefinitions.Clear();
        foreach (var item in filtered)
            FilteredNodeDefinitions.Add(item);

        if (SelectedNodeDefinition is null || !FilteredNodeDefinitions.Contains(SelectedNodeDefinition))
            SelectedNodeDefinition = FilteredNodeDefinitions.FirstOrDefault();
    }

    public ScriptCanvasNode? DuplicateNode(Guid nodeId)
    {
        var original = CanvasNodes.FirstOrDefault(n => n.Id == nodeId);
        if (original is null)
            return null;

        var copy = new ScriptCanvasNode($"{original.Title} Copy", original.X + 40, original.Y + 40);
        CanvasNodes.Add(copy);
        AppendConsole($"[NODE] Duplicated '{original.Title}'.");
        return copy;
    }

    public bool RemoveNode(Guid nodeId)
    {
        var node = CanvasNodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null)
            return false;

        CanvasNodes.Remove(node);

        var linked = CanvasConnections
            .Where(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId)
            .ToList();
        foreach (var connection in linked)
            CanvasConnections.Remove(connection);

        AppendConsole($"[NODE] Deleted '{node.Title}'.");
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public sealed class NodeBrowserItem
{
    public NodeBrowserItem(string title, string category, string description)
    {
        Title = title;
        Category = category;
        Description = description;
    }

    public string Title { get; }

    public string Category { get; }

    public string Description { get; }

    public string DisplayLabel => $"{Title} ({Category})";
}

public sealed class ScriptCanvasNode
{
    public ScriptCanvasNode(string title, double x, double y)
    {
        Title = title;
        X = x;
        Y = y;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public string Title { get; set; }

    public double X { get; set; }

    public double Y { get; set; }
}

public sealed class ScriptCanvasConnection
{
    public ScriptCanvasConnection(Guid sourceNodeId, Guid targetNodeId)
    {
        SourceNodeId = sourceNodeId;
        TargetNodeId = targetNodeId;
    }

    public Guid SourceNodeId { get; }

    public Guid TargetNodeId { get; }
}

public sealed class HierarchyItem
{
    public HierarchyItem(string name, IEnumerable<HierarchyItem>? children = null)
    {
        Name = name;
        Children = children is null
            ? new ObservableCollection<HierarchyItem>()
            : new ObservableCollection<HierarchyItem>(children);
    }

    public string Name { get; }

    public ObservableCollection<HierarchyItem> Children { get; }
}
