using GlobalHotKey;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Input;
using WindowsDesktop;

namespace VirtualDesktopManager
{
    public partial class SettingsForm : Form
    {
        private static readonly Key[] NumKeys = { Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9 };

        private readonly HotKeyManager _leftHotkey;

        private readonly HotKeyManager _numberHotkey;

        private readonly HotKeyManager _rightHotkey;

        private IntPtr[] _activePrograms;

        private bool _closeToTray;

        private IList<VirtualDesktop> _desktops;

        public SettingsForm()
        {
            InitializeComponent();

            HandleChangedNumber();

            _closeToTray = true;

            _rightHotkey = new HotKeyManager();
            _rightHotkey.KeyPressed += RightKeyManagerPressed;

            _leftHotkey = new HotKeyManager();
            _leftHotkey.KeyPressed += LeftKeyManagerPressed;

            _numberHotkey = new HotKeyManager();
            _numberHotkey.KeyPressed += NumberHotkeyPressed;

            VirtualDesktop.CurrentChanged += VirtualDesktop_CurrentChanged;
            VirtualDesktop.Created += VirtualDesktop_Added;
            VirtualDesktop.Destroyed += VirtualDesktop_Destroyed;

            FormClosing += SettingsForm_Closing;

            checkBoxAltHotKey.Checked = Properties.Settings.Default.AltHotKey;

            checkBoxEnableHotkeys.Checked = Properties.Settings.Default.EnableHotKeys;

            listView1.Items.Clear();
            listView1.Columns.Add("File").Width = 400;
            foreach (var file in Properties.Settings.Default.DesktopBackgroundFiles)
            {
                listView1.Items.Add(NewListViewItem(file));
            }
        }

        private static ModifierKeys ActiveAltHotKey => Properties.Settings.Default.AltHotKey ? System.Windows.Input.ModifierKeys.Shift : System.Windows.Input.ModifierKeys.Control;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        private static ListViewItem NewListViewItem(string file)
        {
            return new ListViewItem()
            {
                Text = Path.GetFileName(file),
                ToolTipText = file,
                Name = Path.GetFileName(file),
                Tag = file
            };
        }

        private static string PickNthFile(int currentDesktopIndex)
        {
            var n = Properties.Settings.Default.DesktopBackgroundFiles.Count;
            if (n == 0)
                return null;
            var index = currentDesktopIndex % n;
            return Properties.Settings.Default.DesktopBackgroundFiles[index];
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private void AddFileButton_Click(object sender, EventArgs e)
        {
            openFileDialog1.CheckFileExists = true;
            openFileDialog1.CheckPathExists = true;
            openFileDialog1.Filter = @"Image Files(*.BMP;*.JPG;*.GIF)|*.BMP;*.JPG;*.GIF|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 0;
            openFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            openFileDialog1.Multiselect = true;
            openFileDialog1.Title = @"Select desktop background image";
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                foreach (var file in openFileDialog1.FileNames)
                {
                    listView1.Items.Add(NewListViewItem(file));
                }
            }
        }

        private void ChangeTrayIcon(int currentDesktopIndex = -1)
        {
            if (currentDesktopIndex == -1)
                currentDesktopIndex = GetCurrentDesktopIndex();

            var desktopNumber = currentDesktopIndex + 1;
            var desktopNumberString = desktopNumber.ToString();

            var fontSize = 140;
            var xPlacement = 100;
            var yPlacement = 50;

            if (desktopNumber > 9 && desktopNumber < 100)
            {
                fontSize = 125;
                xPlacement = 75;
                yPlacement = 65;
            }
            else if (desktopNumber > 99)
            {
                fontSize = 80;
                xPlacement = 90;
                yPlacement = 100;
            }

            var newIcon = Properties.Resources.mainIcoPng;
            var desktopNumberFont = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

            var gr = Graphics.FromImage(newIcon);
            gr.DrawString(desktopNumberString, desktopNumberFont, Brushes.White, xPlacement, yPlacement);

            var numberedIcon = Icon.FromHandle(newIcon.GetHicon());
            notifyIcon1.Icon = numberedIcon;

            DestroyIcon(numberedIcon.Handle);
            desktopNumberFont.Dispose();
            newIcon.Dispose();
            gr.Dispose();
        }

