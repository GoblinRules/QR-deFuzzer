using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QR_deFuzzer
{
    internal sealed class DarkTrayMenuRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color Background = Color.FromArgb(18, 18, 22);
        private static readonly Color Surface = Color.FromArgb(28, 28, 36);
        private static readonly Color Hover = Color.FromArgb(42, 36, 62);
        private static readonly Color Accent = Color.FromArgb(112, 0, 255);
        private static readonly Color Border = Color.FromArgb(44, 44, 56);
        private static readonly Color Text = Color.FromArgb(238, 238, 246);
        private static readonly Color MutedText = Color.FromArgb(170, 170, 184);

        public DarkTrayMenuRenderer() : base(new DarkTrayColorTable())
        {
            RoundedEdges = true;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Background);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            Rectangle bounds = new Rectangle(System.Drawing.Point.Empty, e.ToolStrip.Size);
            bounds.Width -= 1;
            bounds.Height -= 1;
            e.Graphics.DrawRectangle(pen, bounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle bounds = new Rectangle(System.Drawing.Point.Empty, e.Item.Size);
            bool active = e.Item.Selected || (e.Item is ToolStripMenuItem menuItem && menuItem.DropDown.Visible);

            if (!active)
            {
                using var brush = new SolidBrush(Background);
                e.Graphics.FillRectangle(brush, bounds);
                return;
            }

            Rectangle highlight = new Rectangle(4, 2, bounds.Width - 8, bounds.Height - 4);
            using var path = CreateRoundedRectangle(highlight, 5);
            using var fill = new SolidBrush(Hover);
            using var border = new Pen(Color.FromArgb(90, Accent));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item?.Enabled == true ? Text : MutedText;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(e.ArrowColor);

            System.Drawing.Point center = new System.Drawing.Point(e.ArrowRectangle.Left + e.ArrowRectangle.Width / 2, e.ArrowRectangle.Top + e.ArrowRectangle.Height / 2);
            System.Drawing.Point[] points = e.Direction switch
            {
                ArrowDirection.Right => new[] { new System.Drawing.Point(center.X - 2, center.Y - 5), new System.Drawing.Point(center.X - 2, center.Y + 5), new System.Drawing.Point(center.X + 4, center.Y) },
                ArrowDirection.Left => new[] { new System.Drawing.Point(center.X + 2, center.Y - 5), new System.Drawing.Point(center.X + 2, center.Y + 5), new System.Drawing.Point(center.X - 4, center.Y) },
                ArrowDirection.Up => new[] { new System.Drawing.Point(center.X - 5, center.Y + 2), new System.Drawing.Point(center.X + 5, center.Y + 2), new System.Drawing.Point(center.X, center.Y - 4) },
                _ => new[] { new System.Drawing.Point(center.X - 5, center.Y - 2), new System.Drawing.Point(center.X + 5, center.Y - 2), new System.Drawing.Point(center.X, center.Y + 4) }
            };

            e.Graphics.FillPolygon(brush, points);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Text : MutedText;
            base.OnRenderItemText(e);
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class DarkTrayColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Background;
            public override Color ImageMarginGradientBegin => Background;
            public override Color ImageMarginGradientMiddle => Background;
            public override Color ImageMarginGradientEnd => Background;
            public override Color MenuBorder => Border;
            public override Color MenuItemBorder => Accent;
            public override Color MenuItemSelected => Hover;
            public override Color MenuItemSelectedGradientBegin => Hover;
            public override Color MenuItemSelectedGradientEnd => Hover;
            public override Color MenuItemPressedGradientBegin => Surface;
            public override Color MenuItemPressedGradientMiddle => Surface;
            public override Color MenuItemPressedGradientEnd => Surface;
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Border;
        }
    }
}
