using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

public sealed class Preferences
{
    public string EventName = "2027 元旦";
    public long End = Clock.NewYear;
    public decimal Rate = 1m;
    public bool Accumulating;
    public long Since;
    public decimal Bank;
    public bool Pinned = true;
    public int Alpha = 95;
    public int Zoom = 100;
    public int WindowWidth, WindowHeight;
    public bool HasPosition;
    public int X, Y;

    public static Preferences Read(string file)
    {
        if (!File.Exists(file)) return new Preferences();
        try
        {
            using (var stream = File.OpenRead(file))
            {
                var p = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(stream);
                if (p.Rate < 0 || p.Rate > 999999 || p.Bank < 0 || p.Since < 0 ||
                    p.Since > DateTime.MaxValue.Ticks || (p.Accumulating && p.Since == 0))
                    return new Preferences();
                p.Alpha = Math.Max(10, Math.Min(100, p.Alpha));
                if (p.Zoom < 75 || p.Zoom > 200) p.Zoom = 100;
                if (string.IsNullOrWhiteSpace(p.EventName)) p.EventName = "2027 元旦";
                if (p.End < Clock.ToUtc(DateTimePicker.MinimumDateTime) ||
                    p.End > Clock.ToUtc(DateTimePicker.MaximumDateTime)) p.End = Clock.NewYear;
                return p;
            }
        }
        catch (InvalidOperationException) { return new Preferences(); }
        catch (IOException) { return new Preferences(); }
        catch (UnauthorizedAccessException) { return new Preferences(); }
    }

    public void Write(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        string tmp = file + ".tmp";
        using (var stream = File.Create(tmp))
            new XmlSerializer(typeof(Preferences)).Serialize(stream, this);
        if (File.Exists(file)) File.Replace(tmp, file, null);
        else File.Move(tmp, file);
    }
}

static class Clock
{
    public static readonly long NewYear = new DateTimeOffset(2027, 1, 1, 0, 0, 0,
        TimeSpan.FromHours(8)).UtcDateTime.Ticks;

    public static long ToUtc(DateTime beijing)
    {
        return beijing.Ticks - 8 * TimeSpan.TicksPerHour;
    }
    public static DateTime InBeijing(long utc)
    {
        return new DateTime(utc + 8 * TimeSpan.TicksPerHour);
    }
    public static void SetTarget(Preferences p, string name, long end, long now)
    {
        if (p.Accumulating && p.End != end)
        {
            p.Bank = Value(p, now);
            p.Since = now;
        }
        p.EventName = name.Trim();
        p.End = end;
    }
    public static long RemainingMs(Preferences p, long now)
    {
        // Round up so the final fractional millisecond never shows zero early.
        long ticks = Math.Max(0L, p.End - now);
        return (ticks + TimeSpan.TicksPerMillisecond - 1) / TimeSpan.TicksPerMillisecond;
    }
    public static decimal Value(Preferences p, long now)
    {
        long ticks = p.Accumulating ? Math.Max(0L, Math.Min(now, p.End) - p.Since)
                                   : Math.Max(0L, p.End - now);
        return (p.Accumulating ? p.Bank : 0m) + ticks * p.Rate / TimeSpan.TicksPerSecond;
    }
    public static void ChangeRate(Preferences p, decimal rate, long now)
    {
        if (p.Accumulating)
        {
            p.Bank = Value(p, now);
            p.Since = Math.Min(now, p.End);
        }
        p.Rate = rate;
    }
    public static void Start(Preferences p, long now)
    {
        p.Accumulating = true;
        p.Bank = 0;
        p.Since = Math.Min(now, p.End);
    }
}

sealed class TargetDialog : Form
{
    readonly TextBox name = new TextBox();
    readonly RadioButton absolute = new RadioButton();
    readonly RadioButton relative = new RadioButton();
    readonly DateTimePicker date = new DateTimePicker();
    readonly NumericUpDown days = new NumericUpDown();
    readonly Label error = new Label();
    public string EventName { get { return name.Text.Trim(); } }
    public long Deadline { get; private set; }
    public long SavedAt { get; private set; }

