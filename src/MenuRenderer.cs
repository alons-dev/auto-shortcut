using System.Drawing;
using System.Windows.Forms;

namespace AutoShortcut
{
    // WinForms menus are always drawn light. This renderer swaps their palette for
    // the theme's (light or dark) and Ui.ApplyFrame rounds their corners.
    internal sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly Theme _theme;

        private MenuRenderer(Theme theme) : base(new MenuColors(theme))
        {
            _theme = theme;
            RoundedEdges = false;
        }

        public static void Apply(ContextMenuStrip menu, Theme theme)
        {
            menu.Renderer = new MenuRenderer(theme);
            menu.BackColor = theme.MenuBack;
            menu.ForeColor = theme.Foreground;
            menu.Font = new Font("Segoe UI", 9.75F);
            menu.ShowImageMargin = false;
            menu.Padding = new Padding(Ui.S(4));
            // More room around each item, like Windows 11 menus.
            menu.ItemAdded += delegate(object sender, ToolStripItemEventArgs e)
            {
                if (!(e.Item is ToolStripSeparator)) e.Item.Padding = new Padding(Ui.S(10), Ui.S(4), Ui.S(10), Ui.S(4));
            };
            menu.HandleCreated += delegate { Ui.ApplyFrame(menu.Handle, theme.Dark, Ui.CornerRoundSmall); };
        }

        // Rounded background for the selected item, instead of the classic bordered rectangle.
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Rectangle r = new Rectangle(Ui.S(2), 0, e.Item.Width - Ui.S(4) - 1, e.Item.Height - 1);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (System.Drawing.Drawing2D.GraphicsPath path = Ui.RoundedRect(r, Ui.S(4)))
            using (SolidBrush brush = new SolidBrush(_theme.Hover))
                e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _theme.Foreground : _theme.Subtle;
            base.OnRenderItemText(e);
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            private readonly Theme _t;
            public MenuColors(Theme t) { _t = t; }

            public override Color ToolStripDropDownBackground { get { return _t.MenuBack; } }
            public override Color ImageMarginGradientBegin { get { return _t.MenuBack; } }
            public override Color ImageMarginGradientMiddle { get { return _t.MenuBack; } }
            public override Color ImageMarginGradientEnd { get { return _t.MenuBack; } }
            public override Color MenuBorder { get { return _t.Border; } }
            public override Color MenuItemBorder { get { return _t.Hover; } }
            public override Color MenuItemSelected { get { return _t.Hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return _t.Hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return _t.Hover; } }
            public override Color MenuItemPressedGradientBegin { get { return _t.Pressed; } }
            public override Color MenuItemPressedGradientEnd { get { return _t.Pressed; } }
            public override Color SeparatorDark { get { return _t.Border; } }
            public override Color SeparatorLight { get { return _t.MenuBack; } }
        }
    }
}
