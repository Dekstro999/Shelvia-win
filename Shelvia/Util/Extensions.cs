using System.Drawing;
using System.Windows.Forms;

namespace Shelvia.Util
{
    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientBegin => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientEnd => Color.FromArgb(43, 43, 43);
        public override Color MenuBorder => Color.FromArgb(70, 70, 70);
        public override Color MenuItemBorder => Color.FromArgb(43, 43, 43);
        public override Color MenuItemSelected => Color.FromArgb(70, 70, 70);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(70, 70, 70);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(70, 70, 70);
        public override Color MenuStripGradientBegin => Color.FromArgb(43, 43, 43);
        public override Color MenuStripGradientEnd => Color.FromArgb(43, 43, 43);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(70, 70, 70);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(70, 70, 70);
    }

    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.White;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var rect = new Rectangle(0, 2, e.Item.Width, 1);
            using (var brush = new SolidBrush(Color.FromArgb(70, 70, 70)))
            {
                e.Graphics.FillRectangle(brush, rect);
            }
        }
    }

    public static class Extensions
    {

        public static PointF Move(this PointF point, float offsetX, float offsetY)
        {
            return new PointF(point.X + offsetX, point.Y + offsetY);
        }


        public static Rectangle Shrink(this Rectangle rect, int offset)
        {
            return new Rectangle(rect.X + offset, rect.Y + offset, rect.Width - offset * 2, rect.Height - offset * 2);
        }

    }
}
