using System;
using System.Drawing;
using System.Windows.Forms;

namespace HYROLauncherWeb
{
    // A clipped viewport avoids WinForms AutoScroll creating horizontal bars
    // when the sidebar changes width. Only an unobtrusive custom thumb is drawn.
    internal sealed class HyBrowserTabsViewport : Panel
    {
        public event EventHandler OffsetChanged;
        public int ScrollOffset { get; private set; }
        public int ContentHeight { get; private set; }
        private bool draggingThumb;
        private int dragStartY;
        private int dragStartOffset;

        public HyBrowserTabsViewport()
        {
            AutoScroll = false;
            DoubleBuffered = true;
        }

        public void Configure(int contentHeight)
        {
            ContentHeight = Math.Max(0, contentHeight);
            SetOffset(ScrollOffset);
            Invalidate();
        }

        public void Reveal(int top, int height)
        {
            if (top < ScrollOffset) SetOffset(top);
            else if (top + height > ScrollOffset + ClientSize.Height)
                SetOffset(top + height - ClientSize.Height);
        }

        public void ScrollWheel(int delta)
        {
            SetOffset(ScrollOffset - delta * 96 / 120);
        }

        private void SetOffset(int value)
        {
            value = Math.Max(0, Math.Min(value, Math.Max(0, ContentHeight - ClientSize.Height)));
            if (value == ScrollOffset) return;
            ScrollOffset = value;
            if (OffsetChanged != null) OffsetChanged(this, EventArgs.Empty);
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollWheel(e.Delta);
            var handled = e as HandledMouseEventArgs;
            if (handled != null) handled.Handled = true;
        }

        private Rectangle ThumbBounds()
        {
            if (ContentHeight <= ClientSize.Height || ClientSize.Height <= 0) return Rectangle.Empty;
            int height = Math.Min(ClientSize.Height, Math.Max(28, ClientSize.Height * ClientSize.Height / ContentHeight));
            int top = (int)((long)ScrollOffset * (ClientSize.Height - height) / Math.Max(1, ContentHeight - ClientSize.Height));
            return new Rectangle(Math.Max(0, Width - 4), top, 3, height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle thumb = ThumbBounds();
            if (!thumb.IsEmpty) using (var brush = new SolidBrush(Color.FromArgb(76, 94, 116)))
                e.Graphics.FillRectangle(brush, thumb);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Rectangle thumb = ThumbBounds();
            if (e.Button != MouseButtons.Left || thumb.IsEmpty || e.X < Width - 8) return;
            if (e.Y < thumb.Top || e.Y >= thumb.Bottom)
                SetOffset(ScrollOffset + (e.Y < thumb.Top ? -ClientSize.Height : ClientSize.Height));
            draggingThumb = true;
            dragStartY = e.Y;
            dragStartOffset = ScrollOffset;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!draggingThumb) return;
            Rectangle thumb = ThumbBounds();
            SetOffset(dragStartOffset + (int)((long)(e.Y - dragStartY) * Math.Max(0, ContentHeight - ClientSize.Height) / Math.Max(1, ClientSize.Height - thumb.Height)));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            draggingThumb = false;
            Capture = false;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) draggingThumb = false;
        }
    }
}
