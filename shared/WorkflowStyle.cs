using System;
using System.Drawing;
using System.Windows.Forms;

namespace WorkFlow
{
    public sealed class SmoothGrid : DataGridView { public SmoothGrid() { DoubleBuffered = true; } }
    public static class Style
    {
        public static readonly Color Ink = Color.FromArgb(28,43,57), Muted = Color.FromArgb(91,107,120), Accent = Color.FromArgb(22,110,98), Background = Color.FromArgb(245,248,250);
        public static void Window(Form form, string title)
        {
            form.Text = title; form.Font = new Font("Segoe UI",10); form.ForeColor = Ink; form.BackColor = Background;
            form.AutoScaleMode = AutoScaleMode.Font; form.MinimumSize = new Size(1060,640); form.Size = new Size(1390,820); form.StartPosition = FormStartPosition.CenterScreen;
        }
        public static Button Button(string text, bool primary = false)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(110,36), Padding = new Padding(12,3,12,3), FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink, Cursor = Cursors.Hand, Margin = new Padding(0,0,10,0), AccessibleName = text };
            b.FlatAppearance.BorderSize = primary ? 0 : 1; b.FlatAppearance.BorderColor = Color.FromArgb(213,222,228); return b;
        }
        public static Label Label(string text, bool title = false)
        {
            return new Label { Text = text, AutoSize = true, Margin = Padding.Empty, ForeColor = title ? Ink : Muted,
                Font = new Font(title ? "Segoe UI Semibold" : "Segoe UI", title ? 23 : 10) };
        }
        public static void Grid(DataGridView g)
        {
            g.Dock = DockStyle.Fill; g.BackgroundColor = Color.White; g.BorderStyle = BorderStyle.None;
            g.AllowUserToAddRows = g.AllowUserToDeleteRows = g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false; g.SelectionMode = DataGridViewSelectionMode.FullRowSelect; g.MultiSelect = false; g.AutoGenerateColumns = false;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal; g.GridColor = Color.FromArgb(235,240,243); g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersHeight = 42; g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(234,240,244); g.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold",9); g.DefaultCellStyle.Font = new Font("Segoe UI",9);
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(221,240,234); g.DefaultCellStyle.SelectionForeColor = Ink;
            g.DefaultCellStyle.Padding = new Padding(7,0,7,0); g.RowTemplate.Height = 34;
        }
        public static TableLayoutPanel Layout(Form form, params float[] heights)
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = heights.Length, Padding = new Padding(24,18,24,14) };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach (float h in heights) p.RowStyles.Add(new RowStyle(h < 0 ? SizeType.Percent : SizeType.Absolute, h < 0 ? 100 : h));
            form.Controls.Add(p); return p;
        }
        public static FlowLayoutPanel Actions(params Control[] controls)
        {
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            p.Controls.AddRange(controls); return p;
        }
    }
}
