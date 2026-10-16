using System;
using System.Collections.Generic;
using Godot;
using Range = Godot.Range;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    // LineEdit / TextEdit callbacks
    private readonly List<(Action<string, LineEdit> Callback, HashSet<string>? Excludes)> _textChangedLineEditGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<string, LineEdit> Callback, HashSet<string>? Excludes)>> _textChangedLineEditTargetedCallbacks = new();
    
    private readonly List<(Action<TextEdit> Callback, HashSet<string>? Excludes)> _textChangedTextEditGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<TextEdit> Callback, HashSet<string>? Excludes)>> _textChangedTextEditTargetedCallbacks = new();
    
    private readonly List<(Action<string, LineEdit> Callback, HashSet<string>? Excludes)> _textSubmittedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<string, LineEdit> Callback, HashSet<string>? Excludes)>> _textSubmittedTargetedCallbacks = new();
    
    // Range callbacks
    private readonly List<(Action<double, Range> Callback, HashSet<string>? Excludes, Type? SourceType)> _valueChangedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<double, Range> Callback, HashSet<string>? Excludes)>> _valueChangedTargetedCallbacks = new();
    
    // ItemList / OptionButton callbacks
    private readonly List<(Action<int, Control> Callback, HashSet<string>? Excludes, Type? SourceType)> _itemSelectedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<int, Control> Callback, HashSet<string>? Excludes)>> _itemSelectedTargetedCallbacks = new();
    
    private readonly List<(Action<int, ItemList> Callback, HashSet<string>? Excludes)> _itemActivatedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<int, ItemList> Callback, HashSet<string>? Excludes)>> _itemActivatedTargetedCallbacks = new();
    
    // TabContainer / TabBar callbacks
    private readonly List<(Action<int, Control> Callback, HashSet<string>? Excludes, Type? SourceType)> _tabChangedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<int, Control> Callback, HashSet<string>? Excludes)>> _tabChangedTargetedCallbacks = new();
    
    // ColorPicker / ColorPickerButton callbacks
    private readonly List<(Action<Color, Control> Callback, HashSet<string>? Excludes, Type? SourceType)> _colorChangedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Color, Control> Callback, HashSet<string>? Excludes)>> _colorChangedTargetedCallbacks = new();
    
    // FileDialog callbacks
    private readonly List<(Action<string, FileDialog> Callback, HashSet<string>? Excludes)> _fileSelectedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<string, FileDialog> Callback, HashSet<string>? Excludes)>> _fileSelectedTargetedCallbacks = new();
    
    private readonly List<(Action<string, FileDialog> Callback, HashSet<string>? Excludes)> _dirSelectedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<string, FileDialog> Callback, HashSet<string>? Excludes)>> _dirSelectedTargetedCallbacks = new();
    
    // Track subscribed controls
    private readonly HashSet<LineEdit> _subscribedLineEdits = [];
    private readonly HashSet<TextEdit> _subscribedTextEdits = [];
    private readonly HashSet<Range> _subscribedRanges = [];
    private readonly HashSet<ItemList> _subscribedItemLists = [];
    private readonly HashSet<OptionButton> _subscribedOptionButtons = [];
    private readonly HashSet<TabContainer> _subscribedTabContainers = [];
    private readonly HashSet<TabBar> _subscribedTabBars = [];
    private readonly HashSet<ColorPicker> _subscribedColorPickers = [];
    private readonly HashSet<ColorPickerButton> _subscribedColorPickerButtons = [];
    private readonly HashSet<FileDialog> _subscribedFileDialogs = [];

    private void SetupControlCallbacks()
    {
        SetupTextChangedCallbacks();
        SetupTextSubmittedCallbacks();
        SetupValueChangedCallbacks();
        SetupItemSelectedCallbacks();
        SetupItemActivatedCallbacks();
        SetupTabChangedCallbacks();
        SetupColorChangedCallbacks();
        SetupFileDialogCallbacks();
        
        // Subscribe to existing nodes and watch for new ones
        var hasControlCallbacks = 
            _textChangedLineEditGlobalCallbacks.Count > 0 || _textChangedLineEditTargetedCallbacks.Count > 0 ||
            _textChangedTextEditGlobalCallbacks.Count > 0 || _textChangedTextEditTargetedCallbacks.Count > 0 ||
            _textSubmittedGlobalCallbacks.Count > 0 || _textSubmittedTargetedCallbacks.Count > 0 ||
            _valueChangedGlobalCallbacks.Count > 0 || _valueChangedTargetedCallbacks.Count > 0 ||
            _itemSelectedGlobalCallbacks.Count > 0 || _itemSelectedTargetedCallbacks.Count > 0 ||
            _itemActivatedGlobalCallbacks.Count > 0 || _itemActivatedTargetedCallbacks.Count > 0 ||
            _tabChangedGlobalCallbacks.Count > 0 || _tabChangedTargetedCallbacks.Count > 0 ||
            _colorChangedGlobalCallbacks.Count > 0 || _colorChangedTargetedCallbacks.Count > 0 ||
            _fileSelectedGlobalCallbacks.Count > 0 || _fileSelectedTargetedCallbacks.Count > 0 ||
            _dirSelectedGlobalCallbacks.Count > 0 || _dirSelectedTargetedCallbacks.Count > 0;
        
        if (!hasControlCallbacks) return;
        
        SubscribeToExistingControls(_root);
        GetTree().NodeAdded += OnNodeAddedForControls;
        GetTree().NodeRemoved += OnNodeRemovedForControls;
    }
    
    private void SubscribeToExistingControls(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (IsShielded(node)) continue;
            SubscribeControl(node);
            foreach (var child in node.GetChildren()) stack.Push(child);
        }
    }
    
    private void OnNodeAddedForControls(Node node)
    {
        if (IsHiddenFromRites(node)) return;
        SubscribeControl(node);
    }
    
    private void OnNodeRemovedForControls(Node node)
    {
        switch (node)
        {
            case LineEdit le: _subscribedLineEdits.Remove(le); break;
            case TextEdit te: _subscribedTextEdits.Remove(te); break;
            case Range r: _subscribedRanges.Remove(r); break;
            case ItemList il: _subscribedItemLists.Remove(il); break;
            case OptionButton ob: _subscribedOptionButtons.Remove(ob); break;
            case TabContainer tc: _subscribedTabContainers.Remove(tc); break;
            case TabBar tb: _subscribedTabBars.Remove(tb); break;
            case ColorPicker cp: _subscribedColorPickers.Remove(cp); break;
            case ColorPickerButton cpb: _subscribedColorPickerButtons.Remove(cpb); break;
            case FileDialog fd: _subscribedFileDialogs.Remove(fd); break;
        }
    }
    
    private void SubscribeControl(Node node)
    {
        var path = node.GetPath().ToString();

        switch (node)
        {
            case LineEdit lineEdit: SubscribeLineEdit(lineEdit, path); break;
            case TextEdit textEdit: SubscribeTextEdit(textEdit, path); break;
            case Range range: SubscribeRange(range, path); break;
            case OptionButton optionButton: SubscribeOptionButton(optionButton, path); break;
            case ItemList itemList: SubscribeItemList(itemList, path); break;
            case TabContainer tabContainer: SubscribeTabContainer(tabContainer, path); break;
            case TabBar tabBar: SubscribeTabBar(tabBar, path); break;
            case ColorPicker colorPicker: SubscribeColorPicker(colorPicker, path); break;
            case ColorPickerButton colorPickerButton: SubscribeColorPickerButton(colorPickerButton, path); break;
            case FileDialog fileDialog: SubscribeFileDialog(fileDialog, path); break;
        }
    }
}