    public TargetDialog(Preferences p)
    {
        Text = "设置倒计时目标";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = MaximizeBox = ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96,96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(316,252);
        var label = new Label { Text = "事件名称", AutoSize = true, Location = new Point(16,14) };
        name.SetBounds(18,36,280,25); name.MaxLength = 60; name.Text = p.EventName;
        absolute.Text = "指定日期和时间（北京时间）"; absolute.SetBounds(16,72,279,24);
        date.SetBounds(38,98,260,24); date.Format = DateTimePickerFormat.Custom;
        date.CustomFormat = "yyyy-MM-dd  HH:mm:ss"; date.Value = Clock.InBeijing(p.End);
        relative.Text = "从现在起，倒数多少天"; relative.SetBounds(16,131,279,24);
        days.SetBounds(38,157,110,26); days.Minimum = 1; days.Maximum = 999999;
        days.Value = Math.Min(days.Maximum, Math.Max(1, (decimal)Math.Ceiling(
            (p.End - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerDay)));
        var unit = new Label { Text = "天", AutoSize = true, Location = new Point(157,161) };
        error.SetBounds(16,190,285,20); error.ForeColor = Color.Firebrick;
        var ok = new Button { Text = "保存" }; ok.SetBounds(140,214,76,28);
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel };
        cancel.SetBounds(223,214,76,28);
        Controls.AddRange(new Control[] {label,name,absolute,date,relative,days,unit,error,ok,cancel});
        absolute.CheckedChanged += delegate { date.Enabled = absolute.Checked; };
        relative.CheckedChanged += delegate { days.Enabled = relative.Checked; };
        absolute.Checked = true; days.Enabled = false;
        ok.Click += delegate {
            if (EventName.Length == 0) { error.Text = "请输入事件名称。"; name.Focus(); return; }
            long now = DateTime.UtcNow.Ticks;
            long end = relative.Checked ? now + (long)days.Value * TimeSpan.TicksPerDay : Clock.ToUtc(date.Value);
            if (end <= now && end != p.End) { error.Text = "请选一个将来的日期和时间。"; return; }
            Deadline = end; SavedAt = now; DialogResult = DialogResult.OK;
        };
        AcceptButton = ok; CancelButton = cancel;
        Shown += delegate { name.Focus(); name.SelectAll(); };
    }
}

sealed class Widget : Form
{
    readonly Preferences prefs;
    readonly string file;
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    readonly Font small = new Font("Microsoft YaHei UI", 12, GraphicsUnit.Pixel);
    readonly Font body = new Font("Microsoft YaHei UI", 14, GraphicsUnit.Pixel);
    readonly Font digits = new Font("Consolas", 37, FontStyle.Bold, GraphicsUnit.Pixel);
    readonly Font days = new Font("Consolas", 33, FontStyle.Bold, GraphicsUnit.Pixel);
    readonly Font money = new Font("Consolas", 22, FontStyle.Bold, GraphicsUnit.Pixel);
    readonly Brush white = new SolidBrush(Color.FromArgb(240,244,248));
    readonly Brush muted = new SolidBrush(Color.FromArgb(169,183,190));
    readonly Brush mint = new SolidBrush(Color.FromArgb(149,235,197));
    readonly Pen divider = new Pen(Color.FromArgb(57,72,79));
    readonly float dpiScale;
    float scale;
    Point dragStart;
    Rectangle dragBounds;
    AnchorStyles dragEdges;
    bool dragging, overMenu, saveWarned;
    const int W = 342, H = 126;

