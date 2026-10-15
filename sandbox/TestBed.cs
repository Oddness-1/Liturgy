using Liturgy;
using Godot;

[assembly: Rite.Settings(Debug = true)]

namespace Sandbox;

/// <summary>
/// Comprehensive test bed for Liturgy hook attributes.
/// 
/// Instructions:
/// 1. Open sandbox/project.godot and run it (TestBed.tscn is the main scene)
/// 2. Use keyboard shortcuts to test different hook categories:
///    - F1: Test Button hooks (pressed, toggled, up, down)
///    - F2: Test Control hooks (text, value, color, item selection)
///    - F3: Test Focus/Mouse hooks
///    - F4: Test Visibility hooks
///    - F5: Test Physics hooks (Area2D body/area enter/exit)
///    - F6: Test Timer hooks
///    - F7: Test Animation hooks
///    - F8: Test Audio hooks
///    - F9: Test Popup/Dialog hooks
///    - F10: Test Slider/Scroll/Container hooks
///    - F11: Test dynamically adding/removing nodes
///    - F12: Print test summary
///
/// The console will show which hooks fire and in what order.
/// </summary>
public partial class TestBed : Control
{
    private static int _testCounter;
    private static string _currentTestCategory = "";
    
    // Test nodes created dynamically
    private Button? _testButton;
    private CheckButton? _testCheckButton;
    private LineEdit? _testLineEdit;
    private SpinBox? _testSpinBox;
    private HSlider? _testSlider;
    private ColorPickerButton? _testColorPicker;
    private OptionButton? _testOptionButton;
    private ItemList? _testItemList;
    private TabBar? _testTabBar;
    private Timer? _testTimer;
    private AnimationPlayer? _testAnimPlayer;
    private AudioStreamPlayer? _testAudioPlayer;
    private AcceptDialog? _testDialog;
    private PopupMenu? _testPopup;
    private ScrollContainer? _testScrollContainer;
    private VBoxContainer? _testContainer;
    private Area2D? _testArea2D;
    private CharacterBody2D? _testBody2D;
    
    public override void _Ready()
    {
        Log("=== Liturgy Test Bed Initialized ===");
        Log(GetTree().Root.GetNodeOrNull("Liturgy") is null ? "Liturgy did NOT attach to /root" : "Liturgy attached to /root");
        Log("Press F1-F12 to run different test categories");
        Log("Press ESC to quit");
        Log("");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key) return;
        
