using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EU2.Map;
using EU2.Map.Codec;
using LayerPainter;

namespace EU2MapEditorControls
{
	[DefaultEvent("TrackerMoved")]
	public class MapView : UserControl
	{
		public delegate void ContextMenuEventHandler(object sender, ContextMenuEventArgs e);

		public delegate void TrackerMovedEventHandler(object sender, TrackerMovedEventArgs e);

		public delegate void SelectionChangedHandler(object sender, EventArgs e);

		public static ShaderProxy DefaultShaderProxy = new DefaultShaderProxy();

		private Panel pnlMap;

		private PictureBox pbMap;

		private Timer tmrResize;

		private Timer tmrScroll;

		private IContainer components;

		private Lightmap source;

		private ShaderProxy shaderproxy;

		private Point origin;

		private ColorDetail bitdepth;

		private bool drawborders;

		private bool drawboundboxes;

		private bool drawadjacent;

		private bool drawsprites;

		private PictureBox minimap;

		private bool drawtrackers;

		private bool drawdateline;

		private DateTime lastMouseMoveTime;

		private MapGridMode gridmode;

		private Color trackercolor;

		private int trackeropacity;

		private Color gridcolor;

		private int gridopacity;

		private int scrollzonesize;

		private Color scrollzonecolor;

		private int scrollzoneopacity;

		private bool allowSelection;

		private bool mapMouseOver;

		private Size mapDelta;

		private Point mapDragOrigin;

		private Point mapDragLocation;

		private Point mapMouseLocation;

		private bool mapSelecting;

		private bool mapSelectionCrossedDateLine;

		private DateTime mapDragStart;

		private Rectangle mapSelection;

		[Category("Behavior")]
		[Description("Indicates of the map allows selecting an area.")]
		public bool AllowSelection
		{
			get
			{
				return allowSelection;
			}
			set
			{
				allowSelection = value;
				if (mapSelecting && !allowSelection)
				{
					mapSelecting = false;
					Refresh();
				}
			}
		}

		[Category("Behavior")]
		[Description("The currently selected area.")]
		public Rectangle Selection
		{
			get
			{
				return mapSelection;
			}
			set
			{
				if (value != mapSelection)
				{
					mapSelection = value;
					Refresh();
					OnSelectionChanged(EventArgs.Empty);
				}
			}
		}

		[Category("Map Appearance")]
		[Description("The origin of the mapview. This point specifies the upper left corner of the map and determines from where the map is drawn. This should be expressed in blocks, not actual pixels.")]
		public Point OriginBlocks
		{
			get
			{
				return origin;
			}
			set
			{
				if (origin != value)
				{
					MoveMapBlocks(value.X - origin.X, value.Y - origin.Y);
				}
			}
		}

		[Category("Map Appearance")]
		[Description("The origin of the mapview in actual pixels. The Source property must be set for this property to return a correct value.")]
		public Point OriginActual
		{
			get
			{
				if (source == null)
				{
					return new Point(-1, -1);
				}
				return source.CoordMap.BlocksToActual(origin);
			}
		}

		[Description("The lightmap used as source for map drawing.")]
		[Category("Map Appearance")]
		public Lightmap Source
		{
			get
			{
				return source;
			}
			set
			{
				if (source != value)
				{
					source = value;
					DrawMap();
				}
			}
		}

		[Category("Map Appearance")]
		[Description("The shader used to shade the map.")]
		public ShaderProxy ShaderProxy
		{
			get
			{
				return shaderproxy;
			}
			set
			{
				if (value == null)
				{
					value = DefaultShaderProxy;
				}
				if (shaderproxy != value)
				{
					shaderproxy = value;
					shaderproxy.DrawBorders = drawborders;
					DrawMap();
				}
			}
		}

		[Category("Map Appearance")]
		[DefaultValue(MapGridMode.Full)]
		[Description("How to draw the map grid.")]
		public MapGridMode GridMode
		{
			get
			{
				return gridmode;
			}
			set
			{
				if (value != gridmode)
				{
					gridmode = value;
					pbMap.Refresh();
				}
			}
		}

		[DefaultValue(false)]
		[Category("Map Appearance")]
		[Description("Indicates whether to draw borders on the map.")]
		public bool DrawBorders
		{
			get
			{
				return drawborders;
			}
			set
			{
				if (value != drawborders)
				{
					drawborders = value;
					shaderproxy.DrawBorders = drawborders;
					DrawMap();
				}
			}
		}

