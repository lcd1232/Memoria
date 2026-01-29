using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using XInputDotNetPure;

namespace Memoria.Launcher
{
    /// <summary>
    /// Service that provides gamepad navigation support for the launcher.
    /// Polls gamepad state and translates inputs to WPF navigation and actions.
    /// </summary>
    public class GamepadService : IDisposable
    {
        private readonly Window _window;
        private readonly DispatcherTimer _pollTimer;
        private GamePadState _previousState;
        private volatile bool _disposed;

        // Thumbstick deadzone and repeat settings
        private const float ThumbstickDeadzone = 0.5f;
        private const int RepeatDelayMs = 400;
        private const int RepeatRateMs = 100;

        private DateTime _lastUpTime = DateTime.MinValue;
        private DateTime _lastDownTime = DateTime.MinValue;
        private DateTime _lastLeftTime = DateTime.MinValue;
        private DateTime _lastRightTime = DateTime.MinValue;
        private bool _upHeld, _downHeld, _leftHeld, _rightHeld;
        private bool _initialDelayPassedUp, _initialDelayPassedDown, _initialDelayPassedLeft, _initialDelayPassedRight;

        /// <summary>
        /// Event raised when the Start button is pressed (to launch the game).
        /// </summary>
        public event EventHandler StartPressed;

        /// <summary>
        /// Creates a new GamepadService for the specified window.
        /// </summary>
        /// <param name="window">The window to provide gamepad navigation for.</param>
        public GamepadService(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));