        switch (key.Keycode)
        {
            case Key.F1: RunButtonTests(); break;
            case Key.F2: RunControlTests(); break;
            case Key.F3: RunFocusMouseTests(); break;
            case Key.F4: RunVisibilityTests(); break;
            case Key.F5: RunPhysicsTests(); break;
            case Key.F6: RunTimerTests(); break;
            case Key.F7: RunAnimationTests(); break;
            case Key.F8: RunAudioTests(); break;
            case Key.F9: RunPopupDialogTests(); break;
            case Key.F10: RunMiscControlTests(); break;
            case Key.F11: RunDynamicNodeTests(); break;
            case Key.F12: PrintSummary(); break;
            case Key.Escape: GetTree().Quit(); break;
        }
    }

    private void RunButtonTests()
    {
        StartCategory("Button Hooks");
        
        // Create test buttons if needed
        _testButton ??= CreateTestNode<Button>("TestButton");
        _testCheckButton ??= CreateTestNode<CheckButton>("TestCheckButton");
        
        Log("Simulating button press...");
        _testButton.EmitSignal(BaseButton.SignalName.Pressed);
        
        Log("Simulating button down...");
        _testButton.EmitSignal(BaseButton.SignalName.ButtonDown);
        
        Log("Simulating button up...");
        _testButton.EmitSignal(BaseButton.SignalName.ButtonUp);
        
        Log("Simulating toggle on...");
        _testCheckButton.EmitSignal(BaseButton.SignalName.Toggled, true);
        
        Log("Simulating toggle off...");
        _testCheckButton.EmitSignal(BaseButton.SignalName.Toggled, false);
        
        EndCategory();
    }

    private void RunControlTests()
    {
        StartCategory("Control Hooks (Text, Value, Color, Items)");
        
        _testLineEdit ??= CreateTestNode<LineEdit>("TestLineEdit");
        _testSpinBox ??= CreateTestNode<SpinBox>("TestSpinBox");
        _testColorPicker ??= CreateTestNode<ColorPickerButton>("TestColorPicker");
        _testOptionButton ??= CreateTestNode<OptionButton>("TestOptionButton");
        _testItemList ??= CreateTestNode<ItemList>("TestItemList");
        _testTabBar ??= CreateTestNode<TabBar>("TestTabBar");
        
        // Setup items for selection tests
        if (_testOptionButton.ItemCount == 0)
        {
            _testOptionButton.AddItem("Option A");
            _testOptionButton.AddItem("Option B");
        }
        if (_testItemList.ItemCount == 0)
        {
            _testItemList.AddItem("Item 1");
            _testItemList.AddItem("Item 2");
        }
        if (_testTabBar.TabCount == 0)
        {
            _testTabBar.AddTab("Tab 1");
            _testTabBar.AddTab("Tab 2");
        }
        
        Log("Simulating text changed...");
        _testLineEdit.EmitSignal(LineEdit.SignalName.TextChanged, "hello");
        
        Log("Simulating text submitted...");
        _testLineEdit.EmitSignal(LineEdit.SignalName.TextSubmitted, "submitted text");
        
        Log("Simulating value changed (SpinBox)...");
        _testSpinBox.EmitSignal(Range.SignalName.ValueChanged, 42.0);
        
        Log("Simulating color changed...");
        _testColorPicker.EmitSignal(ColorPickerButton.SignalName.ColorChanged, Colors.Red);
        
        Log("Simulating item selected (OptionButton)...");
        _testOptionButton.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
        
        Log("Simulating item selected (ItemList)...");
        _testItemList.EmitSignal(ItemList.SignalName.ItemSelected, 0L);
        
        Log("Simulating item activated (ItemList double-click)...");
        _testItemList.EmitSignal(ItemList.SignalName.ItemActivated, 0L);
        
        Log("Simulating tab changed...");
        _testTabBar.EmitSignal(TabBar.SignalName.TabChanged, 1L);
        
        EndCategory();
    }

    private void RunFocusMouseTests()
    {
        StartCategory("Focus/Mouse Hooks");
        
        _testButton ??= CreateTestNode<Button>("TestButton");
        
        Log("Simulating focus entered...");
        _testButton.EmitSignal(Control.SignalName.FocusEntered);
        
        Log("Simulating focus exited...");
        _testButton.EmitSignal(Control.SignalName.FocusExited);
        
        Log("Simulating mouse entered...");
        _testButton.EmitSignal(Control.SignalName.MouseEntered);
        
        Log("Simulating mouse exited...");
        _testButton.EmitSignal(Control.SignalName.MouseExited);
        
        Log("Simulating resized...");
        _testButton.EmitSignal(Control.SignalName.Resized);
        
        Log("Simulating minimum size changed...");
        _testButton.EmitSignal(Control.SignalName.MinimumSizeChanged);
        
        Log("Simulating GUI input...");
        var clickEvent = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true };
        _testButton.EmitSignal(Control.SignalName.GuiInput, clickEvent);
        
        EndCategory();
    }

    private void RunVisibilityTests()
    {
        StartCategory("Visibility Hooks");
        
        _testButton ??= CreateTestNode<Button>("TestButton");
        
        // Make sure it starts visible
        _testButton.Show();
        
        Log("Simulating visibility changed (hiding)...");
        _testButton.Hide();
        
        // Need a frame for the signal to process in some cases
        Callable.From(ContinueVisibilityTest).CallDeferred();
    }

    private void ContinueVisibilityTest()
    {
        Log("Simulating visibility changed (showing)...");
        _testButton!.Show();
        
        EndCategory();
    }

    private void RunPhysicsTests()
    {
        StartCategory("Physics Hooks (Area2D/3D)");
        
        // Create physics nodes
        if (_testArea2D is null)
        {
            _testArea2D = new Area2D { Name = "TestArea2D" };
            var collision = new CollisionShape2D();
            collision.Shape = new CircleShape2D { Radius = 50 };
            _testArea2D.AddChild(collision);
            AddChild(_testArea2D);
        }
        
        if (_testBody2D is null)
        {
            _testBody2D = new CharacterBody2D { Name = "TestBody2D" };
            var collision = new CollisionShape2D();
            collision.Shape = new CircleShape2D { Radius = 25 };
            _testBody2D.AddChild(collision);
            AddChild(_testBody2D);
        }
        
        Log("Simulating body entered Area2D...");
        _testArea2D.EmitSignal(Area2D.SignalName.BodyEntered, _testBody2D);
        
        Log("Simulating body exited Area2D...");
        _testArea2D.EmitSignal(Area2D.SignalName.BodyExited, _testBody2D);
        
        // Create another Area2D for area-to-area tests
        var otherArea = new Area2D { Name = "OtherArea2D" };
        AddChild(otherArea);
        
        Log("Simulating area entered Area2D...");
        _testArea2D.EmitSignal(Area2D.SignalName.AreaEntered, otherArea);
        
        Log("Simulating area exited Area2D...");
        _testArea2D.EmitSignal(Area2D.SignalName.AreaExited, otherArea);
        
        otherArea.QueueFree();
        
        EndCategory();
    }

    private void RunTimerTests()
    {
        StartCategory("Timer Hooks");
        
        if (_testTimer is null)
        {
            _testTimer = new Timer { Name = "TestTimer", OneShot = true };
            AddChild(_testTimer);
        }
        
        Log("Simulating timer timeout...");
        _testTimer.EmitSignal(Timer.SignalName.Timeout);
        
        EndCategory();
    }

    private void RunAnimationTests()
    {
        StartCategory("Animation Hooks");
        
        if (_testAnimPlayer is null)
        {
            _testAnimPlayer = new AnimationPlayer { Name = "TestAnimPlayer" };
            
            // Create a simple animation
            var animLib = new AnimationLibrary();
            var anim = new Animation();
            anim.Length = 0.1f;
            animLib.AddAnimation("test_anim", anim);
            _testAnimPlayer.AddAnimationLibrary("", animLib);
            
            AddChild(_testAnimPlayer);
        }
        
        Log("Simulating animation started...");
        _testAnimPlayer.EmitSignal(AnimationPlayer.SignalName.AnimationStarted, new StringName("test_anim"));
        
        Log("Simulating animation finished...");
        _testAnimPlayer.EmitSignal(AnimationPlayer.SignalName.AnimationFinished, new StringName("test_anim"));
        
        EndCategory();
    }

    private void RunAudioTests()
    {
        StartCategory("Audio Hooks");
        
        if (_testAudioPlayer is null)
        {
            _testAudioPlayer = new AudioStreamPlayer { Name = "TestAudioPlayer" };
            AddChild(_testAudioPlayer);
            
            // Wait a frame for Liturgy to subscribe before emitting
            Callable.From(ContinueAudioTest).CallDeferred();
            return;
        }
        
        ContinueAudioTest();
    }

    private void ContinueAudioTest()
    {
        Log("Simulating audio finished...");
        _testAudioPlayer!.EmitSignal(AudioStreamPlayer.SignalName.Finished);
        
        EndCategory();
    }

    private void RunPopupDialogTests()
    {
        StartCategory("Popup/Dialog Hooks");
        
        if (_testDialog is null)
        {
            _testDialog = new AcceptDialog { Name = "TestDialog", Title = "Test Dialog" };
            AddChild(_testDialog);
        }
        
        if (_testPopup is null)
        {
            _testPopup = new PopupMenu { Name = "TestPopup" };
            _testPopup.AddItem("Menu Item");
            AddChild(_testPopup);
        }
        
        Log("Simulating dialog confirmed...");
        _testDialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
        
        Log("Simulating dialog canceled...");
        _testDialog.EmitSignal(AcceptDialog.SignalName.Canceled);
        
        Log("Simulating close requested...");
        _testDialog.EmitSignal(Window.SignalName.CloseRequested);
        
        Log("Simulating popup hide...");
        _testPopup.EmitSignal(Popup.SignalName.PopupHide);
        
        EndCategory();
    }

    private void RunMiscControlTests()
    {
        StartCategory("Misc Control Hooks (Slider/Scroll/Container)");
        
        _testSlider ??= CreateTestNode<HSlider>("TestSlider");
        
        if (_testScrollContainer is null)
        {
            _testScrollContainer = new ScrollContainer { Name = "TestScrollContainer", CustomMinimumSize = new Vector2(100, 100) };
            var content = new Control { CustomMinimumSize = new Vector2(200, 200) };
            _testScrollContainer.AddChild(content);
            AddChild(_testScrollContainer);
        }
        
        if (_testContainer is null)
        {
            _testContainer = new VBoxContainer { Name = "TestContainer" };
            AddChild(_testContainer);
        }
        
        Log("Simulating drag started...");
        _testSlider.EmitSignal(Slider.SignalName.DragStarted);
        
        Log("Simulating drag ended (value changed)...");
        _testSlider.EmitSignal(Slider.SignalName.DragEnded, true);
        
        Log("Simulating drag ended (no change)...");
        _testSlider.EmitSignal(Slider.SignalName.DragEnded, false);
        
        Log("Simulating scroll started...");
        _testScrollContainer.EmitSignal(ScrollContainer.SignalName.ScrollStarted);
        
        Log("Simulating scroll ended...");
        _testScrollContainer.EmitSignal(ScrollContainer.SignalName.ScrollEnded);
        
        Log("Simulating sort children...");
        _testContainer.EmitSignal(Container.SignalName.SortChildren);
        
        EndCategory();
    }

    private void RunDynamicNodeTests()
    {
        StartCategory("Dynamic Node Add/Remove");
        
        Log("Adding a new Button dynamically...");
        var dynamicButton = new Button { Name = "DynamicButton" };
        AddChild(dynamicButton);
        
        // Give OnReady a frame to fire
        Callable.From(() => ContinueDynamicTest(dynamicButton)).CallDeferred();
    }

    private void ContinueDynamicTest(Button dynamicButton)
    {
        Log("Removing the dynamic Button...");
        dynamicButton.QueueFree();
        
        EndCategory();
    }

    [Rite.OnReady]
    private static void OnSceneReady(Window root) => LogHook("OnReady(Scene)", $"Root: {root.Name}");

    [Rite.OnReady(Mode = Rite.ReadyMode.PerNode)]
    private static void OnNodeReady(Node node) => LogHook("OnReady(PerNode)", node.Name);

    [Rite.OnNodeAdded]
    private static void OnNodeAdded(Node node) => LogHook("OnNodeAdded", node.Name);

    [Rite.OnNodeRemoved]
    private static void OnNodeRemoved(Node node) => LogHook("OnNodeRemoved", node.Name);

    [Rite.OnTreeChanged]
    private static void OnTreeChanged() => LogHook("OnTreeChanged", "");

    // Commented out to avoid spam - uncomment to test
    // [Rite.OnProcess]
    // private static void OnProcess(double delta) => LogHook("OnProcess", $"delta={delta:F4}");

    // [Rite.OnPhysicsProcess]
    // private static void OnPhysicsProcess(double delta) => LogHook("OnPhysicsProcess", $"delta={delta:F4}");

    [Rite.OnInput]
    private static void OnInput(InputEvent @event)
    {
        if (@event is InputEventKey key)
            LogHook("OnInput", $"Key: {key.Keycode}");
    }

    [Rite.OnUnhandledInput]
    private static void OnUnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key)
            LogHook("OnUnhandledInput", $"Key: {key.Keycode}");
    }

    [Rite.OnButtonPressed]
    private static void OnAnyButtonPressed(BaseButton button) => LogHook("OnButtonPressed", button.Name);

    [Rite.OnButtonUp]
    private static void OnAnyButtonUp(BaseButton button) => LogHook("OnButtonUp", button.Name);

    [Rite.OnButtonDown]
    private static void OnAnyButtonDown(BaseButton button) => LogHook("OnButtonDown", button.Name);

    [Rite.OnButtonToggled]
    private static void OnAnyButtonToggled(bool pressed, BaseButton button) => LogHook("OnButtonToggled", $"{button.Name} pressed={pressed}");

    [Rite.OnTextChanged]
    private static void OnTextChanged(string text, LineEdit lineEdit) => LogHook("OnTextChanged", $"{lineEdit.Name}: '{text}'");

    [Rite.OnTextSubmitted]
    private static void OnTextSubmitted(string text, LineEdit lineEdit) => LogHook("OnTextSubmitted", $"{lineEdit.Name}: '{text}'");

    [Rite.OnValueChanged]
    private static void OnValueChanged(double value, Range range) => LogHook("OnValueChanged", $"{range.Name}: {value}");

    [Rite.OnColorChanged]
    private static void OnColorChanged(Color color, ColorPickerButton picker) => LogHook("OnColorChanged", $"{picker.Name}: {color}");

    // Fixed: Use specific types, not Control
    [Rite.OnItemSelected]
    private static void OnOptionItemSelected(int index, OptionButton option) => LogHook("OnItemSelected(OptionButton)", $"{option.Name}: index={index}");

    [Rite.OnItemSelected]
    private static void OnListItemSelected(int index, ItemList list) => LogHook("OnItemSelected(ItemList)", $"{list.Name}: index={index}");

    // Fixed: Use int, not long
    [Rite.OnItemActivated]
    private static void OnItemActivated(int index, ItemList list) => LogHook("OnItemActivated", $"{list.Name}: index={index}");

    [Rite.OnTabChanged]
    private static void OnTabChanged(long tab, TabBar tabBar) => LogHook("OnTabChanged", $"{tabBar.Name}: tab={tab}");

    [Rite.OnFocusEntered]
    private static void OnFocusEntered(Control control) => LogHook("OnFocusEntered", control.Name);

    [Rite.OnFocusExited]
    private static void OnFocusExited(Control control) => LogHook("OnFocusExited", control.Name);

    [Rite.OnMouseEntered]
    private static void OnMouseEntered(Control control) => LogHook("OnMouseEntered", control.Name);

    [Rite.OnMouseExited]
    private static void OnMouseExited(Control control) => LogHook("OnMouseExited", control.Name);

    [Rite.OnResized]
    private static void OnResized(Control control) => LogHook("OnResized", control.Name);

    [Rite.OnMinimumSizeChanged]
    private static void OnMinimumSizeChanged(Control control) => LogHook("OnMinimumSizeChanged", control.Name);

    [Rite.OnGuiInput]
    private static void OnGuiInput(InputEvent @event, Control control) => LogHook("OnGuiInput", $"{control.Name}: {@event.GetType().Name}");

    [Rite.OnVisibilityChanged]
    private static void OnVisibilityChanged(CanvasItem item) => LogHook("OnVisibilityChanged", $"{item.Name} visible={item.Visible}");

    [Rite.OnBodyEntered]
    private static void OnBodyEntered(Node2D body, Area2D area) => LogHook("OnBodyEntered", $"{area.Name} <- {body.Name}");

    [Rite.OnBodyExited]
    private static void OnBodyExited(Node2D body, Area2D area) => LogHook("OnBodyExited", $"{area.Name} <- {body.Name}");

    [Rite.OnAreaEntered]
    private static void OnAreaEntered(Area2D entering, Area2D source) => LogHook("OnAreaEntered", $"{source.Name} <- {entering.Name}");

    [Rite.OnAreaExited]
    private static void OnAreaExited(Area2D exiting, Area2D source) => LogHook("OnAreaExited", $"{source.Name} <- {exiting.Name}");

    [Rite.OnTimeout]
    private static void OnTimeout(Timer timer) => LogHook("OnTimeout", timer.Name);

    [Rite.OnAnimationStarted]
    private static void OnAnimationStarted(StringName animName, AnimationPlayer player) => LogHook("OnAnimationStarted", $"{player.Name}: {animName}");

    [Rite.OnAnimationFinished]
    private static void OnAnimationFinished(StringName animName, AnimationPlayer player) => LogHook("OnAnimationFinished", $"{player.Name}: {animName}");

    [Rite.OnAudioFinished]
    private static void OnAudioFinished(AudioStreamPlayer player) => LogHook("OnAudioFinished", player.Name);

    [Rite.OnConfirmed]
    private static void OnConfirmed(AcceptDialog dialog) => LogHook("OnConfirmed", dialog.Name);

    [Rite.OnCanceled]
    private static void OnCanceled(AcceptDialog dialog) => LogHook("OnCanceled", dialog.Name);

    [Rite.OnCloseRequested]
    private static void OnCloseRequested(Window window) => LogHook("OnCloseRequested", window.Name);

    [Rite.OnPopupHide]
    private static void OnPopupHide(Popup popup) => LogHook("OnPopupHide", popup.Name);

    [Rite.OnDragStarted]
    private static void OnDragStarted(Slider slider) => LogHook("OnDragStarted", slider.Name);

    [Rite.OnDragEnded]
    private static void OnDragEnded(bool valueChanged, Slider slider) => LogHook("OnDragEnded", $"{slider.Name} changed={valueChanged}");

    [Rite.OnScrollStarted]
    private static void OnScrollStarted(ScrollContainer scroll) => LogHook("OnScrollStarted", scroll.Name);

    [Rite.OnScrollEnded]
    private static void OnScrollEnded(ScrollContainer scroll) => LogHook("OnScrollEnded", scroll.Name);

    [Rite.OnSortChildren]
    private static void OnSortChildren(Container container) => LogHook("OnSortChildren", container.Name);

    private T CreateTestNode<T>(string name) where T : Control, new()
    {
        var node = new T { Name = name, Visible = false }; // Hidden so they don't clutter the screen
        AddChild(node);
        return node;
    }

    private static void StartCategory(string name)
    {
        _currentTestCategory = name;
        _testCounter = 0;
        Log("");
        Log($"=== Testing: {name} ===");
    }

    private static void EndCategory()
    {
        Log($"=== End {_currentTestCategory} ({_testCounter} hooks fired) ===");
        Log("");
    }

    private static void Log(string message)
    {
        GD.Print($"[TestBed] {message}");
    }

    private static void LogHook(string hookName, string details)
    {
        _testCounter++;
        var detailStr = string.IsNullOrEmpty(details) ? "" : $" | {details}";
        GD.Print($"  -> [{hookName}]{detailStr}");
    }

    private static void PrintSummary()
    {
        Log("");
        Log("=== Test Summary ===");
        Log("All hook categories have been executed.");
        Log("Check the console output above for any missing or unexpected hooks.");
        Log("If a hook didn't fire, check:");
        Log("  1. The attribute is applied correctly");
        Log("  2. The parameter types match what Liturgy expects");
        Log("  3. The node type matches the hook's requirements");
        Log("");
    }

}