		[Description("Indicates whether to draw province adjacency arrows on the map.")]
		[DefaultValue(false)]
		[Category("Map Appearance")]
		public bool DrawAdjacenties
		{
			get
			{
				return drawadjacent;
			}
			set
			{
				if (value != drawadjacent)
				{
					drawadjacent = value;
					pbMap.Refresh();
				}
			}
		}

		[Description("Indicates whether to draw province boundboxes on the map. The Boundboxes property must be set for this flag to have effect.")]
		[DefaultValue(false)]
		[Category("Map Appearance")]
		public bool DrawBoundboxes
		{
			get
			{
				return drawboundboxes;
			}
			set
			{
				if (value != drawboundboxes)
				{
					drawboundboxes = value;
					pbMap.Refresh();
				}
			}
		}

		[Category("Map Appearance")]
		[Description("Indicates whether to draw the sprites (as defined in each province) on the map. The map source must have a link to provinces for this to have effect.")]
		[DefaultValue(false)]
		public bool DrawSprites
		{
			get
			{
				return drawsprites;
			}
			set
			{
				if (value != drawsprites)
				{
					drawsprites = value;
					pbMap.Refresh();
				}
			}
		}

		[Description("Picturebox representing the minimap.")]
		[DefaultValue(null)]
		[Category("Map Appearance")]
		public PictureBox Minimap
		{
			get
			{
				return minimap;
			}
			set
			{
				if (minimap != value)
				{
					if (minimap != null)
					{
						minimap.Paint -= minimap_Paint;
						minimap.MouseDown -= minimap_MouseDown;
					}
					minimap = value;
					minimap.Paint += minimap_Paint;
					minimap.MouseDown += minimap_MouseDown;
				}
			}
		}

		[DefaultValue(BorderStyle.Fixed3D)]
		[Description("Indicates whether or not the mapview should have a border.")]
		[Category("Appearance")]
		public new BorderStyle BorderStyle
		{
			get
			{
				return pnlMap.BorderStyle;
			}
			set
			{
				pnlMap.BorderStyle = value;
			}
		}

		[Category("Appearance")]
		[DefaultValue(false)]
		[Description("How detailed to draw the map colors. Low will draw a 16bpp image, High will draw a 32bpp image.")]
		public ColorDetail ColorDetail
		{
			get
			{
				return bitdepth;
			}
			set
			{
				if (value != bitdepth)
				{
					bitdepth = value;
					DrawMap();
				}
			}
		}

		[Description("Size of the scroll area. Set this value to 0 to display the scroll area functionality.")]
		[DefaultValue(32)]
		[Category("Appearance")]
		public int ScrollZoneSize
		{
			get
			{
				return scrollzonesize;
			}
			set
			{
				if (scrollzonesize >= 0 && scrollzonesize != value)
				{
					scrollzonesize = value;
					pbMap.Refresh();
				}
			}
		}

		[Description("Specifies the color of the scrollzone.")]
		[Category("Appearance")]
		public Color ScrollZoneColor
		{
			get
			{
				return scrollzonecolor;
			}
			set
			{
				if (!(value == scrollzonecolor))
				{
					scrollzonecolor = value;
					pbMap.Refresh();
				}
			}
		}

		[Description("Specifies the opacity of the scrollzone.")]
		[Category("Appearance")]
		[DefaultValue(70)]
		public int ScrollZoneOpacity
		{
			get
			{
				return scrollzoneopacity;
			}
			set
			{
				if (value < 0 || value > 255)
				{
					throw new ArgumentOutOfRangeException();
				}
				if (value != scrollzoneopacity)
				{
					scrollzoneopacity = value;
					pbMap.Refresh();
				}
			}
		}

		[Category("Appearance")]
		[DefaultValue(true)]
		[Description("Indicates whether to draw mouse trackers or not.")]
		public bool DrawMouseTrackers
		{
			get
			{
				return drawtrackers;
			}
			set
			{
				if (value != drawtrackers)
				{
					drawtrackers = value;
					pbMap.Refresh();
				}
			}
		}

		[Description("Specifies which color the mouse trackers have.")]
		[Category("Appearance")]
		public Color MouseTrackerColor
		{
			get
			{
				return trackercolor;
			}
			set
			{
				if (!(value == trackercolor))
				{
					trackercolor = value;
					pbMap.Refresh();
				}
			}
		}