            _pollTimer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS polling
            };
            _pollTimer.Tick += PollGamepad;

            // Get initial state
            try
            {
                _previousState = GamePad.GetState(PlayerIndex.One);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GamepadService: Failed to get initial state: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts polling for gamepad input.
        /// </summary>
        public void Start()
        {
            if (!_disposed)
            {
                _pollTimer.Start();
            }
        }

        /// <summary>
        /// Stops polling for gamepad input.
        /// </summary>
        public void Stop()
        {
            _pollTimer.Stop();
        }

        private void PollGamepad(object sender, EventArgs e)
        {
            if (_disposed)
                return;

            try
            {
                var state = GamePad.GetState(PlayerIndex.One);

                if (!state.IsConnected)
                {
                    _previousState = state;
                    return;
                }

                // Handle button presses (on press, not release)
                HandleButtonA(state);
                HandleButtonB(state);
                HandleButtonStart(state);
                HandleShoulderButtons(state);

                // Handle D-Pad and thumbstick navigation with repeat
                HandleNavigation(state);

                _previousState = state;
            }
            catch (Exception ex)
            {
                // Log gamepad errors for debugging but don't crash
                Debug.WriteLine($"GamepadService: Error polling gamepad: {ex.Message}");
            }
        }

        private void HandleButtonA(GamePadState state)
        {
            // A button = Confirm/Click (on press)
            if (state.Buttons.A == ButtonState.Pressed && _previousState.Buttons.A == ButtonState.Released)
            {
                ActivateFocusedElement();
            }
        }

        private void HandleButtonB(GamePadState state)
        {
            // B button = Cancel/Escape (on press)
            if (state.Buttons.B == ButtonState.Pressed && _previousState.Buttons.B == ButtonState.Released)
            {
                SendKey(Key.Escape);
            }
        }

        private void HandleButtonStart(GamePadState state)
        {
            // Start button = Launch game (on press)
            if (state.Buttons.Start == ButtonState.Pressed && _previousState.Buttons.Start == ButtonState.Released)
            {
                StartPressed?.Invoke(this, EventArgs.Empty);
            }
        }

        private void HandleShoulderButtons(GamePadState state)
        {
            // LB = Previous tab, RB = Next tab (on press)
            if (state.Buttons.LeftShoulder == ButtonState.Pressed && _previousState.Buttons.LeftShoulder == ButtonState.Released)
            {
                NavigateTabs(-1);
            }
            else if (state.Buttons.RightShoulder == ButtonState.Pressed && _previousState.Buttons.RightShoulder == ButtonState.Released)
            {
                NavigateTabs(1);
            }
        }

        private void HandleNavigation(GamePadState state)
        {
            var now = DateTime.UtcNow;

            // Determine if navigation directions are active (D-Pad or left thumbstick)
            bool upActive = state.DPad.Up == ButtonState.Pressed || state.ThumbSticks.Left.Y > ThumbstickDeadzone;
            bool downActive = state.DPad.Down == ButtonState.Pressed || state.ThumbSticks.Left.Y < -ThumbstickDeadzone;
            bool leftActive = state.DPad.Left == ButtonState.Pressed || state.ThumbSticks.Left.X < -ThumbstickDeadzone;
            bool rightActive = state.DPad.Right == ButtonState.Pressed || state.ThumbSticks.Left.X > ThumbstickDeadzone;

            // Handle Up
            if (upActive)
            {
                if (!_upHeld)
                {
                    NavigateUp();
                    _lastUpTime = now;
                    _upHeld = true;
                    _initialDelayPassedUp = false;
                }
                else
                {
                    var elapsed = (now - _lastUpTime).TotalMilliseconds;
                    if (!_initialDelayPassedUp && elapsed > RepeatDelayMs)
                    {
                        NavigateUp();
                        _lastUpTime = now;
                        _initialDelayPassedUp = true;
                    }
                    else if (_initialDelayPassedUp && elapsed > RepeatRateMs)
                    {
                        NavigateUp();
                        _lastUpTime = now;
                    }
                }
            }
            else
            {
                _upHeld = false;
                _initialDelayPassedUp = false;
            }

            // Handle Down
            if (downActive)
            {
                if (!_downHeld)
                {
                    NavigateDown();
                    _lastDownTime = now;
                    _downHeld = true;
                    _initialDelayPassedDown = false;
                }
                else
                {
                    var elapsed = (now - _lastDownTime).TotalMilliseconds;
                    if (!_initialDelayPassedDown && elapsed > RepeatDelayMs)
                    {
                        NavigateDown();
                        _lastDownTime = now;
                        _initialDelayPassedDown = true;
                    }
                    else if (_initialDelayPassedDown && elapsed > RepeatRateMs)
                    {
                        NavigateDown();
                        _lastDownTime = now;
                    }
                }
            }
            else
            {
                _downHeld = false;
                _initialDelayPassedDown = false;
            }

            // Handle Left
            if (leftActive)
            {
                if (!_leftHeld)
                {
                    NavigateLeft();
                    _lastLeftTime = now;
                    _leftHeld = true;
                    _initialDelayPassedLeft = false;
                }
                else
                {
                    var elapsed = (now - _lastLeftTime).TotalMilliseconds;
                    if (!_initialDelayPassedLeft && elapsed > RepeatDelayMs)
                    {
                        NavigateLeft();
                        _lastLeftTime = now;
                        _initialDelayPassedLeft = true;
                    }
                    else if (_initialDelayPassedLeft && elapsed > RepeatRateMs)
                    {
                        NavigateLeft();
                        _lastLeftTime = now;
                    }
                }
            }
            else
            {
                _leftHeld = false;
                _initialDelayPassedLeft = false;
            }

            // Handle Right
            if (rightActive)
            {
                if (!_rightHeld)
                {
                    NavigateRight();
                    _lastRightTime = now;
                    _rightHeld = true;
                    _initialDelayPassedRight = false;
                }
                else
                {
                    var elapsed = (now - _lastRightTime).TotalMilliseconds;
                    if (!_initialDelayPassedRight && elapsed > RepeatDelayMs)
                    {
                        NavigateRight();
                        _lastRightTime = now;
                        _initialDelayPassedRight = true;
                    }
                    else if (_initialDelayPassedRight && elapsed > RepeatRateMs)
                    {
                        NavigateRight();
                        _lastRightTime = now;
                    }
                }
            }
            else
            {
                _rightHeld = false;
                _initialDelayPassedRight = false;
            }
        }

        private void NavigateUp()
        {
            var focused = Keyboard.FocusedElement as UIElement;
            if (focused != null)
            {
                // Try to handle special controls first
                if (TryHandleSlider(focused, -1) || TryHandleComboBox(focused, -1) || TryHandleListView(focused, -1))
                    return;
            }
            MoveFocus(FocusNavigationDirection.Up);
        }

        private void NavigateDown()
        {
            var focused = Keyboard.FocusedElement as UIElement;
            if (focused != null)
            {
                // Try to handle special controls first
                if (TryHandleSlider(focused, 1) || TryHandleComboBox(focused, 1) || TryHandleListView(focused, 1))
                    return;
            }
            MoveFocus(FocusNavigationDirection.Down);
        }

        private void NavigateLeft()
        {
            var focused = Keyboard.FocusedElement as UIElement;
            if (focused != null)
            {
                // Try to handle special controls first
                if (TryHandleSlider(focused, -1) || TryHandleComboBox(focused, -1))
                    return;
            }
            MoveFocus(FocusNavigationDirection.Left);
        }

        private void NavigateRight()
        {
            var focused = Keyboard.FocusedElement as UIElement;
            if (focused != null)
            {
                // Try to handle special controls first
                if (TryHandleSlider(focused, 1) || TryHandleComboBox(focused, 1))
                    return;
            }
            MoveFocus(FocusNavigationDirection.Right);
        }

        private bool TryHandleSlider(UIElement element, int direction)
        {
            var slider = element as Slider ?? FindParent<Slider>(element);
            if (slider != null)
            {
                double change = (slider.Maximum - slider.Minimum) * 0.1;
                slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, slider.Value + (change * direction)));
                return true;
            }
            return false;
        }

        private bool TryHandleComboBox(UIElement element, int direction)
        {
            var comboBox = element as ComboBox ?? FindParent<ComboBox>(element);
            if (comboBox != null && comboBox.Items.Count > 0)
            {
                int newIndex = comboBox.SelectedIndex + direction;
                if (newIndex >= 0 && newIndex < comboBox.Items.Count)
                {
                    comboBox.SelectedIndex = newIndex;
                    return true;
                }
                // Return false when at bounds to allow focus navigation
                return false;
            }
            return false;
        }

        private bool TryHandleListView(UIElement element, int direction)
        {
            var listView = element as ListView ?? FindParent<ListView>(element);
            if (listView != null && listView.Items.Count > 0)
            {
                int newIndex = listView.SelectedIndex + direction;
                if (newIndex >= 0 && newIndex < listView.Items.Count)
                {
                    listView.SelectedIndex = newIndex;
                    var selectedItem = listView.SelectedItem;
                    if (selectedItem != null)
                    {
                        listView.ScrollIntoView(selectedItem);
                    }
                    return true;
                }
                // Return false when at bounds to allow focus navigation
                return false;
            }
            return false;
        }

        private void MoveFocus(FocusNavigationDirection direction)
        {
            var focused = Keyboard.FocusedElement as UIElement;
            if (focused != null)
            {
                focused.MoveFocus(new TraversalRequest(direction));
            }
            else
            {
                // If nothing is focused, try to focus the first focusable element
                var firstFocusable = FindFirstFocusableElement(_window);
                firstFocusable?.Focus();
            }
        }

        private void NavigateTabs(int direction)
        {
            // Find the main TabControl in the window
            var tabControl = FindChild<TabControl>(_window, "ContentTabControl");
            if (tabControl != null && tabControl.Items.Count > 0)
            {
                int newIndex = tabControl.SelectedIndex + direction;
                if (newIndex < 0)
                    newIndex = tabControl.Items.Count - 1;
                else if (newIndex >= tabControl.Items.Count)
                    newIndex = 0;

                tabControl.SelectedIndex = newIndex;
            }
        }

        private void ActivateFocusedElement()
        {
            var focused = Keyboard.FocusedElement;

            if (focused is Button button)
            {
                // Click the button
                var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer(button);
                var invokeProvider = peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)
                    as System.Windows.Automation.Provider.IInvokeProvider;
                invokeProvider?.Invoke();
            }
            else if (focused is CheckBox checkBox)
            {
                // Toggle the checkbox - handle nullable bool properly
                checkBox.IsChecked = checkBox.IsChecked != true;
            }
            else if (focused is ToggleButton toggleButton)
            {
                // Toggle the toggle button - handle nullable bool properly
                toggleButton.IsChecked = toggleButton.IsChecked != true;
            }
            else if (focused is ComboBox comboBox)
            {
                // Toggle dropdown
                comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen;
            }
            else if (focused is ListViewItem listViewItem)
            {
                // Select and activate the item
                listViewItem.IsSelected = true;
            }
            else if (focused is TabItem tabItem)
            {
                tabItem.IsSelected = true;
            }
            else
            {
                // For other elements, try to send Enter key
                SendKey(Key.Enter);
            }
        }

        private void SendKey(Key key)
        {
            var target = Keyboard.FocusedElement as IInputElement;
            if (target == null)
                target = _window;

            var presentationSource = PresentationSource.FromVisual(_window as Visual);
            if (presentationSource == null)
                return;

            var keyEventArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                presentationSource,
                0,
                key)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            InputManager.Current.ProcessInput(keyEventArgs);
        }

        private static T FindChild<T>(DependencyObject parent, string childName = null) where T : DependencyObject
        {
            if (parent == null) return null;

            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    if (string.IsNullOrEmpty(childName))
                        return typedChild;
                    if (child is FrameworkElement fe && fe.Name == childName)
                        return typedChild;
                }

                var result = FindChild<T>(child, childName);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            if (child == null) return null;

            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T typedParent)
                    return typedParent;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private static UIElement FindFirstFocusableElement(DependencyObject parent)
        {
            if (parent == null) return null;

            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is UIElement element && element.Focusable && element.IsEnabled &&
                    element.Visibility == Visibility.Visible)
                {
                    return element;
                }

                var result = FindFirstFocusableElement(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _pollTimer.Stop();
                _pollTimer.Tick -= PollGamepad;
            }
        }
    }
}
