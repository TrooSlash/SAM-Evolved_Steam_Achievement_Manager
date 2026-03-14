using System.Drawing;
using System.Windows.Forms;

namespace SAM.Picker
{
    internal class SettingsDialog : Form
    {
        public Localization.Language SelectedLanguage { get; private set; }
        public bool IsTileView { get; private set; }
        public string ApiKey { get; private set; }

        private readonly ComboBox _LanguageCombo;
        private readonly RadioButton _ListRadio;
        private readonly RadioButton _TilesRadio;
        private readonly TextBox _ApiKeyTextBox;

        public SettingsDialog(Localization.Language currentLang, bool isTileView)
        {
            SelectedLanguage = currentLang;
            IsTileView = isTileView;
            ApiKey = AppSettings.SteamApiKey;

            this.Text = "\u2699 " + Localization.Get("Settings");
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(420, 310);
            this.BackColor = DarkTheme.DarkBackground;
            this.ForeColor = DarkTheme.Text;
            this.Font = new Font("Segoe UI", 9f);

            // Language
            var langLabel = new Label
            {
                Text = Localization.Get("Language"),
                Location = new Point(15, 18),
                Size = new Size(80, 20),
                ForeColor = DarkTheme.TextBright,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };

            _LanguageCombo = new ComboBox
            {
                Location = new Point(100, 15),
                Size = new Size(180, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.TextBright,
                FlatStyle = FlatStyle.Flat,
            };
            _LanguageCombo.Items.AddRange(new object[] { "English", "\u0420\u0443\u0441\u0441\u043A\u0438\u0439" });
            _LanguageCombo.SelectedIndex = currentLang == Localization.Language.Russian ? 1 : 0;

            // View Mode
            var viewLabel = new Label
            {
                Text = Localization.Get("ViewMode"),
                Location = new Point(15, 58),
                Size = new Size(160, 20),
                ForeColor = DarkTheme.TextBright,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };

            _ListRadio = new RadioButton
            {
                Text = Localization.Get("ListMode"),
                Location = new Point(20, 82),
                Size = new Size(120, 22),
                ForeColor = DarkTheme.Text,
                Checked = !isTileView,
            };

            _TilesRadio = new RadioButton
            {
                Text = Localization.Get("TilesMode"),
                Location = new Point(150, 82),
                Size = new Size(120, 22),
                ForeColor = DarkTheme.Text,
                Checked = isTileView,
            };

            // Steam API Key
            var apiLabel = new Label
            {
                Text = Localization.Get("SteamApiKey"),
                Location = new Point(15, 120),
                Size = new Size(390, 20),
                ForeColor = DarkTheme.TextBright,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };

            _ApiKeyTextBox = new TextBox
            {
                Location = new Point(15, 143),
                Size = new Size(390, 23),
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.TextBright,
                BorderStyle = BorderStyle.FixedSingle,
                Text = AppSettings.SteamApiKey,
                Font = new Font("Consolas", 9f),
            };

            var apiHint = new Label
            {
                Text = Localization.Get("SteamApiKeyHint"),
                Location = new Point(15, 170),
                Size = new Size(390, 50),
                ForeColor = DarkTheme.TextSecondary,
                Font = new Font("Segoe UI", 8f),
            };

            var apiLink = new LinkLabel
            {
                Text = "steamcommunity.com/dev/apikey",
                Location = new Point(15, 220),
                Size = new Size(390, 18),
                LinkColor = DarkTheme.Accent,
                ActiveLinkColor = DarkTheme.AccentSecondary,
                Font = new Font("Segoe UI", 8.5f),
            };
            apiLink.LinkClicked += (s, e) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://steamcommunity.com/dev/apikey",
                    UseShellExecute = true
                });
            };

            // Buttons
            var okButton = new Button
            {
                Text = Localization.Get("OK"),
                DialogResult = DialogResult.OK,
                Location = new Point(230, 260),
                Size = new Size(80, 30),
                BackColor = DarkTheme.Accent,
                ForeColor = DarkTheme.TextBright,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };
            okButton.FlatAppearance.BorderSize = 0;

            var cancelButton = new Button
            {
                Text = Localization.Get("Cancel"),
                DialogResult = DialogResult.Cancel,
                Location = new Point(320, 260),
                Size = new Size(80, 30),
                BackColor = DarkTheme.Toolbar,
                ForeColor = DarkTheme.Text,
                FlatStyle = FlatStyle.Flat,
            };
            cancelButton.FlatAppearance.BorderSize = 0;

            this.Controls.AddRange(new Control[]
            {
                langLabel, _LanguageCombo,
                viewLabel, _ListRadio, _TilesRadio,
                apiLabel, _ApiKeyTextBox, apiHint, apiLink,
                okButton, cancelButton
            });

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;

            this.FormClosing += (s, e) =>
            {
                if (this.DialogResult == DialogResult.OK)
                {
                    SelectedLanguage = _LanguageCombo.SelectedIndex == 1
                        ? Localization.Language.Russian
                        : Localization.Language.English;
                    IsTileView = _TilesRadio.Checked;
                    ApiKey = _ApiKeyTextBox.Text.Trim();
                }
            };
        }
    }
}