		[Category("Appearance")]
		[DefaultValue(70)]
		[Description("Specifies the opacity of the mouse trackers.")]
		public int MouseTrackerOpacity
		{
			get
			{
				return trackeropacity;
			}
			set
			{
				if (value < 0 || value > 255)
				{
					throw new ArgumentOutOfRangeException();
				}
				if (value != trackeropacity)
				{
					trackeropacity = value;
					pbMap.Refresh();
				}
			}
		}

		[Description("Specifies which color the grid is drawn.")]
		[Category("Appearance")]
		public Color GridColor
		{
			get
			{
				return gridcolor;
			}
			set
			{
				if (!(value == gridcolor))
				{
					gridcolor = value;
					pbMap.Refresh();
				}
			}
		}

		[Description("Specifies the opacity of the grid.")]
		[Category("Appearance")]
		[DefaultValue(60)]
		public int GridOpacity
		{
			get
			{
				return gridopacity;
			}
			set
			{
				if (value < 0 || value > 255)
				{
					throw new ArgumentOutOfRangeException();
				}
				if (value != gridopacity)
				{
					gridopacity = value;
					pbMap.Refresh();
				}
			}
		}

		public new event ContextMenuEventHandler ContextMenu;

		public event TrackerMovedEventHandler TrackerMoved;

		public event SelectionChangedHandler SelectionChanged;

