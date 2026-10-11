using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HYROLauncherWeb
{
    internal sealed class ApiLogWindow : Form
    {
        private readonly string root;
        private readonly Color accent, surface=Color.FromArgb(20,27,39), muted=Color.FromArgb(139,155,176);
        private readonly ListView list=new ListView();
        private readonly TextBox detail=new TextBox(), search=new TextBox();
        private readonly Label status=new Label(), capture=new Label();
        private readonly CheckBox follow=new CheckBox();
        private readonly ComboBox filter=new ComboBox(), outcome=new ComboBox();
        private readonly Timer timer=new Timer();
        private readonly List<ApiLogEntry> rows=new List<ApiLogEntry>();
        private readonly Label requests=new Label(), failures=new Label(), latency=new Label(), transferred=new Label();
        private long last;

        internal ApiLogWindow(string root,Color accent)
        {
            this.root=root;this.accent=accent;
            string icon=System.IO.Path.Combine(root,"assets","app.ico");if(System.IO.File.Exists(icon))try{Icon=new Icon(icon);}catch{}
            Text="HYRO · API Monitor";Size=new Size(1180,770);MinimumSize=new Size(820,540);StartPosition=FormStartPosition.CenterScreen;
            BackColor=Color.FromArgb(11,16,25);ForeColor=Color.FromArgb(232,240,249);Font=new Font("Segoe UI",9);DoubleBuffered=true;
            HandleCreated+=delegate{try{int dark=1;if(DwmSetWindowAttribute(Handle,20,ref dark,4)!=0)DwmSetWindowAttribute(Handle,19,ref dark,4);}catch{}};
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(22,18,22,12),BackColor=BackColor};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,67));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,86));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,93));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
            var heading=new Panel {Dock=DockStyle.Fill};
            heading.Controls.Add(new Label {Text="API Monitor",AutoSize=true,Font=new Font("Segoe UI",23,FontStyle.Bold),Location=new Point(0,-3),ForeColor=ForeColor});
            heading.Controls.Add(new Label {Text="HYRO  /  NETWORK ACTIVITY",AutoSize=true,Font=new Font("Segoe UI",8),ForeColor=muted,Location=new Point(2,39)});
            capture.Text="●  LIVE";capture.ForeColor=Color.FromArgb(111,224,176);capture.Font=new Font("Segoe UI",9,FontStyle.Bold);capture.AutoSize=true;capture.Anchor=AnchorStyles.Top|AnchorStyles.Right;
            heading.Controls.Add(capture);heading.Resize+=delegate{capture.Location=new Point(Math.Max(0,heading.Width-capture.Width-4),12);};layout.Controls.Add(heading,0,0);
            var stats=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=new Padding(0,0,0,12)};
            for(int i=0;i<4;i++)stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            stats.Controls.Add(StatTile("REQUESTS",requests,accent),0,0);stats.Controls.Add(StatTile("ERRORS",failures,Color.FromArgb(247,139,151)),1,0);
            stats.Controls.Add(StatTile("AVG. RESPONSE",latency,Color.FromArgb(179,165,255)),2,0);stats.Controls.Add(StatTile("RECEIVED",transferred,Color.FromArgb(111,224,176)),3,0);layout.Controls.Add(stats,0,1);
            var toolbar=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=6,RowCount=1,Margin=Padding.Empty};
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,104));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,98));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));
            var searchFrame=new RoundedPanel {Dock=DockStyle.Fill,BackColor=surface,Padding=new Padding(12,9,8,8),Margin=new Padding(0,2,9,10)};
            search.BorderStyle=BorderStyle.None;search.BackColor=surface;search.ForeColor=ForeColor;search.Dock=DockStyle.Fill;search.Font=new Font("Segoe UI",10);
            var placeholder=new Label {Text="Tìm URL, nguồn hoặc hoạt động…",Dock=DockStyle.Fill,ForeColor=muted,BackColor=surface,Font=search.Font,Cursor=Cursors.IBeam};
            placeholder.Click+=delegate{search.Focus();};search.TextChanged+=delegate{placeholder.Visible=search.TextLength==0;RenderRows();};searchFrame.Controls.Add(search);searchFrame.Controls.Add(placeholder);placeholder.BringToFront();toolbar.Controls.Add(searchFrame,0,0);
            var tip=new ToolTip();tip.SetToolTip(search,"Tìm URL, nguồn hoặc hoạt động");
            ConfigureCombo(filter,new object[]{"Tất cả nguồn","HYTCG","ACDB","Zerochan","AniList","Steam","IGDB","Twitch","HyBrowser"});
            ConfigureCombo(outcome,new object[]{"Mọi trạng thái","Lỗi","Chậm > 1s"});toolbar.Controls.Add(filter,1,0);toolbar.Controls.Add(outcome,2,0);
            filter.SelectedIndexChanged+=delegate{RenderRows();};outcome.SelectedIndexChanged+=delegate{RenderRows();};
            var clear=ActionButton("Xóa màn hình");clear.Click+=delegate{rows.Clear();RenderRows();UpdateStats();};toolbar.Controls.Add(clear,3,0);
            var folder=ActionButton("Thư mục ↗");folder.Click+=delegate{try{Process.Start(new ProcessStartInfo(System.IO.Path.Combine(root,"logs")){UseShellExecute=true});}catch{}};toolbar.Controls.Add(folder,4,0);
            follow.Text="Theo dõi";follow.Checked=true;follow.ForeColor=muted;follow.AutoSize=true;follow.Anchor=AnchorStyles.Left|AnchorStyles.Top;follow.Margin=new Padding(11,9,0,0);toolbar.Controls.Add(follow,5,0);layout.Controls.Add(toolbar,0,2);
            list.Dock=DockStyle.Fill;list.View=View.Details;list.FullRowSelect=true;list.MultiSelect=false;list.HideSelection=false;list.BorderStyle=BorderStyle.None;
            list.BackColor=BackColor;list.ForeColor=ForeColor;list.OwnerDraw=true;list.HeaderStyle=ColumnHeaderStyle.Nonclickable;
            list.Columns.Add("TIME",112);list.Columns.Add("SOURCE",91);list.Columns.Add("METHOD",73);list.Columns.Add("STATUS",76);list.Columns.Add("DURATION",92);list.Columns.Add("SIZE",80);list.Columns.Add("ENDPOINT / ACTIVITY",450);
            var height=new ImageList {ImageSize=new Size(1,33),ColorDepth=ColorDepth.Depth32Bit};list.SmallImageList=height;
            list.DrawColumnHeader+=DrawHeader;list.DrawSubItem+=DrawCell;list.DrawItem+=delegate(object sender,DrawListViewItemEventArgs e){if(list.View!=View.Details)e.DrawDefault=true;};
            list.Resize+=delegate{list.Columns[6].Width=Math.Max(150,list.ClientSize.Width-525);};
            list.SelectedIndexChanged+=delegate{if(list.SelectedItems.Count==1){var entry=(ApiLogEntry)list.SelectedItems[0].Tag;detail.Text=entry.Source+"  ·  "+entry.Method+"  ·  "+entry.Status+"  ·  "+entry.Milliseconds+" ms"+Environment.NewLine+(entry.Url==""?entry.Detail:entry.Url)+(entry.Url!=""&&entry.Detail!=""?Environment.NewLine+entry.Detail:"");}};
            list.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.C&&detail.Text!=""){try{Clipboard.SetText(detail.Text);}catch{}e.Handled=true;}};
            layout.Controls.Add(list,0,3);
            var detailFrame=new RoundedPanel {Dock=DockStyle.Fill,BackColor=surface,Padding=new Padding(14,12,14,10),Margin=new Padding(0,12,0,0)};
            detail.Multiline=true;detail.ReadOnly=true;detail.BorderStyle=BorderStyle.None;detail.BackColor=surface;detail.ForeColor=Color.FromArgb(176,194,215);detail.Dock=DockStyle.Fill;detail.Font=new Font("Consolas",9);detail.Text="Chọn một request để xem chi tiết.";detail.TextChanged+=delegate{detail.ScrollBars=TextRenderer.MeasureText(detail.Text,detail.Font,new Size(Math.Max(1,detail.ClientSize.Width-10),Int32.MaxValue),TextFormatFlags.WordBreak).Height>detail.ClientSize.Height?ScrollBars.Vertical:ScrollBars.None;};detailFrame.Controls.Add(detail);layout.Controls.Add(detailFrame,0,4);
            status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;status.AutoEllipsis=true;status.ForeColor=muted;status.Font=new Font("Segoe UI",8);layout.Controls.Add(status,0,5);Controls.Add(layout);
            timer.Interval=250;timer.Tick+=delegate{RefreshRows();};UpdateStats();
            Shown+=delegate{try{ApiActivityLog.Start(root);timer.Start();status.Text="●  Đang ghi  ·  "+ApiActivityLog.FilePath;}catch{capture.Text="●  ERROR";capture.ForeColor=Color.FromArgb(247,139,151);status.Text="Không mở được file log";}};
            FormClosing+=delegate{timer.Stop();ApiActivityLog.Stop();};FormClosed+=delegate{timer.Dispose();height.Dispose();tip.Dispose();};
        }
        private Panel StatTile(string title,Label value,Color color)
        {
            var tile=new RoundedPanel {Dock=DockStyle.Fill,BackColor=surface,Margin=new Padding(0,0,10,0),Padding=new Padding(15,10,15,8)};
            var label=new Label {Text=title,Dock=DockStyle.Top,Height=18,Font=new Font("Segoe UI",8),ForeColor=muted};
            value.Text="0";value.Dock=DockStyle.Fill;value.Font=new Font("Segoe UI",19,FontStyle.Bold);value.ForeColor=color;tile.Controls.Add(value);tile.Controls.Add(label);return tile;
        }
        private sealed class RoundedPanel:Panel
        {
            protected override void OnResize(EventArgs e){base.OnResize(e);if(Width<18||Height<18)return;using(var path=new System.Drawing.Drawing2D.GraphicsPath()){path.AddArc(0,0,16,16,180,90);path.AddArc(Width-16,0,16,16,270,90);path.AddArc(Width-16,Height-16,16,16,0,90);path.AddArc(0,Height-16,16,16,90,90);path.CloseFigure();var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
        }
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
        private Button ActionButton(string text){return new Button {Text=text,Dock=DockStyle.Fill,Margin=new Padding(5,2,0,10),FlatStyle=FlatStyle.Flat,BackColor=surface,ForeColor=muted,Font=new Font("Segoe UI",8)};}
        private void ConfigureCombo(ComboBox combo,object[] values){combo.DropDownStyle=ComboBoxStyle.DropDownList;combo.FlatStyle=FlatStyle.Flat;combo.Dock=DockStyle.Fill;combo.Margin=new Padding(2,3,7,0);combo.BackColor=surface;combo.ForeColor=ForeColor;combo.Items.AddRange(values);combo.SelectedIndex=0;}
        private static bool IsError(ApiLogEntry row){return row.Status=="ERROR"||row.Status.StartsWith("4")||row.Status.StartsWith("5");}
        private void RefreshRows()
        {
            var incoming=ApiActivityLog.ReadAfter(last);if(incoming.Length>0){foreach(var entry in incoming){last=entry.Sequence;rows.Add(entry);}if(rows.Count>2000)rows.RemoveRange(0,rows.Count-2000);RenderRows();UpdateStats();}
            status.Text=ApiActivityLog.FileError!=""?ApiActivityLog.FileError:"●  Đang ghi  ·  "+ApiActivityLog.FilePath;
        }
        private void UpdateStats(){var completed=rows.Where(r=>r.RequestId>0&&r.Status!="START").ToArray();requests.Text=completed.Length.ToString("N0");failures.Text=completed.Count(IsError).ToString("N0");latency.Text=completed.Length==0?"—":Math.Round(completed.Average(r=>(double)r.Milliseconds))+" ms";transferred.Text=Bytes(completed.Sum(r=>r.Bytes));}
        private static string Bytes(long bytes){return bytes<1024?bytes+" B":bytes<1048576?(bytes/1024d).ToString("0.0")+" KB":(bytes/1048576d).ToString("0.0")+" MB";}
        private void RenderRows()
        {
            long selected=list.SelectedItems.Count==1?((ApiLogEntry)list.SelectedItems[0].Tag).Sequence:0;
            string source=filter.SelectedIndex<=0?"":Convert.ToString(filter.SelectedItem),query=search.Text.Trim();list.BeginUpdate();
            try{list.Items.Clear();foreach(var entry in rows){if(source!=""&&entry.Source.IndexOf(source,StringComparison.OrdinalIgnoreCase)<0)continue;if(outcome.SelectedIndex==1&&!IsError(entry)||outcome.SelectedIndex==2&&entry.Milliseconds<=1000)continue;
                if(query!=""&&(entry.Url+" "+entry.Detail+" "+entry.Source+" "+entry.Status).IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
                var item=new ListViewItem(new[]{entry.Time.ToString("HH:mm:ss.fff"),entry.Source,entry.Method,entry.Status,entry.Status=="START"?"—":entry.Milliseconds+" ms",entry.Bytes==0?"—":Bytes(entry.Bytes),entry.Url==""?entry.Detail:entry.Url});item.Tag=entry;list.Items.Add(item);if(entry.Sequence==selected)item.Selected=true;
            }while(list.Items.Count>1500)list.Items.RemoveAt(0);if(follow.Checked&&list.Items.Count>0)list.Items[list.Items.Count-1].EnsureVisible();}finally{list.EndUpdate();}
        }
        private void DrawHeader(object sender,DrawListViewColumnHeaderEventArgs e)
        {
            using(var brush=new SolidBrush(surface))e.Graphics.FillRectangle(brush,e.Bounds);
            using(var font=new Font("Segoe UI",8))TextRenderer.DrawText(e.Graphics,e.Header.Text,font,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-14,e.Bounds.Height),muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
        private void DrawCell(object sender,DrawListViewSubItemEventArgs e)
        {
            var entry=(ApiLogEntry)e.Item.Tag;Color background=e.Item.Selected?Color.FromArgb(35,52,70):e.ItemIndex%2==0?BackColor:Color.FromArgb(13,20,30);
            using(var brush=new SolidBrush(background))e.Graphics.FillRectangle(brush,e.Bounds);
            Color color=e.ColumnIndex==0||e.ColumnIndex==4||e.ColumnIndex==5?muted:ForeColor;
            if(e.ColumnIndex==1)color=accent;if(e.ColumnIndex==3)color=IsError(entry)?Color.FromArgb(247,139,151):entry.Status=="START"||entry.Status=="INFO"?muted:Color.FromArgb(111,224,176);
            using(var font=new Font(e.ColumnIndex==6||e.ColumnIndex==0?"Consolas":"Segoe UI",9,e.ColumnIndex==3?FontStyle.Bold:FontStyle.Regular))TextRenderer.DrawText(e.Graphics,e.SubItem.Text,font,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-14,e.Bounds.Height),color,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        }
    }
}
