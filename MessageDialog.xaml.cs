using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EmpireGame
{
    public partial class MessageDialog : Window
    {
        public bool Result { get; private set; } = false;

        // Icon presets
        public const string IconInfo    = "ℹ️";
        public const string IconWarning = "⚠️";
        public const string IconConfirm = "❓";
        public const string IconSuccess = "✅";

        private MessageDialog(string message, string title, bool isYesNo, string icon,
                              Color gradientStart, Color gradientEnd)
        {
            InitializeComponent();

            TitleIcon.Text    = icon;
            TitleText.Text    = title;
            MessageText.Text  = message;
            TitleGrad1.Color  = gradientStart;
            TitleGrad2.Color  = gradientEnd;

            if (isYesNo)
            {
                OkPanel.Visibility    = Visibility.Collapsed;
                YesNoPanel.Visibility = Visibility.Visible;
            }

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Space)  { Result = true;  Close(); }
                if (e.Key == Key.Escape)                        { Result = false; Close(); }
            };
        }

        // ── Static helpers ───────────────────────────────────────────────────

        /// <summary>Simple info/message dialog (OK button).</summary>
        public static void Show(Window owner, string message, string title = "Message",
                                string icon = IconInfo)
        {
            var (c1, c2) = GradientForIcon(icon);
            var dlg = new MessageDialog(message, title, false, icon, c1, c2) { Owner = owner };
            dlg.ShowDialog();
        }

        /// <summary>Warning dialog (OK button).</summary>
        public static void Warn(Window owner, string message, string title = "Warning")
            => Show(owner, message, title, IconWarning);

        /// <summary>Yes/No confirmation dialog. Returns true if the user clicked YES.</summary>
        public static bool Confirm(Window owner, string message, string title = "Confirm")
        {
            var dlg = new MessageDialog(message, title, true, IconConfirm,
                                        Color.FromRgb(0xFF, 0xD4, 0x3B),
                                        Color.FromRgb(0xFF, 0xA9, 0x4D)) { Owner = owner };
            dlg.ShowDialog();
            return dlg.Result;
        }

        // ── Button handlers ──────────────────────────────────────────────────

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static (Color, Color) GradientForIcon(string icon) => icon switch
        {
            IconWarning => (Color.FromRgb(0xFF, 0x8A, 0x8A), Color.FromRgb(0xFF, 0xC0, 0x61)),
            IconConfirm => (Color.FromRgb(0xFF, 0xD4, 0x3B), Color.FromRgb(0xFF, 0xA9, 0x4D)),
            IconSuccess => (Color.FromRgb(0x2F, 0xB8, 0x5B), Color.FromRgb(0x26, 0x9E, 0x4E)),
            _           => (Color.FromRgb(0x5B, 0xA0, 0xFF), Color.FromRgb(0x3E, 0x8E, 0xF7)),
        };
    }
}