		public MapView()
		{
			InitializeComponent();
			SetStyle(ControlStyles.Selectable, true);
			Focus();
			source = null;
			shaderproxy = DefaultShaderProxy;
			bitdepth = ColorDetail.Low;
			drawsprites = false;
			drawboundboxes = false;
			drawadjacent = false;
			drawborders = false;
			minimap = null;
			drawdateline = true;
			drawtrackers = true;
			gridmode = MapGridMode.Full;
			scrollzonesize = 32;
			trackercolor = Color.Yellow;
			trackeropacity = 70;
			gridcolor = Color.LightGray;
			gridopacity = 60;
			scrollzonecolor = SystemColors.ActiveCaption;
			scrollzoneopacity = 70;
			mapMouseOver = false;
			mapMouseLocation = new Point(-1, -1);
			origin = new Point(0, 0);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.pnlMap = new System.Windows.Forms.Panel();
			this.pbMap = new System.Windows.Forms.PictureBox();
			this.tmrResize = new System.Windows.Forms.Timer(this.components);
			this.tmrScroll = new System.Windows.Forms.Timer(this.components);
			this.pnlMap.SuspendLayout();
			base.SuspendLayout();
			this.pnlMap.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.pnlMap.Controls.Add(this.pbMap);
			this.pnlMap.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pnlMap.Location = new System.Drawing.Point(0, 0);
			this.pnlMap.Name = "pnlMap";
			this.pnlMap.Size = new System.Drawing.Size(236, 176);
			this.pnlMap.TabIndex = 2;
			this.pbMap.BackColor = System.Drawing.SystemColors.AppWorkspace;
			this.pbMap.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pbMap.Location = new System.Drawing.Point(0, 0);
			this.pbMap.Name = "pbMap";
			this.pbMap.Size = new System.Drawing.Size(232, 172);
			this.pbMap.TabIndex = 0;
			this.pbMap.TabStop = false;
			this.pbMap.Resize += new System.EventHandler(this.pbMap_Resize);
			this.pbMap.Paint += new System.Windows.Forms.PaintEventHandler(this.pbMap_Paint);
			this.pbMap.MouseEnter += new System.EventHandler(this.pbMap_MouseEnter);
			this.pbMap.MouseUp += new System.Windows.Forms.MouseEventHandler(this.pbMap_MouseUp);
			this.pbMap.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pbMap_MouseMove);
			this.pbMap.MouseLeave += new System.EventHandler(this.pbMap_MouseLeave);
			this.pbMap.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pbMap_MouseDown);
			this.tmrResize.Tick += new System.EventHandler(this.tmrResize_Tick);
			this.tmrScroll.Interval = 30;
			this.tmrScroll.Tick += new System.EventHandler(this.tmrScroll_Tick);
			base.Controls.Add(this.pnlMap);
			base.Name = "MapView";
			base.Size = new System.Drawing.Size(236, 176);
			this.pnlMap.ResumeLayout(false);
			base.ResumeLayout(false);
		}

		private void pbMap_Paint(object sender, PaintEventArgs e)
		{
			if (gridmode == MapGridMode.Full)
			{
				using (Pen pen = new Pen(Color.FromArgb(gridopacity, gridcolor), 1f))
				{
					for (int i = 31; i < pbMap.Height; i += 32)
					{
						e.Graphics.DrawLine(pen, 0, i, pbMap.Width, i);
					}
					for (int j = 31; j < pbMap.Width; j += 32)
					{
						e.Graphics.DrawLine(pen, j, 0, j, pbMap.Height);
					}
				}
			}
			else if (gridmode == MapGridMode.Near)
			{
				using (LinearGradientBrush brush = new LinearGradientBrush(new Point(mapMouseLocation.X, mapMouseLocation.Y - 96), new Point(mapMouseLocation.X, mapMouseLocation.Y + 96), Color.Transparent, Color.FromArgb(60, gridcolor)))
				{
					using (Pen pen2 = new Pen(brush, 1f))
					{
						int k = (mapMouseLocation.X >> 5 << 5) - 64;
						for (int num = k + 192; k < num; k += 32)
						{
							e.Graphics.DrawLine(pen2, k, 0, k, pbMap.Height);
						}
					}
				}
			}
			Point point = new Point(origin.X << 5, origin.Y << 5);
			if (drawdateline && point.X > Lightmap.BaseWidth - pbMap.Width)
			{
				using (Pen pen3 = new Pen(Color.DarkViolet, 3f))
				{
					pen3.DashStyle = DashStyle.DashDot;
					e.Graphics.DrawLine(pen3, Lightmap.BaseWidth - point.X - 1, 0, Lightmap.BaseWidth - point.X - 1, pbMap.Height);
				}
			}
			if (mapSelection != Rectangle.Empty)
			{
				Rectangle rect = ConvertToDrawSelection(mapSelection);
				using (Brush brush2 = new SolidBrush(Color.FromArgb(30, Color.OrangeRed)))
				{
					rect.Inflate(-1, -1);
					e.Graphics.FillRectangle(brush2, rect);
					rect.Inflate(1, 1);
				}
				using (Pen pen4 = new Pen(Color.FromArgb(70, Color.White), 6f))
				{
					e.Graphics.DrawRectangle(pen4, rect);
				}
				using (Pen pen5 = new Pen(Color.OrangeRed, 2f))
				{
					e.Graphics.DrawRectangle(pen5, rect);
				}
			}
			if (pbMap.Image != null)
			{
				Region region = new Region(new Rectangle(0, 0, pbMap.Width, pbMap.Height));
				region.Exclude(new Rectangle(0, 0, pbMap.Image.Width, pbMap.Image.Height));
				HatchBrush brush3 = new HatchBrush(HatchStyle.Divot, SystemColors.ControlDark, Color.Transparent);
				e.Graphics.FillRegion(brush3, region);
			}
			if (!mapMouseOver)
			{
				return;
			}
			if (drawtrackers)
			{
				using (Pen pen6 = new Pen(Color.FromArgb(trackeropacity, trackercolor), 1f))
				{
					e.Graphics.DrawLine(pen6, 0, mapMouseLocation.Y, pbMap.Width, mapMouseLocation.Y);
					e.Graphics.DrawLine(pen6, mapMouseLocation.X, 0, mapMouseLocation.X, pbMap.Width);
				}
			}
			Rectangle[] array = new Rectangle[4];
			int num2 = scrollzonesize;
			array[0] = new Rectangle(0, 0, pbMap.Width, num2);
			array[1] = new Rectangle(0, num2, num2, pbMap.Height - num2 * 2);
			array[2] = new Rectangle(0, pbMap.Height - num2, pbMap.Width, num2);
			array[3] = new Rectangle(pbMap.Width - num2, num2, num2, pbMap.Height - num2 * 2);
			using (Brush brush4 = new SolidBrush(Color.FromArgb(scrollzoneopacity, scrollzonecolor)))
			{
				e.Graphics.FillRectangles(brush4, array);
			}
		}

		private Rectangle ConvertToDrawSelection(Rectangle drawSelection)
		{
			drawSelection.Offset(-(origin.X << 5), -(origin.Y << 5));
			if (drawSelection.Width < 0)
			{
				if (mapSelectionCrossedDateLine)
				{
					drawSelection.Width += Lightmap.BaseWidth;
				}
				else
				{
					int width = -drawSelection.Width;
					drawSelection.X = drawSelection.Right;
					drawSelection.Width = width;
				}
			}
			else if (mapSelectionCrossedDateLine)
			{
				int width2 = drawSelection.Width;
				drawSelection.X = source.NormalizeX(drawSelection.Right);
				drawSelection.Width = Lightmap.BaseWidth - width2;
			}
			if (drawSelection.Right > drawSelection.Left && drawSelection.Right < 0)
			{
				drawSelection.X = source.NormalizeX(drawSelection.X);
			}
			if (drawSelection.Height < 0)
			{
				int height = -drawSelection.Height;
				drawSelection.Y = drawSelection.Bottom;
				drawSelection.Height = height;
			}
			return drawSelection;
		}

		private void pbMap_Resize(object sender, EventArgs e)
		{
			tmrResize.Enabled = false;
			Application.DoEvents();
			tmrResize.Enabled = true;
		}

		private void tmrResize_Tick(object sender, EventArgs e)
		{
			tmrResize.Enabled = false;
			DrawMap();
			pbMap.Refresh();
			if (minimap != null)
			{
				minimap.Refresh();
			}
		}

		private void pbMap_MouseEnter(object sender, EventArgs e)
		{
			mapMouseOver = true;
			mapSelecting = false;
			lastMouseMoveTime = DateTime.Now;
			pbMap.Refresh();
		}

		private void pbMap_MouseLeave(object sender, EventArgs e)
		{
			mapMouseOver = false;
			mapSelecting = false;
			tmrScroll.Enabled = false;
			pbMap.Refresh();
		}

		private void pbMap_MouseMove(object sender, MouseEventArgs e)
		{
			if (!mapMouseOver || scrollzonesize <= 0 || DateTime.Now.Subtract(lastMouseMoveTime).TotalMilliseconds < 100.0)
			{
				return;
			}
			lastMouseMoveTime = DateTime.Now;
			mapMouseLocation = new Point(e.X, e.Y);
			int num = 0;
			int num2 = 0;
			if (e.X < scrollzonesize)
			{
				num = -1;
			}
			else if (e.X > pbMap.Width - scrollzonesize)
			{
				num = 1;
			}
			if (e.Y < scrollzonesize)
			{
				num2 = -1;
			}
			else if (e.Y > pbMap.Height - scrollzonesize)
			{
				num2 = 1;
			}
			bool flag = mapSelecting;
			if (num != 0 || num2 != 0)
			{
				mapDelta = new Size(num, num2);
				tmrScroll.Enabled = true;
			}
			else
			{
				tmrScroll.Enabled = false;
				flag = true;
			}
			if (mapSelecting && source != null)
			{
				Point point = source.CoordMap.FitToGrid(new Point(mapMouseLocation.X, mapMouseLocation.Y));
				if (source.NormalizeX(point.X + (mapSelectionCrossedDateLine ? source.Size.Width : 0)) >= mapSelection.X)
				{
					point.X += 32;
				}
				if (point.Y >= mapSelection.Y)
				{
					point.Y += 32;
				}
				point.Offset(origin.X << 5, origin.Y << 5);
				point.X = source.NormalizeX(point.X);
				Rectangle rectangle = Rectangle.FromLTRB(mapSelection.X, mapSelection.Y, point.X, point.Y);
				if (Math.Abs(rectangle.Width - mapSelection.Width) > Lightmap.BaseWidth >> 1)
				{
					mapSelectionCrossedDateLine = !mapSelectionCrossedDateLine;
				}
				if (mapSelection != rectangle)
				{
					mapSelection = rectangle;
					OnSelectionChanged(EventArgs.Empty);
				}
			}
			Point point2 = mapMouseLocation;
			point2.Offset(origin.X << 5, origin.Y << 5);
			if (source != null)
			{
				point2.X = source.NormalizeX(point2.X);
			}
			else
			{
				point2.X = MapSize.Default.BaseNormalizeX(point2.X);
			}
			if (source != null)
			{
				OnTrackerMoved(new TrackerMovedEventArgs(point2, source.CoordMap.FitToGrid(point2)));
			}
			if (flag)
			{
				pbMap.Refresh();
			}
		}

		private void pbMap_MouseDown(object sender, MouseEventArgs e)
		{
			mapDragLocation = new Point(e.X, e.Y);
			mapDragOrigin = origin;
			mapDragStart = DateTime.Now;
			mapSelecting = e.Button == MouseButtons.Left && allowSelection && source != null;
			if (mapSelecting && source != null)
			{
				mapSelectionCrossedDateLine = false;
				Point point = source.CoordMap.FitToGrid(mapDragLocation);
				point.Offset(origin.X << 5, origin.Y << 5);
				point.X = source.NormalizeX(point.X);
				mapSelection = Rectangle.FromLTRB(point.X, point.Y, point.X, point.Y);
				OnSelectionChanged(EventArgs.Empty);
			}
		}

		private void pbMap_MouseUp(object sender, MouseEventArgs e)
		{
			mapSelecting = false;
			int num = (mapDragLocation.X - e.X) / 32;
			int num2 = (mapDragLocation.Y - e.Y) / 32;
			if (DateTime.Now.Subtract(mapDragStart).Milliseconds < 200 && Math.Abs(num) == 0 && Math.Abs(num2) == 0)
			{
				if (e.Button == MouseButtons.Right)
				{
					int blockindex = -1;
					if (source != null)
					{
						Point point = source.CoordMap.ActualToBlocks(new Point(e.X, e.Y));
						blockindex = (origin.X + point.X) % source.SizeBlocks.Width + (origin.Y + point.Y) * source.SizeBlocks.Width;
					}
					OnContextMenu(new ContextMenuEventArgs(e.X, e.Y, blockindex));
				}
			}
			else if (e.Button == MouseButtons.Right)
			{
				MoveMapBlocks(num, num2);
			}
		}

		private void tmrScroll_Tick(object sender, EventArgs e)
		{
			MoveMapBlocks(mapDelta.Width, mapDelta.Height);
		}

		private void minimap_MouseDown(object sender, MouseEventArgs e)
		{
			if (minimap != null)
			{
				float num = (float)source.Size.Width / (float)minimap.Width;
				float num2 = (float)source.Size.Height / (float)minimap.Height;
				MoveMapTo((int)(((float)e.X - (float)pbMap.Width / num / 2f + 3f) * num), (int)(((float)e.Y - (float)pbMap.Height / num2 / 2f) * num2), false);
			}
		}

		private void minimap_Paint(object sender, PaintEventArgs e)
		{
			if (minimap == null || source == null)
			{
				return;
			}
			Point point = new Point(origin.X << 5, origin.Y << 5);
			float num = (float)minimap.Width / (float)source.Size.Width;
			float num2 = (float)minimap.Height / (float)source.Size.Height;
			RectangleF value = new RectangleF((float)point.X * num, (float)point.Y * num2, (float)pbMap.Width * num, (float)pbMap.Height * num2);
			using (new Pen(Color.FromArgb(70, Color.Yellow), 1f))
			{
				using (Brush brush = new SolidBrush(Color.FromArgb(50, Color.Yellow)))
				{
					e.Graphics.FillRectangle(brush, Rectangle.Round(value));
					e.Graphics.DrawRectangle(Pens.Yellow, Rectangle.Round(value));
					if (value.X < 0f)
					{
						value.Offset(minimap.Width, 0f);
						e.Graphics.FillRectangle(brush, Rectangle.Round(value));
						e.Graphics.DrawRectangle(Pens.Yellow, Rectangle.Round(value));
					}
					else if (value.Right > (float)minimap.Width)
					{
						value.Offset(0f - (float)minimap.Width, 0f);
						e.Graphics.FillRectangle(brush, Rectangle.Round(value));
						e.Graphics.DrawRectangle(Pens.Yellow, Rectangle.Round(value));
					}
				}
			}
		}

		public void CenterMapTo(int x, int y)
		{
			x -= pbMap.Width / 2;
			y -= pbMap.Height / 2;
			MoveMapTo(x, y, true);
		}

		public void CenterMapTo(Point location)
		{
			CenterMapTo(location.X, location.Y);
		}

		public void MoveMapTo(int x, int y)
		{
			MoveMapTo(x, y, true);
		}

		public void MoveMapTo(Point location)
		{
			CenterMapTo(location.X, location.Y);
		}

		public void MoveMapBy(int dx, int dy)
		{
			MoveMapBlocks(dx >> 5, dy >> 5, false);
		}

		private void MoveMapBlocks(int dx, int dy)
		{
			MoveMapBlocks(dx, dy, false);
		}

		private void MoveMapBlocks(int dx, int dy, bool forced)
		{
			Application.DoEvents();
			if (source == null)
			{
				return;
			}
			Point point = source.CoordMap.BlocksToActual(new Point(origin.X + dx, origin.Y + dy));
			point.X = source.NormalizeX(point.X);
			if (point.Y < 0)
			{
				point.Y = 0;
			}
			else if (point.Y > source.Size.Height - pbMap.Height)
			{
				point.Y = Lightmap.FitToGrid(source.Size.Height - pbMap.Height);
			}
			point = source.CoordMap.ActualToBlocks(point);
			dx = point.X - origin.X;
			dy = point.Y - origin.Y;
			if (!forced && dx == 0 && dy == 0)
			{
				return;
			}
			int num = Math.Abs(dx);
			int num2 = Math.Abs(dy);
			if (num > 2 || num2 > 2)
			{
				Cursor = Cursors.AppStarting;
			}
			if (pbMap.Image == null || (dx == 0 && dy == 0))
			{
				origin = point;
				DrawMap();
			}
			else if (dx != 0 && dy != 0)
			{
				if (num < pbMap.Image.Width >> 5 && num2 < pbMap.Image.Height >> 5)
				{
					origin.X = point.X;
					DrawMapDX(dx);
					origin.Y = point.Y;
					DrawMapDY(dy);
				}
				else
				{
					origin = point;
					DrawMap();
				}
			}
			else if (dx != 0)
			{
				origin.X = point.X;
				if (num < pbMap.Image.Width >> 5)
				{
					DrawMapDX(dx);
				}
				else
				{
					DrawMap();
				}
			}
			else
			{
				origin.Y = point.Y;
				if (num2 < pbMap.Image.Height >> 5)
				{
					DrawMapDY(dy);
				}
				else
				{
					DrawMap();
				}
			}
			pbMap.Refresh();
			if (minimap != null)
			{
				minimap.Refresh();
			}
			Cursor = Cursors.Default;
		}

		private void MoveMapTo(int x, int y, bool forced)
		{
			Point point = new Point(origin.X << 5, origin.Y << 5);
			MoveMapBlocks(x - point.X >> 5, y - point.Y >> 5, forced);
		}

		private void DrawMap()
		{
			Point location = new Point(origin.X << 5, origin.Y << 5);
			Rectangle rect = new Rectangle(location, pbMap.Size);
			Bitmap image = DrawMapImage(rect);
			if (pbMap.Image != null)
			{
				pbMap.Image.Dispose();
			}
			pbMap.Image = image;
		}

		private void DrawMapDX(int dx)
		{
			if (source == null)
			{
				return;
			}
			dx <<= 5;
			Bitmap image = new Bitmap(pbMap.Image.Width, pbMap.Image.Height);
			using (Graphics graphics = Graphics.FromImage(image))
			{
				if (dx > 0)
				{
					Rectangle destRect = new Rectangle(0, 0, pbMap.Image.Width - dx, pbMap.Image.Height);
					graphics.DrawImage(pbMap.Image, destRect, dx, 0, pbMap.Image.Width - dx, pbMap.Image.Height, GraphicsUnit.Pixel);
				}
				else
				{
					graphics.DrawImageUnscaled(pbMap.Image, -dx, 0, pbMap.Image.Width + dx, pbMap.Image.Height);
				}
			}
			Point location = new Point(origin.X << 5, origin.Y << 5);
			if (dx > 0)
			{
				location.X += pbMap.Image.Width - dx;
			}
			Rectangle rect = new Rectangle(location, new Size(Math.Abs(dx), pbMap.Image.Height));
			Bitmap bitmap = DrawMapImage(rect);
			using (Graphics graphics2 = Graphics.FromImage(image))
			{
				if (dx > 0)
				{
					graphics2.DrawImageUnscaled(bitmap, pbMap.Image.Width - dx, 0, dx, pbMap.Image.Height);
				}
				else
				{
					graphics2.DrawImageUnscaled(bitmap, 0, 0, -dx, pbMap.Image.Height);
				}
			}
			bitmap.Dispose();
			if (pbMap.Image != null)
			{
				pbMap.Image.Dispose();
			}
			pbMap.Image = image;
		}

		private void DrawMapDY(int dy)
		{
			if (source == null)
			{
				return;
			}
			dy <<= 5;
			Bitmap image = new Bitmap(pbMap.Image.Width, pbMap.Image.Height);
			using (Graphics graphics = Graphics.FromImage(image))
			{
				if (dy > 0)
				{
					Rectangle destRect = new Rectangle(0, 0, pbMap.Image.Width, pbMap.Image.Height - dy);
					graphics.DrawImage(pbMap.Image, destRect, 0, dy, pbMap.Image.Width, pbMap.Image.Height - dy, GraphicsUnit.Pixel);
				}
				else
				{
					graphics.DrawImageUnscaled(pbMap.Image, 0, -dy, pbMap.Image.Width, pbMap.Image.Height + dy);
				}
			}
			Point location = new Point(origin.X << 5, origin.Y << 5);
			if (dy > 0)
			{
				location.Y += pbMap.Image.Height - dy;
			}
			Rectangle rect = new Rectangle(location, new Size(pbMap.Image.Width, Math.Abs(dy)));
			Bitmap bitmap = DrawMapImage(rect);
			using (Graphics graphics2 = Graphics.FromImage(image))
			{
				if (dy > 0)
				{
					graphics2.DrawImageUnscaled(bitmap, 0, pbMap.Image.Height - dy, pbMap.Image.Width, dy);
				}
				else
				{
					graphics2.DrawImageUnscaled(bitmap, 0, 0, pbMap.Image.Width, -dy);
				}
			}
			bitmap.Dispose();
			if (pbMap.Image != null)
			{
				pbMap.Image.Dispose();
			}
			pbMap.Image = image;
		}

		private void InitMap()
		{
			mapMouseOver = false;
			mapMouseLocation = new Point(-1, -1);
			mapDelta = new Size(0, 0);
			MoveMapBlocks(0, 0);
		}

		private Bitmap DrawMapImage(Rectangle rect)
		{
			if (source == null)
			{
				return null;
			}
			Bitmap bitmap = null;
			RawImage rawImage = source.DecodeImage(rect);
			bitmap = new Bitmap(rawImage.Bounds.Width, rawImage.Bounds.Height, (bitdepth == ColorDetail.Low) ? PixelFormat.Format16bppRgb555 : PixelFormat.Format32bppRgb);
			BitmapData bitmapData = bitmap.LockBits(new Rectangle(new Point(0, 0), bitmap.Size), ImageLockMode.WriteOnly, bitmap.PixelFormat);
			if (bitdepth == ColorDetail.Low)
			{
				short[] array = shaderproxy.Shade16(rawImage);
				Marshal.Copy(array, 0, bitmapData.Scan0, array.Length);
			}
			else
			{
				int[] array2 = shaderproxy.Shade32(rawImage);
				Marshal.Copy(array2, 0, bitmapData.Scan0, array2.Length);
			}
			bitmap.UnlockBits(bitmapData);
			return bitmap;
		}

		public Image GetImage()
		{
			return pbMap.Image;
		}

		public new void Refresh()
		{
			DrawMap();
			base.Refresh();
		}

		protected override void OnNotifyMessage(Message m)
		{
			base.OnNotifyMessage(m);
			if (m.Msg == 256)
			{
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			switch (e.KeyCode)
			{
			case Keys.Left:
				MoveMapBlocks(-1, 0);
				break;
			case Keys.Right:
				MoveMapBlocks(1, 0);
				break;
			case Keys.Up:
				MoveMapBlocks(0, -1);
				break;
			case Keys.Down:
				MoveMapBlocks(0, 1);
				break;
			}
		}

		protected virtual void OnContextMenu(ContextMenuEventArgs e)
		{
			ContextMenuEventHandler contextMenu = ContextMenu;
			if (contextMenu != null)
			{
				contextMenu(this, e);
			}
		}

		protected virtual void OnTrackerMoved(TrackerMovedEventArgs e)
		{
			TrackerMovedEventHandler trackerMoved = TrackerMoved;
			if (trackerMoved != null)
			{
				trackerMoved(this, e);
			}
		}

		protected virtual void OnSelectionChanged(EventArgs e)
		{
			SelectionChangedHandler selectionChanged = SelectionChanged;
			if (selectionChanged != null)
			{
				selectionChanged(this, e);
			}
		}
	}
}