        private void DownButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (listView1.SelectedItems.Count > 0)
                {
                    var selected = listView1.SelectedItems[0];
                    var indx = selected.Index;
                    var totl = listView1.Items.Count;

                    if (indx == totl - 1)
                    {
                        listView1.Items.Remove(selected);
                        listView1.Items.Insert(0, selected);
                    }
                    else
                    {
                        listView1.Items.Remove(selected);
                        listView1.Items.Insert(indx + 1, selected);
                    }
                }
                else
                {
                    MessageBox.Show(@"You can only move one item at a time. Please select only one item and try again.",
                        @"Item Select", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _rightHotkey.Dispose();
            _leftHotkey.Dispose();
            _numberHotkey.Dispose();

            _closeToTray = false;

            Close();
        }

        private int GetCurrentDesktopIndex()
        {
            return _desktops.IndexOf(VirtualDesktop.Current);
        }

        private void HandleChangedNumber()
        {
            _desktops = VirtualDesktop.GetDesktops();
            _activePrograms = new IntPtr[_desktops.Count];
        }

        private VirtualDesktop InitialDesktopState()
        {
            var desktop = VirtualDesktop.Current;
            var desktopIndex = GetCurrentDesktopIndex();

            SaveApplicationFocus(desktopIndex);

            return desktop;
        }

        private void LeftKeyManagerPressed(object sender, KeyPressedEventArgs e)
        {
            var desktop = InitialDesktopState();

            if (desktop.GetLeft() != null)
            {
                desktop.GetLeft()?.Switch();
            }
            else
            {
                _desktops.Last()?.Switch();
            }
        }

        private void NotifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            OpenSettings();
        }

        private void NumberHotkeyPressed(object sender, KeyPressedEventArgs e)
        {
            var index = (int)e.HotKey.Key - (int)Key.D0 - 1;
            var currentDesktopIndex = GetCurrentDesktopIndex();

            if (index == currentDesktopIndex)
            {
                return;
            }

            if (index > _desktops.Count - 1)
            {
                return;
            }

            _desktops.ElementAt(index)?.Switch();
        }

        private void OpenSettings()
        {
            Visible = true;
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
        }

        private void RegisterHotKeys()
        {
            try
            {
                if (!Properties.Settings.Default.EnableHotKeys) return;

                _rightHotkey.Register(Key.Right, ActiveAltHotKey | System.Windows.Input.ModifierKeys.Alt);
                _leftHotkey.Register(Key.Left, ActiveAltHotKey | System.Windows.Input.ModifierKeys.Alt);
                RegisterNumberHotkeys(ActiveAltHotKey | System.Windows.Input.ModifierKeys.Alt);
            }
            catch (Exception)
            {
                notifyIcon1.BalloonTipTitle = @"Error registering hotkeys";
                notifyIcon1.BalloonTipText = @"Could not register hotkeys. Please open settings and try changing the Alt hotKey option.";
                notifyIcon1.ShowBalloonTip(2000);
            }
        }

