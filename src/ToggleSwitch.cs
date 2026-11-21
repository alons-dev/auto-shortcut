using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoShortcut
{
    // On/off switch like Windows 11's: a pill with a round knob. Off: outline and
    // knob in the secondary color, knob on the left. On: accent fill, knob on the right.
    internal sealed class ToggleSwitch : Control
    {
        private const int TrackW = 40;
        private const int TrackH = 20;

        private readonly Theme _theme;
        private bool _checked;
        private bool _hover;
        private bool _pressed;

        public event EventHandler CheckedChanged;

        public ToggleSwitch(string name, Theme theme)
        {
            _theme = theme;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Size = new Size(Ui.S(TrackW + 8), Ui.S(32));   // a bit of room around it to click
            Cursor = Cursors.Hand;
            BackColor = theme.Background;
            Text = name;
            AccessibleName = name;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
                EventHandler h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; _pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Glass.Paint(e, _theme, delegate(Graphics g)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int w = Ui.S(TrackW), h = Ui.S(TrackH);
                // Half-pixel inset so the 1 px outline lands on whole pixels.
                RectangleF track = new RectangleF((Width - w) / 2 + 0.5f, (Height - h) / 2 + 0.5f, w - 1, h - 1);
                using (GraphicsPath path = Pill(track))
                {
                    if (_checked)
                    {
                        using (SolidBrush b = new SolidBrush(_theme.Accent)) g.FillPath(b, path);
                    }
                    else
                    {
                        if (_hover)
                            using (SolidBrush b = new SolidBrush(_theme.Hover)) g.FillPath(b, path);
                        using (Pen p = new Pen(_theme.Subtle, Math.Max(1f, Ui.Scale))) g.DrawPath(p, path);
                    }
                }

                // The knob grows a little under the mouse and stretches while pressed.
                float knob = Ui.S(_hover ? 14 : 12);
                float stretch = _pressed ? Ui.S(3) : 0;
                float pad = (track.Height - knob) / 2;
                float x = _checked ? track.Right - pad - knob - stretch : track.Left + pad;
                RectangleF dot = new RectangleF(x, track.Top + pad, knob + stretch, knob);
                using (GraphicsPath p = Pill(dot))
                using (SolidBrush b = new SolidBrush(_checked ? _theme.AccentText : _theme.Subtle))
                    g.FillPath(b, p);
            });
        }

        // A rectangle with fully round ends (a circle when it's square).
        private static GraphicsPath Pill(RectangleF r)
        {
            float d = r.Height;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            path.CloseFigure();
            return path;
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new SwitchAccessible(this);
        }

        // Screen readers hear "Keep open, check button, checked".
        private sealed class SwitchAccessible : ControlAccessibleObject
        {
            private readonly ToggleSwitch _owner;

            public SwitchAccessible(ToggleSwitch owner) : base(owner) { _owner = owner; }

            public override AccessibleStates State
            {
                get
                {
                    AccessibleStates s = base.State | AccessibleStates.Focusable;
                    return _owner.Checked ? s | AccessibleStates.Checked : s;
                }
            }

            public override string DefaultAction
            {
                get { return "Toggle"; }
            }

            public override void DoDefaultAction()
            {
                _owner.Checked = !_owner.Checked;
            }
        }
    }
}
