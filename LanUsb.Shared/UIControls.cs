using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace LanUsb.Shared;

public class ModernButton : Button
{
    private bool _isHovered;
    private bool _isPressed;

    [DefaultValue(6)]
    public int BorderRadius { get; set; } = 6;

    public Color NormalColor { get; set; } = UITheme.SlateButton;
    public Color HoverColor { get; set; } = UITheme.SlateButtonHover;
    public Color PressedColor { get; set; } = UITheme.BgDarkest;
    public Color CustomBorderColor { get; set; } = Color.Transparent;

    public ModernButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        ForeColor = UITheme.TextPrimary;
        Font = UITheme.RegularFont;
        Cursor = Cursors.Hand;
        Size = new Size(110, 32);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        _isPressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _isPressed = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        var fillColor = !Enabled ? Color.FromArgb(45, 52, 68)
            : _isPressed ? PressedColor
            : _isHovered ? HoverColor
            : NormalColor;

        using (var path = UITheme.CreateRoundedPath(rect, BorderRadius))
        {
            using (var brush = new SolidBrush(fillColor))
            {
                g.FillPath(brush, path);
            }

            if (CustomBorderColor != Color.Transparent)
            {
                using var pen = new Pen(CustomBorderColor, 1f);
                g.DrawPath(pen, path);
            }
        }

        var textColor = !Enabled ? UITheme.TextMuted : ForeColor;
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            rect,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

public class StatCard : Panel
{
    private string _iconText = "⚡";
    private string _title = "STATUS";
    private string _valueText = "—";
    private string _subText = "";
    private Color _accentColor = UITheme.Blue;

    public string IconText
    {
        get => _iconText;
        set { _iconText = value; Invalidate(); }
    }

    public string Title
    {
        get => _title;
        set { _title = value; Invalidate(); }
    }

    public string ValueText
    {
        get => _valueText;
        set { _valueText = value; Invalidate(); }
    }

    public string SubText
    {
        get => _subText;
        set { _subText = value; Invalidate(); }
    }

    public Color AccentColor
    {
        get => _accentColor;
        set { _accentColor = value; Invalidate(); }
    }

    public StatCard()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        Size = new Size(190, 68);
        BackColor = UITheme.BgSurface;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var rect = ClientRectangle;
        using var path = UITheme.CreateRoundedPath(rect, 8);

        // Fill background
        using (var brush = new SolidBrush(UITheme.BgSurface))
        {
            g.FillPath(brush, path);
        }

        // Border
        using (var borderPen = new Pen(UITheme.BorderSubtle, 1f))
        {
            g.DrawPath(borderPen, path);
        }

        // Left accent indicator pill
        var accentRect = new Rectangle(0, 8, 4, Height - 16);
        using (var accentBrush = new SolidBrush(_accentColor))
        {
            g.FillRectangle(accentBrush, accentRect);
        }

        // Icon
        var iconRect = new Rectangle(14, 12, 34, 44);
        using (var iconFont = new Font("Segoe UI Emoji", 14f))
        {
            TextRenderer.DrawText(
                g,
                _iconText,
                iconFont,
                iconRect,
                _accentColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // Texts (Title and Value)
        var titleRect = new Rectangle(52, 10, Width - 56, 16);
        TextRenderer.DrawText(
            g,
            _title.ToUpperInvariant(),
            UITheme.SmallBoldFont,
            titleRect,
            UITheme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        var valRect = new Rectangle(52, 26, Width - 56, 24);
        TextRenderer.DrawText(
            g,
            _valueText,
            UITheme.TitleFont,
            valRect,
            UITheme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        if (!string.IsNullOrEmpty(_subText))
        {
            var subRect = new Rectangle(52, 48, Width - 56, 16);
            TextRenderer.DrawText(
                g,
                _subText,
                UITheme.SmallFont,
                subRect,
                UITheme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }
}

public class SearchBoxControl : Panel
{
    private readonly TextBox _textBox = new();
    private readonly Label _lblIcon = new();
    private readonly Button _btnClear = new();

    public event EventHandler? SearchTextChanged;

    public string SearchText
    {
        get => _textBox.Text;
        set => _textBox.Text = value;
    }

    public SearchBoxControl()
    {
        Size = new Size(240, 32);
        BackColor = UITheme.BgInput;

        _lblIcon.Text = "🔍";
        _lblIcon.Font = new Font("Segoe UI Emoji", 9f);
        _lblIcon.ForeColor = UITheme.TextMuted;
        _lblIcon.Size = new Size(24, 28);
        _lblIcon.Location = new Point(4, 2);
        _lblIcon.TextAlign = ContentAlignment.MiddleCenter;

        _textBox.BorderStyle = BorderStyle.None;
        _textBox.BackColor = UITheme.BgInput;
        _textBox.ForeColor = UITheme.TextPrimary;
        _textBox.Font = UITheme.RegularFont;
        _textBox.PlaceholderText = "Szukaj urządzenia...";
        _textBox.Location = new Point(30, 6);
        _textBox.Width = Width - 60;
        _textBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _textBox.TextChanged += (_, _) =>
        {
            _btnClear.Visible = !string.IsNullOrEmpty(_textBox.Text);
            SearchTextChanged?.Invoke(this, EventArgs.Empty);
        };

        _btnClear.Text = "✕";
        _btnClear.Font = new Font("Segoe UI", 8f);
        _btnClear.ForeColor = UITheme.TextMuted;
        _btnClear.FlatStyle = FlatStyle.Flat;
        _btnClear.FlatAppearance.BorderSize = 0;
        _btnClear.BackColor = Color.Transparent;
        _btnClear.Size = new Size(22, 22);
        _btnClear.Location = new Point(Width - 26, 4);
        _btnClear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnClear.Cursor = Cursors.Hand;
        _btnClear.Visible = false;
        _btnClear.Click += (_, _) => _textBox.Clear();

        Controls.Add(_lblIcon);
        Controls.Add(_textBox);
        Controls.Add(_btnClear);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(UITheme.Border, 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        Invalidate();
    }
}

public class DoubleBufferedListView : ListView
{
    private readonly ImageList _spacingImageList = new();

    public DoubleBufferedListView()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.ResizeRedraw,
            true);

        View = View.Details;
        FullRowSelect = true;
        GridLines = false;
        MultiSelect = false;
        BorderStyle = BorderStyle.None;
        BackColor = UITheme.BgDarkest;
        ForeColor = UITheme.TextPrimary;
        Font = UITheme.RegularFont;

        // Give rows comfortable breathing room (28px height)
        _spacingImageList.ImageSize = new Size(1, 26);
        SmallImageList = _spacingImageList;
    }
}