        private void RegisterNumberHotkeys(ModifierKeys modifiers)
        {
            foreach (var key in NumKeys)
            {
                _numberHotkey.Register(key, modifiers);
            }
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (listView1.SelectedItems.Count > 0)
                {
                    var selected = listView1.SelectedItems[0];
                    listView1.Items.Remove(selected);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void RestoreApplicationFocus(int currentDesktopIndex = -1)
        {
            if (currentDesktopIndex == -1)
                currentDesktopIndex = GetCurrentDesktopIndex();

            if (_activePrograms[currentDesktopIndex] != IntPtr.Zero)
            {
                SetForegroundWindow(_activePrograms[currentDesktopIndex]);
            }
        }

        private void RightKeyManagerPressed(object sender, KeyPressedEventArgs e)
        {
            var desktop = InitialDesktopState();

            if (desktop.GetRight() != null)
            {
                desktop.GetRight()?.Switch();
            }
            else
            {
                _desktops.First()?.Switch();
            }
        }

        private void SaveApplicationFocus(int currentDesktopIndex = -1)
        {
            var activeAppWindow = GetForegroundWindow();

            if (currentDesktopIndex == -1)
                currentDesktopIndex = GetCurrentDesktopIndex();

            _activePrograms[currentDesktopIndex] = activeAppWindow;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            // Remove hotkeys - if any
            UnregisterHotKeys();

            // Update settings in regards to hotkeys
            Properties.Settings.Default.EnableHotKeys = checkBoxEnableHotkeys.Checked;
            Properties.Settings.Default.AltHotKey = checkBoxAltHotKey.Checked;

            // Register hotkeys - if needed
            RegisterHotKeys();

            // Update other settings
            Properties.Settings.Default.DesktopBackgroundFiles.Clear();

            foreach (ListViewItem item in listView1.Items)
            {
                Properties.Settings.Default.DesktopBackgroundFiles.Add(item.Tag.ToString());
            }

            Properties.Settings.Default.Save();

            labelStatus.Text = @"Changes were saved.";
        }

        private void SettingsForm_Closing(object sender, FormClosingEventArgs e)
        {
            if (_closeToTray)
            {
                e.Cancel = true;
                Visible = false;
                ShowInTaskbar = false;
                notifyIcon1.BalloonTipTitle = @"Still Running...";
                notifyIcon1.BalloonTipText = @"Right-click on the tray icon to exit.";
                notifyIcon1.ShowBalloonTip(2000);
            }
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            labelStatus.Text = "";

            RegisterHotKeys();

            InitialDesktopState();
            ChangeTrayIcon();

            Visible = false;
        }

        private void SettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenSettings();
        }

        private void UnregisterHotKeys()
        {
            try
            {
                if (!Properties.Settings.Default.EnableHotKeys) return;

                _rightHotkey.Unregister(Key.Right, ActiveAltHotKey | System.Windows.Input.ModifierKeys.Alt);
                _leftHotkey.Unregister(Key.Left, ActiveAltHotKey | System.Windows.Input.ModifierKeys.Alt);
                UnregisterNumberHotkeys(ActiveAltHotKey | System.Windows.Input.ModifierKeys.Alt);
            }
            catch (Exception)
            {
                notifyIcon1.BalloonTipTitle = @"Error unregistering hotkeys";
                notifyIcon1.BalloonTipText = @"Could not unregister hotkeys. The registration process earlier also probably failed?";
                notifyIcon1.ShowBalloonTip(2000);
            }
        }

        private void UnregisterNumberHotkeys(ModifierKeys modifiers)
        {
            foreach (var key in NumKeys)
            {
                _numberHotkey.Unregister(key, modifiers);
            }
        }

        private void UpButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (listView1.SelectedItems.Count > 0)
                {
                    var selected = listView1.SelectedItems[0];
                    var indx = selected.Index;
                    var totl = listView1.Items.Count;

                    if (indx == 0)
                    {
                        listView1.Items.Remove(selected);
                        listView1.Items.Insert(totl - 1, selected);
                    }
                    else
                    {
                        listView1.Items.Remove(selected);
                        listView1.Items.Insert(indx - 1, selected);
                    }
                }
                else
                {
                    MessageBox.Show(@"You can only move one item at a time. Please select only one item and try again.",
                        @"Item Select", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void VirtualDesktop_Added(object sender, VirtualDesktop e)
        {
            HandleChangedNumber();
        }

        private void VirtualDesktop_CurrentChanged(object sender, VirtualDesktopChangedEventArgs e)
        {
            // 0 == first
            var currentDesktopIndex = GetCurrentDesktopIndex();

            var pictureFile = PickNthFile(currentDesktopIndex);
            if (pictureFile != null)
            {
                Native.SetBackground(pictureFile);
            }

            RestoreApplicationFocus(currentDesktopIndex);
            ChangeTrayIcon(currentDesktopIndex);
        }

        private void VirtualDesktop_Destroyed(object sender, VirtualDesktopDestroyEventArgs e)
        {
            HandleChangedNumber();
        }
    }
}