    public Widget(Preferences prefs, string file)
    {
        this.prefs = prefs;
        this.file = file;
        Text = prefs.EventName + "倒计时";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(24,33,39);
        using (Graphics g = CreateGraphics()) dpiScale = g.DpiX / 96f;
        Rectangle area = prefs.HasPosition ? Screen.FromPoint(new Point(prefs.X, prefs.Y)).WorkingArea
                                          : Screen.PrimaryScreen.WorkingArea;
        MinimumSize = new Size((int)Math.Ceiling(200*dpiScale), (int)Math.Ceiling(72*dpiScale));
        Size initialSize = NominalSize(prefs.Zoom);
        int width = prefs.WindowWidth > 0 ? prefs.WindowWidth : initialSize.Width;
        int height = prefs.WindowHeight > 0 ? prefs.WindowHeight : initialSize.Height;
        ClientSize = new Size(Math.Min(area.Width,width),Math.Min(area.Height,height));
        UpdateShape();
        Location = Clamp(prefs.HasPosition ? new Point(prefs.X,prefs.Y) :
            new Point(area.Right - Width - 24, area.Top + 24), area);
        TopMost = prefs.Pinned;
        Opacity = prefs.Alpha / 100.0;
        BuildMenu();
        ContextMenuStrip = menu;
        timer.Interval = 16;
        timer.Tick += delegate { Invalidate(); };
        timer.Start();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (dpiScale > 0 && Width > 0 && Height > 0) UpdateShape();
    }
    void UpdateShape()
    {
        scale = Math.Min(ClientSize.Width/(float)W, ClientSize.Height/(float)H);
        using (var path = new GraphicsPath())
        {
            float r = 22 * scale, w = Width, h = Height;
            path.AddArc(0,0,r,r,180,90); path.AddArc(w-r,0,r,r,270,90);
            path.AddArc(w-r,h-r,r,r,0,90); path.AddArc(0,h-r,r,r,90,90);
            path.CloseFigure();
            Region = new Region(path);
        }
        Invalidate();
    }

    Size NominalSize(int percent)
    {
        float factor = dpiScale * percent / 100f;
        return new Size((int)Math.Round(W * factor), (int)Math.Round(H * factor));
    }

    void RestoreDefaultSize()
    {
        Rectangle area = Screen.FromControl(this).WorkingArea;
        Size standard = NominalSize(100);
        Point position = Location;
        ClientSize = new Size(Math.Min(standard.Width,area.Width), Math.Min(standard.Height,area.Height));
        Location = Clamp(position,area);
        prefs.Zoom = 100;
        RememberPosition();
    }

    void RememberPosition()
    {
        prefs.WindowWidth = ClientSize.Width; prefs.WindowHeight = ClientSize.Height;
        prefs.X = Left; prefs.Y = Top; prefs.HasPosition = true;
        Persist();
    }

