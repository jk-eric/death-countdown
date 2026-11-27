using System;
using System.Globalization;
using System.Windows.Forms;

internal static class Locale
{
    internal static bool English { get; private set; }
    internal static string Currency { get; private set; }

    static Locale() { Use("auto", RegionalCurrency); }

    internal static string RegionalCurrency {
        get { return RegionInfo.CurrentRegion.ISOCurrencySymbol == "CNY" ? "CNY" : "USD"; }
    }

    internal static void Use(string language, string currency)
    {
        English = language == "en" || (language != "zh" &&
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "zh");
        Currency = currency == "USD" ? "USD" : "CNY";
    }

    internal static string T(string chinese, string english) { return English ? english : chinese; }
    internal static string Symbol { get { return Currency == "USD" ? "$" : "¥"; } }
    internal static string UnitNote {
        get { return T("切换币种只更改金额单位，数值保持原样。", "Changing units keeps the same amounts."); }
    }
    internal const string SettingsTitle = "语言 / Language";
    internal static ComboBox LanguageBox(string code)
    {
        var box = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
            AccessibleName = SettingsTitle };
        box.Items.AddRange(new object[] { T("跟随系统", "System default"), "简体中文", "English" });
        box.SelectedIndex = code == "zh" ? 1 : code == "en" ? 2 : 0;
        return box;
    }
    internal static string LanguageCode(ComboBox box)
    {
        return box.SelectedIndex == 1 ? "zh" : box.SelectedIndex == 2 ? "en" : "auto";
    }
    internal static ComboBox CurrencyBox(string code)
    {
        var box = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
            AccessibleName = T("货币", "Currency") };
        box.Items.AddRange(new object[] { T("人民币 ¥ (CNY)", "CNY (¥)"), T("美元 $ (USD)", "USD ($)") });
        box.SelectedIndex = code == "USD" ? 1 : 0;
        return box;
    }
    internal static string CurrencyCode(ComboBox box) { return box.SelectedIndex == 1 ? "USD" : "CNY"; }
}

internal sealed class LanguageDialog : Form
{
    readonly ComboBox language, currency;
    internal string Language { get { return Locale.LanguageCode(language); } }
    internal string Currency { get { return Locale.CurrencyCode(currency); } }

    internal LanguageDialog(string languageCode, string currencyCode)
    {
        Text = Locale.T("语言和货币 / Language", "Language and currency");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = ShowInTaskbar = false;
        AutoScaleDimensions = new System.Drawing.SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new System.Drawing.Size(370, 158);
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(10) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        language = Locale.LanguageBox(languageCode);
        currency = Locale.CurrencyBox(currencyCode);
        grid.Controls.Add(new Label { Text = Locale.SettingsTitle, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        grid.Controls.Add(language, 1, 0);
        grid.Controls.Add(new Label { Text = currency.AccessibleName, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        grid.Controls.Add(currency, 1, 1);
        var note = new Label { Text = Locale.UnitNote, Dock = DockStyle.Fill, AutoSize = true };
        grid.Controls.Add(note, 0, 2); grid.SetColumnSpan(note, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var ok = new Button { Text = Locale.T("保存", "Save"), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = Locale.T("取消", "Cancel"), DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        grid.Controls.Add(buttons, 0, 3); grid.SetColumnSpan(buttons, 2);
        Controls.Add(grid); AcceptButton = ok; CancelButton = cancel;
    }
}