    Point Clamp(Point pt, Rectangle area)
    {
        return new Point(Math.Max(area.Left, Math.Min(pt.X, area.Right - Width)),
                         Math.Max(area.Top, Math.Min(pt.Y, area.Bottom - Height)));
    }
    void Persist()
    {
        try { prefs.Write(file); saveWarned = false; }
        catch (Exception ex)
        {
            if (!(ex is IOException || ex is UnauthorizedAccessException)) throw;
            if (saveWarned) return;
            saveWarned = true;
            MessageBox.Show(this,"本次设置已生效，但无法保存到本机。\r\n" + ex.Message, Text);
        }
    }
    void BuildMenu()
    {
        var target = new ToolStripMenuItem("设置倒计时目标…", null, delegate {
            using (var dialog = new TargetDialog(prefs))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                Clock.SetTarget(prefs, dialog.EventName, dialog.Deadline, dialog.SavedAt);
                Text = prefs.EventName + "倒计时";
                Persist(); Invalidate();
            }
        });
        var rate = new ToolStripMenuItem("设置费率…", null, delegate { EditRate(); });
        var remain = new ToolStripMenuItem("显示剩余时间价值", null, delegate {
            prefs.Accumulating = false; Persist(); Invalidate();
        });
        var start = new ToolStripMenuItem("从零开始累计金额", null, delegate {
            if (prefs.Accumulating && MessageBox.Show(this, "将当前累计金额清零并重新开始？",
                Text, MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            Clock.Start(prefs, DateTime.UtcNow.Ticks); Persist(); Invalidate();
        });
        var pin = new ToolStripMenuItem("窗口置顶", null, delegate {
            TopMost = prefs.Pinned = !prefs.Pinned; Persist();
        });
        var alpha = new ToolStripLabel("不透明度   " + prefs.Alpha + "%");
        var slider = new TrackBar { Minimum = 10, Maximum = 100, Value = prefs.Alpha,
            TickStyle = TickStyle.None, AutoSize = false, AccessibleName = "不透明度",
            Size = new Size((int)(160*dpiScale), (int)(26*dpiScale)),
            BackColor = SystemColors.Menu };
        var host = new ToolStripControlHost(slider);
        bool alphaChanged = false;
        slider.ValueChanged += delegate {
            prefs.Alpha = slider.Value;
            Opacity = prefs.Alpha / 100.0;
            alpha.Text = "不透明度   " + prefs.Alpha + "%";
            alphaChanged = true;
        };
        menu.Closed += delegate { if (alphaChanged) { Persist(); alphaChanged = false; } };
        var resetSize = new ToolStripMenuItem("恢复默认大小", null, delegate { RestoreDefaultSize(); });
        menu.Items.AddRange(new ToolStripItem[] {target,rate, remain, start,new ToolStripSeparator(),
            pin,resetSize,alpha,host,new ToolStripSeparator(),new ToolStripMenuItem("退出",null,delegate { Close(); })});
        menu.Opening += delegate {
            rate.Text = "设置费率…  ¥" + prefs.Rate.ToString("0.####") + "/秒";
            remain.Checked = !prefs.Accumulating;
            start.Text = prefs.Accumulating ? "重新从零累计…" : "从零开始累计金额";
            start.Enabled = DateTime.UtcNow.Ticks < prefs.End;
            pin.Checked = prefs.Pinned;
        };
    }
    void EditRate()
    {
        using (var box = new Form())
        using (var input = new NumericUpDown())
        using (var ok = new Button())
        using (var cancel = new Button())
        {
            box.Text = "设置费率"; box.FormBorderStyle = FormBorderStyle.FixedDialog;
            box.StartPosition = FormStartPosition.CenterParent;
            box.MinimizeBox = box.MaximizeBox = false; box.ShowInTaskbar = false;
            box.AutoScaleMode = AutoScaleMode.Dpi; box.AutoScaleDimensions = new SizeF(96,96);
            box.ClientSize = new Size(264,125);
            var label = new Label { Text = "每秒金额（元）", AutoSize = true, Location = new Point(16,13) };
            input.SetBounds(19,39,225,26); input.DecimalPlaces = 4; input.Maximum = 999999;
            input.Increment = 0.1m; input.Value = prefs.Rate;
            ok.SetBounds(82,83,76,28); ok.Text = "确定"; ok.DialogResult = DialogResult.OK;
            cancel.SetBounds(168,83,76,28); cancel.Text = "取消"; cancel.DialogResult = DialogResult.Cancel;
            box.Controls.AddRange(new Control[] { label,input,ok,cancel });
            box.AcceptButton = ok; box.CancelButton = cancel;
            box.Shown += delegate { input.Focus(); input.Select(0, input.Text.Length); };
            if (box.ShowDialog(this) == DialogResult.OK) {
                Clock.ChangeRate(prefs, input.Value, DateTime.UtcNow.Ticks); Persist(); Invalidate();
            }
        }
    }
    bool MenuHit(Point p) { return p.X >= ClientSize.Width-47*scale && p.Y < 36*scale; }
    AnchorStyles Edges(Point p)
    {
        int grip = (int)Math.Ceiling(7*dpiScale);
        AnchorStyles edge = AnchorStyles.None;
        if (p.X < grip) edge |= AnchorStyles.Left;
        else if (p.X >= ClientSize.Width-grip) edge |= AnchorStyles.Right;
        if (p.Y < grip) edge |= AnchorStyles.Top;
        else if (p.Y >= ClientSize.Height-grip) edge |= AnchorStyles.Bottom;
        return edge;
    }
    static Cursor CursorFor(AnchorStyles edge)
    {
        switch (edge) {
            case AnchorStyles.Left: case AnchorStyles.Right: return Cursors.SizeWE;
            case AnchorStyles.Top: case AnchorStyles.Bottom: return Cursors.SizeNS;
            case AnchorStyles.Left | AnchorStyles.Top:
            case AnchorStyles.Right | AnchorStyles.Bottom: return Cursors.SizeNWSE;
            case AnchorStyles.Right | AnchorStyles.Top:
            case AnchorStyles.Left | AnchorStyles.Bottom: return Cursors.SizeNESW;
            default: return Cursors.Default;
        }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        dragEdges = Edges(e.Location);
        if (dragEdges == AnchorStyles.None && MenuHit(e.Location)) {
            menu.Show(this,new Point(ClientSize.Width-(int)(42*scale),(int)(32*scale))); return;
        }
        dragStart = PointToScreen(e.Location); dragBounds = Bounds;
        dragging = true; Capture = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var edge = dragging ? dragEdges : Edges(e.Location);
        overMenu = !dragging && edge == AnchorStyles.None && MenuHit(e.Location);
        Cursor = overMenu ? Cursors.Hand : CursorFor(edge);
        if (!dragging) return;
        var mouse = PointToScreen(e.Location);
        int dx = mouse.X - dragStart.X, dy = mouse.Y - dragStart.Y;
        var b = dragBounds;
        if (edge == AnchorStyles.None) { Location = new Point(b.X + dx,b.Y + dy); return; }
        int left = b.Left, top = b.Top, right = b.Right, bottom = b.Bottom;
        if ((edge & AnchorStyles.Left) != 0) left = Math.Min(b.Right-MinimumSize.Width,b.Left+dx);
        if ((edge & AnchorStyles.Right) != 0) right = Math.Max(b.Left+MinimumSize.Width,b.Right+dx);
        if ((edge & AnchorStyles.Top) != 0) top = Math.Min(b.Bottom-MinimumSize.Height,b.Top+dy);
        if ((edge & AnchorStyles.Bottom) != 0) bottom = Math.Max(b.Top+MinimumSize.Height,b.Bottom+dy);
        Bounds = Rectangle.FromLTRB(left,top,right,bottom);
    }
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!dragging || Capture) return;
        dragging = false;
        Location = Clamp(Location, Screen.FromControl(this).WorkingArea);
        RememberPosition();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (dragging && e.Button == MouseButtons.Left) Capture = false;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics; g.ScaleTransform(scale,scale);
        float w = ClientSize.Width/scale, h = ClientSize.Height/scale;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        long now = DateTime.UtcNow.Ticks;
        TimeSpan left = TimeSpan.FromTicks(Clock.RemainingMs(prefs,now) * TimeSpan.TicksPerMillisecond);
        g.FillEllipse(mint,18,18,6,6);
        string title = "距 " + prefs.EventName;
        if (now >= prefs.End) title = prefs.End == Clock.NewYear && prefs.EventName == "2027 元旦"
            ? "2027，新年快乐" : prefs.EventName + " · 已到达";
        using (var header = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            g.DrawString(title,body,white,new RectangleF(29,10,w-82,24),header);
        for (int i=0;i<3;i++) g.FillEllipse(overMenu ? white : muted,w-39+i*6,19,3,3);
        string day = left.Days.ToString("00");
        var timeState = g.Save();
        g.TranslateTransform((w-W)/2,(h-H)/2);
        if (day.Length > 2) {
            g.TranslateTransform(15,0);
            g.ScaleTransform(306f/(306f+(day.Length-2)*18.2f),1f);
            g.TranslateTransform(-15,0);
        }
        g.DrawString(day,days,white,15,39);
        float dx = day.Length * 18.2f;
        g.DrawString("天",body,muted,19+dx,55);
        g.DrawString(left.ToString(@"hh\:mm\:ss\.fff"),digits,white,43+dx,36);
        g.Restore(timeState);
        g.DrawLine(divider,18,h-42,w-18,h-42);
        g.DrawString(prefs.Accumulating ? "累计金额" : "剩余时间价值",small,muted,16,h-29);
        string amount = "¥" + Clock.Value(prefs,now).ToString("N2",CultureInfo.InvariantCulture);
        using (var right = new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap }) {
            float width = Math.Max(w-112, g.MeasureString(amount,money).Width+2);
            g.TranslateTransform(w-15,0);
            g.ScaleTransform((w-112)/width,1);
            g.DrawString(amount,money,mint,new RectangleF(-width,h-34,width,32),right);
        }
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); overMenu = false; }
    protected override void Dispose(bool disposing)
    {
        if (disposing) {
            timer.Dispose(); menu.Dispose(); small.Dispose(); body.Dispose(); digits.Dispose();
            days.Dispose(); money.Dispose(); white.Dispose(); muted.Dispose(); mint.Dispose(); divider.Dispose();
        }
        base.Dispose(disposing);
    }
}

static class Program
{
    [STAThread]
    public static void Main()
    {
        bool first;
        using (var instance = new Mutex(true, @"Local\YuanDan2027DesktopWidget", out first))
        {
            if (!first) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                       "YuanDanCountdown2027","settings.xml");
            using (var widget = new Widget(Preferences.Read(file), file)) Application.Run(widget);
        }
    }
}
