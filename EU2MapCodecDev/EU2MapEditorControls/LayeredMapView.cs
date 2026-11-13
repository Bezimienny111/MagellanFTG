using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;
using EU2.Map;
using LayerPainter;

namespace EU2MapEditorControls
{
	public class LayeredMapView : UserControl
	{
		public delegate void BorderStyleChangedEventHandler(object sender, EventArgs e);

		public delegate void ContextMenuEventHandler(object sender, ContextMenuEventArgs e);

		public delegate void TrackerMovedEventHandler(object sender, TrackerMovedEventArgs e);

		public delegate void SelectionChangedHandler(object sender, EventArgs e);

		private class MapDimensions : ILightmapDimensions
		{
			private int zoom;

			private MapSize mapSz;

			public CoordinateMapper CoordMap
			{
				get
				{
					return new CoordinateMapper(zoom);
				}
			}

			public Size Size
			{
				get
				{
					return new Size(mapSz.BaseWidth >> zoom, mapSz.BaseHeight >> zoom);
				}
			}

			public Size SizeBlocks
			{
				get
				{
					return new Size(mapSz.BaseWidth >> 5 + zoom, mapSz.BaseHeight >> 5 + zoom);
				}
			}

			public MapSize MapSize
			{
				get
				{
					return mapSz;
				}
			}

			public int Zoom
			{
				get
				{
					return zoom;
				}
				set
				{
					zoom = value;
				}
			}

			public MapDimensions(MapSize mapSz)
				: this(mapSz, 0)
			{
			}

			public MapDimensions(MapSize mapSize, int zoom)
			{
				mapSz = mapSize;
				this.zoom = zoom;
			}

			public int NormalizeX(int x)
			{
				x %= Size.Width;
				if (x < 0)
				{
					x += Size.Width;
				}
				return x;
			}

			public int NormalizeBlockX(int x)
			{
				x %= SizeBlocks.Width;
				if (x < 0)
				{
					x += SizeBlocks.Width;
				}
				return x;
			}
		}

		private class TimerHandler : IDisposable
		{
			public delegate void MapScrollingEventHandler(object sender, MapScrollingEventArgs e);

			public class MapScrollingEventArgs
			{
				private Size delta;

				private Size totalDelta;

				public int DX
				{
					get
					{
						return delta.Width;
					}
				}

				public int DY
				{
					get
					{
						return delta.Height;
					}
				}

				public int TotalDX
				{
					get
					{
						return totalDelta.Width;
					}
				}

				public int TotalDY
				{
					get
					{
						return totalDelta.Height;
					}
				}

				public MapScrollingEventArgs(Size delta, Size totalDelta)
				{
					this.delta = delta;
					this.totalDelta = totalDelta;
				}
			}

			public delegate void MapScrollEndEventHandler(object sender, MapScrollingEventArgs e);

			private LayeredMapView parent;

			private Thread scrollthread;

			private Size delta = Size.Empty;

			private Size totaldelta = Size.Empty;

			private Size prevDelta = Size.Empty;

			private bool allow_run;

			public bool IsScrolling
			{
				get
				{
					return !delta.IsEmpty;
				}
			}

			public event MapScrollingEventHandler MapScrolling;

			public event MapScrollEndEventHandler MapScrollEnd;

			public TimerHandler(LayeredMapView parent)
			{
				this.parent = parent;
				scrollthread = new Thread(ThreadStart);
				scrollthread.Start();
				allow_run = true;
			}

			public void StartScrolling(int dx, int dy)
			{
				if (dx != delta.Width || dy != delta.Height)
				{
					totaldelta = Size.Empty;
					delta = new Size(dx, dy);
				}
			}

			public Size StopScrolling()
			{
				delta = Size.Empty;
				return totaldelta;
			}

			protected virtual void OnMapScrolling(MapScrollingEventArgs e)
			{
				MapScrollingEventHandler mapScrolling = MapScrolling;
				if (mapScrolling != null)
				{
					mapScrolling(this, e);
				}
			}

			protected virtual void OnMapScrollEnd(MapScrollingEventArgs e)
			{
				MapScrollEndEventHandler mapScrollEnd = MapScrollEnd;
				if (mapScrollEnd != null)
				{
					mapScrollEnd(this, e);
				}
			}

			public void ThreadStart()
			{
				while (allow_run)
				{
					if (!delta.IsEmpty)
					{
						totaldelta.Width += delta.Width;
						totaldelta.Height += delta.Height;
						OnMapScrolling(new MapScrollingEventArgs(delta, totaldelta));
						prevDelta = delta;
					}
					else if (!prevDelta.IsEmpty)
					{
						OnMapScrollEnd(new MapScrollingEventArgs(Size.Empty, totaldelta));
						prevDelta = Size.Empty;
					}
					Thread.Sleep(50);
				}
			}

			public void Dispose()
			{
				if (scrollthread != null)
				{
					allow_run = false;
					if (scrollthread.ThreadState != ThreadState.Unstarted)
					{
						scrollthread.Join();
					}
					scrollthread = null;
				}
			}
		}

		internal enum PaintRequestType
		{
			Move,
			Paint
		}

		internal struct PaintRequest
		{
			private PaintRequestType type;

			private Rectangle area;

			private int dx;

			private int dy;

			public PaintRequestType Type
			{
				get
				{
					return type;
				}
			}

			public Rectangle Area
			{
				get
				{
					return area;
				}
			}

			public int DX
			{
				get
				{
					return dx;
				}
			}

			public int DY
			{
				get
				{
					return dy;
				}
			}

			public PaintRequest(PaintRequestType type, int x, int y, int width, int height, int dx, int dy)
			{
				this.type = type;
				area = new Rectangle(x, y, width, height);
				this.dx = dx;
				this.dy = dy;
			}

			public PaintRequest(PaintRequestType type, int x, int y, int width, int height)
			{
				this.type = type;
				area = new Rectangle(x, y, width, height);
				dx = 0;
				dy = 0;
			}

			public PaintRequest(PaintRequestType type, Rectangle area)
			{
				this.type = type;
				this.area = area;
				dx = 0;
				dy = 0;
			}

			public PaintRequest(PaintRequestType type, Rectangle area, int dx, int dy)
			{
				this.type = type;
				this.area = area;
				this.dx = dx;
				this.dy = dy;
			}

			public PaintRequest(PaintRequestType type, Point location, Size size)
			{
				this.type = type;
				area = new Rectangle(location, size);
				dx = 0;
				dy = 0;
			}

			public PaintRequest(PaintRequestType type, Point location, Size size, int dx, int dy)
			{
				this.type = type;
				area = new Rectangle(location, size);
				this.dx = dx;
				this.dy = dy;
			}

			public override string ToString()
			{
				return string.Format("{{{0}:{1},{2},{3},{4}}}", type, area.X, area.Y, area.Width, area.Height);
			}
		}

		private const int LOADINGCNT = 13;

		private IContainer components;

		private BorderStyle border;

		private MapGridMode gridmode;

		private Color gridcolor;

		private double gridopacity;

		private Point trackerlocation;

		private bool drawtrackers;

		private RegionDrawMode drawRegionBorders;

		private Color trackercolor;

		private double trackeropacity;

		private int scrollzonesize;

		private Color scrollzonecolor;

		private double scrollzoneopacity;

		private bool drawdateline;

		private Color datelinecolor;

		private double datelineopacity;

		private Color regionbordercolor;

		private double regionborderopacity;

		private Color regionoverlapcolor;

		private double regionoverlapopacity;

		private bool allow_selection;

		private bool drawcoordinates;

		private Color coordinatescolor;

		private double coordinatesopacity;

		private Font coordinatesfont;

		private PictureBox minimap;

		private Point origin;

		private bool is_mouse_over;

		private Point drag_location;

		private Point drag_origin;

		private DateTime drag_starttime;

		private bool is_selecting;

		private Rectangle selected_area;

		private bool selection_crossed_dateline;

		private DateTime lastmousemove_time;

		private TimerHandler timer;

		private bool isloading;

		private int loadingindex;

		private bool loadingflag;

		private System.Windows.Forms.Timer loadingtimer;

		private ILightmapDimensions map_dimensions;

		private System.Windows.Forms.Timer tmrTotalDelta;

		private LayerPainterCollection layers;

		private ArrayList surfaces;

		[Category("Appearance")]
		[DefaultValue(null)]
		[Description("Picturebox representing the minimap.")]
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

		[Description("Indicates whether or not the mapview should have a border.")]
		[NotifyParentProperty(true)]
		[DefaultValue(BorderStyle.Fixed3D)]
		[Category("Appearance")]
		public new BorderStyle BorderStyle
		{
			get
			{
				return border;
			}
			set
			{
				if (value != border)
				{
					border = value;
					AdjustLayers();
					UpdateStyles();
					OnBorderStyleChanged(EventArgs.Empty);
				}
			}
		}

		[Category("Appearance")]
		[Description("Defines the layerpainters for the view.")]
		public LayerPainterCollection Layers
		{
			get
			{
				return layers;
			}
		}

		[NotifyParentProperty(true)]
		[DefaultValue(MapGridMode.Full)]
		[Description("How to draw the map grid.")]
		[Category("Appearance")]
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
					Invalidate();
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
					Invalidate();
				}
			}
		}

		[TypeConverter(typeof(OpacityConverter))]
		[DefaultValue(0.6)]
		[Description("Specifies the opacity of the grid.")]
		[Category("Appearance")]
		public double GridOpacity
		{
			get
			{
				return gridopacity;
			}
			set
			{
				if (value < 0.0 || value > 1.0)
				{
					throw new ArgumentOutOfRangeException();
				}
				if (value != gridopacity)
				{
					gridopacity = value;
					Invalidate();
				}
			}
		}

		[Description("Indicates wheter to draw a 'loading' screen or the map.")]
		[Category("Appearance")]
		[DefaultValue(true)]
		public bool ShowLoading
		{
			get
			{
				return isloading;
			}
			set
			{
				if (value == isloading)
				{
					return;
				}
				isloading = value;
				if (isloading)
				{
					if (loadingtimer == null)
					{
						loadingindex = 0;
						loadingflag = false;
						loadingtimer = new System.Windows.Forms.Timer();
						loadingtimer.Tick += loadingtimer_Tick;
						loadingtimer.Interval = 100;
						loadingtimer.Enabled = true;
					}
				}
				else if (loadingtimer != null)
				{
					loadingtimer.Enabled = false;
					loadingtimer.Dispose();
					loadingtimer = null;
				}
				Invalidate();
			}
		}

		[Description("Indicates whether to draw mouse trackers or not.")]
		[DefaultValue(true)]
		[Category("Appearance")]
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
					Invalidate();
				}
			}
		}

		[Category("Appearance")]
		[Description("Specifies which color the mouse trackers have.")]
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
					Invalidate();
				}
			}
		}

		[Description("Specifies the opacity of the mouse trackers.")]
		[TypeConverter(typeof(OpacityConverter))]
		[Category("Appearance")]
		[DefaultValue(0.7)]
		public double MouseTrackerOpacity
		{
			get
			{
				return trackeropacity;
			}
			set
			{
				if (value < 0.0 || value > 1.0)
				{
					throw new ArgumentOutOfRangeException();
				}
				if (value != trackeropacity)
				{
					trackeropacity = value;
					Invalidate();
				}
			}
		}

		[DefaultValue(true)]
		[Description("Indicates whether to draw the date line or not.")]
		[Category("Appearance")]
		public bool DrawDateline
		{
			get
			{
				return drawdateline;
			}
			set
			{
				if (value != drawdateline)
				{
					drawdateline = value;
					Invalidate();
				}
			}
		}

		[Category("Appearance")]
		[Description("Specifies which color the dateline is drawn.")]
		public Color DatelineColor
		{
			get
			{
				return datelinecolor;
			}
			set
			{
				if (!(value == datelinecolor))
				{
					datelinecolor = value;
					Invalidate();
				}
			}
		}

		[Description("Specifies the opacity of the dateline.")]
		[TypeConverter(typeof(OpacityConverter))]
		[DefaultValue(1)]
		[Category("Appearance")]
		public double DatelineOpacity
		{
			get
			{
				return datelineopacity;
			}
			set
			{
				if (value < 0.0 || value > 1.0)
				{
					throw new ArgumentOutOfRangeException();
				}
				if (value != datelineopacity)
				{
					datelineopacity = value;
					Invalidate();
				}
			}
		}

		[DefaultValue(true)]
		[Category("Appearance")]
		[Description("Indicates whether to regions borders or not.")]
		public RegionDrawMode DrawRegionBorders
		{
			get
			{
				return drawRegionBorders;
			}
			set
			{
				if (value != drawRegionBorders)
				{
					drawRegionBorders = value;
					Invalidate();
				}
			}
		}

		[Description("Specifies which color the region borders is are drawn.")]
		[Category("Appearance")]
		public Color RegionBorderColor
		{
			get
			{
				return regionbordercolor;
			}
			set
			{
				if (!(value == regionbordercolor))
				{
					regionbordercolor = value;
					Invalidate();
				}
			}
		}

		[TypeConverter(typeof(OpacityConverter))]
		[Description("Specifies the opacity of the region borders.")]
		[DefaultValue(1)]
		[Category("Appearance")]
		public double RegionBorderOpacity
		{
			get
			{
				return regionborderopacity;
			}
			set
			{
				if (value < 0.0 || value > 1.0)
				{
					throw new ArgumentOutOfRangeException();
				}
				if (value != regionborderopacity)
				{
					regionborderopacity = value;
					Invalidate();
				}
			}
		}

		[Category("Appearance")]
		[Description("Specifies which color the region overlaps is are drawn.")]
		public Color RegionOverlapColor
		{
			get
			{
				return regionoverlapcolor;
			}
			set
			{
				if (!(value == regionoverlapcolor))
				{
					regionoverlapcolor = value;
					Invalidate();
				}
			}
		}

		[Category("Appearance")]
		[Description("Specifies the opacity of the region overlaps.")]
		[TypeConverter(typeof(OpacityConverter))]
		[DefaultValue(0.25)]
		public double RegionOverlapOpacity
		{
			get
			{
				return regionoverlapopacity;
			}
			set
			{
				if (value < 0.0 || value > 1.0)
				{
					throw new ArgumentOutOfRangeException();
				}
				if (value != regionoverlapopacity)
				{
					regionoverlapopacity = value;
					Invalidate();
				}
			}
		}

		[DefaultValue(32)]
		[Description("Size of the scroll area. Set this value to 0 to display the scroll area functionality.")]
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
					Invalidate();
				}
			}
		}

		[Category("Appearance")]
		[Description("Specifies the color of the scrollzone.")]
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
					Invalidate();
				}
			}
		}

		[Category("Appearance")]
		[DefaultValue(0.7)]
		[Description("Specifies the opacity of the scrollzone.")]
		[TypeConverter(typeof(OpacityConverter))]
		public double ScrollZoneOpacity
		{
			get
			{
				return scrollzoneopacity;
			}
			set
			{
				if (value < 0.0 || value > 1.0)
				{
					throw new ArgumentOutOfRangeException();
				}
				if (value != scrollzoneopacity)
				{
					scrollzoneopacity = value;
					Invalidate();
				}
			}
		}

		[Description("Indicates of the map allows selecting an area.")]
		[DefaultValue(true)]
		[Category("Behavior")]
		public bool AllowSelection
		{
			get
			{
				return allow_selection;
			}
			set
			{
				allow_selection = value;
				if (is_selecting && !allow_selection)
				{
					is_selecting = false;
					Invalidate();
				}
			}
		}

		[Category("Behavior")]
		[Description("The currently selected area.")]
		public Rectangle Selection
		{
			get
			{
				Rectangle result = selected_area;
				if (selection_crossed_dateline)
				{
					if (result.Left < result.Right)
					{
						result.X = result.Right;
						result.Width = -result.Width;
					}
					result.Width = map_dimensions.NormalizeX(result.Width);
				}
				else if (result.Width < 0)
				{
					result.X += result.Width;
					result.Width = -result.Width;
				}
				if (result.Height < 0)
				{
					result.Y += result.Height;
					result.Height = -result.Height;
				}
				return result;
			}
			set
			{
				if (value != selected_area)
				{
					selected_area = value;
					Invalidate();
					OnSelectionChanged(EventArgs.Empty);
				}
			}
		}

		[Category("Appearance")]
		[Description("Indicates whether to draw coordinates in each block or not.")]
		[DefaultValue(true)]
		public bool DrawCoordinates
		{
			get
			{
				return drawcoordinates;
			}
			set
			{
				if (value != drawcoordinates)
				{
					drawcoordinates = value;
					Invalidate();
				}
			}
		}

		[Category("Appearance")]
		[Description("Specifies which color the coordinates have.")]
		public Color CoordinatesColor
		{
			get
			{
				return coordinatescolor;
			}
			set
			{
				if (!(value == coordinatescolor))
				{
					coordinatescolor = value;
					Invalidate();
				}
			}
		}

		[TypeConverter(typeof(OpacityConverter))]
		[DefaultValue(0.7)]
		[Description("Specifies the opacity of the coordinates.")]
		[Category("Appearance")]
		public double CoordinatesOpacity
		{
			get
			{
				return coordinatesopacity;
			}
			set
			{
				if (value < 0.0 || value > 1.0)
				{
					throw new ArgumentOutOfRangeException();
				}
				if (value != coordinatesopacity)
				{
					coordinatesopacity = value;
					Invalidate();
				}
			}
		}

		[Category("Appearance")]
		[Description("Specifies what font the coordinates have.")]
		public Font CoordinatesFont
		{
			get
			{
				return coordinatesfont;
			}
			set
			{
				if (value != coordinatesfont)
				{
					coordinatesfont.Dispose();
					coordinatesfont = value;
					Invalidate();
				}
			}
		}

		[Browsable(false)]
		public ILightmapDimensions LightmapDimensions
		{
			get
			{
				return map_dimensions;
			}
			set
			{
				map_dimensions = value;
			}
		}

		public event BorderStyleChangedEventHandler BorderStyleChanged;

		public new event ContextMenuEventHandler ContextMenu;

		public event TrackerMovedEventHandler TrackerMoved;

		public event SelectionChangedHandler SelectionChanged;

		public LayeredMapView()
		{
			InitializeComponent();
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer | ControlStyles.Selectable | ControlStyles.UserPaint, true);
			UpdateStyles();
			border = BorderStyle.Fixed3D;
			gridmode = MapGridMode.Full;
			gridcolor = Color.LightGray;
			gridopacity = 0.6;
			trackerlocation = new Point(0);
			drawtrackers = true;
			trackercolor = Color.Yellow;
			trackeropacity = 0.7;
			scrollzonesize = 32;
			scrollzonecolor = SystemColors.ActiveCaption;
			scrollzoneopacity = 0.7;
			allow_selection = true;
			drawdateline = true;
			datelinecolor = Color.DarkViolet;
			datelineopacity = 1.0;
			drawRegionBorders = RegionDrawMode.None;
			regionbordercolor = Color.Orange;
			regionborderopacity = 1.0;
			regionoverlapcolor = Color.Orange;
			regionoverlapopacity = 0.25;
			drawcoordinates = true;
			coordinatescolor = Color.LightGray;
			coordinatesopacity = 0.7;
			coordinatesfont = new Font(Font, FontStyle.Regular);
			minimap = null;
			origin = new Point(0, 0);
			map_dimensions = new MapDimensions(MapSize.Default);
			is_mouse_over = false;
			is_selecting = false;
			selected_area = Rectangle.Empty;
			drag_location = Point.Empty;
			drag_origin = Point.Empty;
			drag_starttime = DateTime.MinValue;
			layers = new LayerPainterCollection();
			layers.LayerAdded += layer_added;
			layers.LayerRemoved += layer_removed;
			layers.LayerPropertyChanged += layer_propertychanged;
			surfaces = new ArrayList();
			timer = new TimerHandler(this);
			timer.MapScrolling += timer_MapScrolling;
			timer.MapScrollEnd += timer_MapScrollEnd;
			isloading = DesignMode;
			loadingtimer = null;
			loadingindex = 0;
			loadingflag = false;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				timer.Dispose();
				coordinatesfont.Dispose();
				if (components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.tmrTotalDelta = new System.Windows.Forms.Timer(this.components);
			this.tmrTotalDelta.Interval = 200;
			base.Name = "LayeredMapView";
		}

		public void CenterMapTo(int x, int y)
		{
			x -= Width / 2;
			y -= Height / 2;
			MoveMapTo(x, y);
		}

		public void CenterMapTo(Point location)
		{
			CenterMapTo(location.X, location.Y);
		}

		public void MoveMapTo(int x, int y)
		{
			Point point = new Point(origin.X << 5, origin.Y << 5);
			MoveMapBlocks(x - point.X >> 5, y - point.Y >> 5, true);
		}

		public void MoveMapTo(Point location)
		{
			CenterMapTo(location.X, location.Y);
		}

		public void MoveMapBy(int dx, int dy)
		{
			MoveMapBlocks(dx >> 5, dy >> 5, true);
		}

		public Image GetImage()
		{
			return new Bitmap(Width, Height);
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			AdjustLayers();
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			AdjustLayers();
		}

		private void layer_added(object sender, LayerActionEventArgs e)
		{
			surfaces.Insert(e.Index, null);
			AdjustLayers();
		}

		private void layer_removed(object sender, LayerActionEventArgs e)
		{
			surfaces.RemoveAt(e.Index);
			AdjustLayers();
		}

		private void layer_propertychanged(object sender, LayerActionEventArgs e)
		{
			AdjustLayers();
		}

		private void minimap_MouseDown(object sender, MouseEventArgs e)
		{
			if (minimap != null)
			{
				float num = (float)map_dimensions.Size.Width / (float)minimap.Width;
				float num2 = (float)map_dimensions.Size.Height / (float)minimap.Height;
				Size size = CalcCanvasSize();
				MoveMapTo((int)(((float)e.X - (float)size.Width / num / 2f + 3f) * num), (int)(((float)e.Y - (float)size.Height / num2 / 2f) * num2));
			}
		}

		private void minimap_Paint(object sender, PaintEventArgs e)
		{
			if (minimap == null)
			{
				return;
			}
			Point point = map_dimensions.CoordMap.BlocksToActual(new Point(origin.X, origin.Y));
			Size size = CalcCanvasSize();
			float num = (float)minimap.Width / (float)map_dimensions.Size.Width;
			float num2 = (float)minimap.Height / (float)map_dimensions.Size.Height;
			RectangleF value = new RectangleF((float)point.X * num, (float)point.Y * num2, (float)size.Width * num, (float)size.Height * num2);
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

		private void ClearSurfaces()
		{
			for (int i = 0; i < surfaces.Count; i++)
			{
				if (surfaces[i] != null)
				{
					((Bitmap)surfaces[i]).Dispose();
					surfaces[i] = null;
				}
			}
		}

		private void AdjustLayers()
		{
			ClearSurfaces();
			UpdateLayerSurfaces(origin, new PaintRequest(PaintRequestType.Paint, new Rectangle(origin, CalcCanvasSizeBlocks())), false);
			Invalidate();
		}

		private Size CalcCanvasSize()
		{
			return CalcCanvas().Size;
		}

		private Size CalcCanvasSizeBlocks()
		{
			return map_dimensions.CoordMap.ActualToBlocks(map_dimensions.CoordMap.FitToGrid(CalcCanvas().Size));
		}

		private Rectangle CalcCanvas()
		{
			Rectangle result = new Rectangle(0, 0, Width, Height);
			Size size = Size.Empty;
			if (border == BorderStyle.FixedSingle)
			{
				size = new Size(-1, -1);
			}
			else if (border == BorderStyle.Fixed3D)
			{
				size = new Size(-SystemInformation.Border3DSize.Width, -SystemInformation.Border3DSize.Height);
			}
			result.Width -= size.Width;
			result.Height -= size.Height;
			return result;
		}

		public void RefreshMap()
		{
			if (!CalcCanvas().IsEmpty)
			{
				ClearSurfaces();
				UpdateLayerSurfaces(origin, new PaintRequest(PaintRequestType.Paint, origin, CalcCanvasSizeBlocks()), false);
			}
		}

		private void MoveMapBlocks(int dx, int dy, bool permanent)
		{
			Size size = CalcCanvasSize();
			Point point = map_dimensions.CoordMap.BlocksToActual(new Point(origin.X + dx, origin.Y + dy));
			point.X = map_dimensions.NormalizeX(point.X);
			if (point.Y < 0)
			{
				point.Y = 0;
			}
			else if (point.Y > map_dimensions.Size.Height - size.Height)
			{
				point.Y = Lightmap.FitToGrid(map_dimensions.Size.Height - size.Height);
			}
			point = map_dimensions.CoordMap.ActualToBlocks(point);
			dx = point.X - origin.X;
			dy = point.Y - origin.Y;
			if (dx != 0 || dy != 0)
			{
				Size size2 = CalcCanvasSizeBlocks();
				PaintRequest[] array = null;
				if (Math.Abs(dx) >= size2.Width || Math.Abs(dy) >= size2.Height)
				{
					array = new PaintRequest[1]
					{
						new PaintRequest(PaintRequestType.Paint, point, size2)
					};
				}
				else if (dx != 0)
				{
					if (dy != 0)
					{
						if (dx > 0 && dy > 0)
						{
							array = new PaintRequest[3]
							{
								new PaintRequest(PaintRequestType.Move, point.X, point.Y, size2.Width - dx, size2.Height - dy, -dx, -dy),
								new PaintRequest(PaintRequestType.Paint, point.X + size2.Width - dx, point.Y, dx, size2.Height),
								new PaintRequest(PaintRequestType.Paint, point.X, point.Y + size2.Height - dy, size2.Width - dx, dy)
							};
						}
						else if (dx < 0 && dy > 0)
						{
							array = new PaintRequest[3]
							{
								new PaintRequest(PaintRequestType.Move, point.X - dx, point.Y, size2.Width + dx, size2.Height - dy, -dx, -dy),
								new PaintRequest(PaintRequestType.Paint, point.X, point.Y + size2.Height - dy, size2.Width, dy),
								new PaintRequest(PaintRequestType.Paint, point.X, point.Y, -dx, size2.Height - dy)
							};
						}
						else
						{
							array = ((dx <= 0 || dy >= 0) ? new PaintRequest[3]
							{
								new PaintRequest(PaintRequestType.Move, point.X - dx, point.Y - dy, size2.Width + dx, size2.Height + dy, -dx, -dy),
								new PaintRequest(PaintRequestType.Paint, point.X, point.Y, -dx, size2.Height),
								new PaintRequest(PaintRequestType.Paint, point.X - dx, point.Y, size2.Width + dx, -dy)
							} : new PaintRequest[3]
							{
								new PaintRequest(PaintRequestType.Move, point.X, point.Y - dy, size2.Width - dx, size2.Height + dy, -dx, -dy),
								new PaintRequest(PaintRequestType.Paint, point.X + size2.Width - dx, point.Y, dx, size2.Height),
								new PaintRequest(PaintRequestType.Paint, point.X, point.Y, size2.Width - dx, -dy)
							});
						}
					}
					else
					{
						array = ((dx <= 0) ? new PaintRequest[2]
						{
							new PaintRequest(PaintRequestType.Move, point.X - dx, point.Y, size2.Width + dx, size2.Height, -dx, -dy),
							new PaintRequest(PaintRequestType.Paint, point.X, point.Y, -dx, size2.Height)
						} : new PaintRequest[2]
						{
							new PaintRequest(PaintRequestType.Move, point.X, point.Y, size2.Width - dx, size2.Height, -dx, -dy),
							new PaintRequest(PaintRequestType.Paint, point.X + size2.Width - dx, point.Y, dx, size2.Height)
						});
					}
				}
				else
				{
					array = ((dy <= 0) ? new PaintRequest[2]
					{
						new PaintRequest(PaintRequestType.Move, point.X, point.Y - dy, size2.Width, size2.Height + dy, -dx, -dy),
						new PaintRequest(PaintRequestType.Paint, point.X, point.Y, size2.Width, -dy)
					} : new PaintRequest[2]
					{
						new PaintRequest(PaintRequestType.Move, point.X, point.Y, size2.Width, size2.Height - dy, -dx, -dy),
						new PaintRequest(PaintRequestType.Paint, point.X, point.Y + size2.Height - dy, size2.Width, dy)
					});
				}
				Point referenceOrigin = origin;
				origin = point;
				if (minimap != null)
				{
					minimap.Invalidate();
				}
				UpdateLayerSurfaces(referenceOrigin, array, !permanent);
			}
		}

		private void UpdateLayerSurfaces(Point referenceOrigin, PaintRequest request, bool quick)
		{
			UpdateLayerSurfaces(referenceOrigin, new PaintRequest[1] { request }, quick);
		}

		private void UpdateLayerSurfaces(Point referenceOrigin, PaintRequest[] requests, bool quick)
		{
			if (requests == null || requests.Length == 0)
			{
				return;
			}
			Size size = map_dimensions.CoordMap.FitToGrid(CalcCanvasSize());
			if (size.Width <= 0 || size.Height <= 0)
			{
				return;
			}
			lock (surfaces)
			{
				for (int i = 0; i < requests.Length; i++)
				{
					PaintRequest paintRequest = requests[i];
					if (paintRequest.Area.IsEmpty)
					{
						continue;
					}
					Bitmap bitmap = null;
					Size size2 = map_dimensions.CoordMap.BlocksToActual(paintRequest.Area.Size);
					for (int j = 0; j < layers.Count; j++)
					{
						ILayerPainter layerPainter = layers[j];
						if (surfaces[j] == null)
						{
							bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
							using (Graphics graphics = Graphics.FromImage(bitmap))
							{
								graphics.Clear(Color.Transparent);
								if (layerPainter.Enabled)
								{
									if (quick)
									{
										layerPainter.QuickPaint(graphics, map_dimensions, new Rectangle(origin, map_dimensions.CoordMap.ActualToBlocks(size)));
									}
									else
									{
										layerPainter.Paint(graphics, map_dimensions, new Rectangle(origin, map_dimensions.CoordMap.ActualToBlocks(size)));
									}
								}
							}
							surfaces[j] = bitmap;
							continue;
						}
						bitmap = (Bitmap)surfaces[j];
						if (paintRequest.Type == PaintRequestType.Move)
						{
							Rectangle area = paintRequest.Area;
							area.Offset(-referenceOrigin.X, -referenceOrigin.Y);
							Rectangle rect = area;
							rect.Offset(paintRequest.DX, paintRequest.DY);
							area = map_dimensions.CoordMap.BlocksToActual(area);
							rect = map_dimensions.CoordMap.BlocksToActual(rect);
							using (Bitmap bitmap2 = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
							{
								using (Graphics graphics2 = Graphics.FromImage(bitmap2))
								{
									graphics2.DrawImage(bitmap, new Rectangle(Point.Empty, rect.Size), area, GraphicsUnit.Pixel);
								}
								using (Graphics graphics3 = Graphics.FromImage(bitmap))
								{
									graphics3.SetClip(new Rectangle(rect.Location, bitmap2.Size));
									graphics3.Clear(Color.Transparent);
									graphics3.DrawImageUnscaled(bitmap2, rect.X, rect.Y);
									graphics3.ResetClip();
								}
							}
						}
						else
						{
							if (paintRequest.Type != PaintRequestType.Paint)
							{
								continue;
							}
							using (Bitmap bitmap3 = new Bitmap(size2.Width, size2.Height, PixelFormat.Format32bppArgb))
							{
								using (Graphics g = Graphics.FromImage(bitmap3))
								{
									if (layerPainter.Enabled)
									{
										if (quick)
										{
											layerPainter.QuickPaint(g, map_dimensions, paintRequest.Area);
										}
										else
										{
											layerPainter.Paint(g, map_dimensions, paintRequest.Area);
										}
									}
								}
								using (Graphics graphics4 = Graphics.FromImage(bitmap))
								{
									Point point = new Point(paintRequest.Area.X - origin.X, paintRequest.Area.Y - origin.Y);
									point = map_dimensions.CoordMap.BlocksToActual(point);
									graphics4.SetClip(new Rectangle(point, bitmap3.Size));
									graphics4.Clear(Color.Transparent);
									graphics4.DrawImageUnscaled(bitmap3, point);
									graphics4.ResetClip();
								}
							}
						}
					}
				}
			}
			Invalidate();
		}

		private Rectangle ConvertToDrawSelection(Rectangle drawSelection)
		{
			drawSelection.Offset(-(origin.X << 5), -(origin.Y << 5));
			if (drawSelection.Width < 0)
			{
				if (selection_crossed_dateline)
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
			else if (selection_crossed_dateline)
			{
				int width2 = drawSelection.Width;
				drawSelection.X = map_dimensions.NormalizeX(drawSelection.Right);
				drawSelection.Width = Lightmap.BaseWidth - width2;
			}
			if (drawSelection.Right > drawSelection.Left && drawSelection.Right < 0)
			{
				drawSelection.X = map_dimensions.NormalizeX(drawSelection.X);
			}
			if (drawSelection.Height < 0)
			{
				int height = -drawSelection.Height;
				drawSelection.Y = drawSelection.Bottom;
				drawSelection.Height = height;
			}
			return drawSelection;
		}

		private void timer_MapScrollStart(object sender, TimerHandler.MapScrollingEventArgs e)
		{
		}

		private void timer_MapScrolling(object sender, TimerHandler.MapScrollingEventArgs e)
		{
			MoveMapBlocks(e.DX, e.DY, false);
		}

		private void timer_MapScrollEnd(object sender, TimerHandler.MapScrollingEventArgs e)
		{
		}

		private void loadingtimer_Tick(object sender, EventArgs e)
		{
			if (isloading)
			{
				loadingindex = (loadingindex + 1) % 13;
				if (loadingindex == 0)
				{
					loadingflag = !loadingflag;
				}
				Invalidate();
			}
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			is_mouse_over = true;
			is_selecting = false;
			if (!isloading)
			{
				Invalidate();
			}
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			if (timer.IsScrolling)
			{
				timer.StopScrolling();
			}
			is_mouse_over = false;
			is_selecting = false;
			if (!isloading)
			{
				Invalidate();
			}
			base.OnMouseLeave(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			drag_location = new Point(e.X, e.Y);
			drag_origin = origin;
			drag_starttime = DateTime.Now;
			is_selecting = e.Button == MouseButtons.Left && allow_selection;
			if (is_selecting)
			{
				selection_crossed_dateline = false;
				Point point = map_dimensions.CoordMap.FitToGrid(drag_location);
				point.Offset(origin.X << 5, origin.Y << 5);
				point.X = map_dimensions.NormalizeX(point.X);
				selected_area = Rectangle.FromLTRB(point.X, point.Y, point.X, point.Y);
				if (!isloading)
				{
					Invalidate();
					OnSelectionChanged(EventArgs.Empty);
				}
			}
			base.OnMouseDown(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			if (!isloading)
			{
				is_selecting = false;
				int num = (drag_location.X - e.X) / 32;
				int num2 = (drag_location.Y - e.Y) / 32;
				if (DateTime.Now.Subtract(drag_starttime).Milliseconds < 200 && Math.Abs(num) == 0 && Math.Abs(num2) == 0)
				{
					if (e.Button == MouseButtons.Right)
					{
						Point point = map_dimensions.CoordMap.ActualToBlocks(new Point(e.X, e.Y));
						int blockindex = (origin.X + point.X) % map_dimensions.SizeBlocks.Width + (origin.Y + point.Y) * map_dimensions.SizeBlocks.Width;
						OnContextMenu(new ContextMenuEventArgs(e.X, e.Y, blockindex));
					}
				}
				else if (e.Button == MouseButtons.Right)
				{
					MoveMapBlocks(num, num2, true);
				}
				Invalidate();
			}
			base.OnMouseDown(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			try
			{
				if (!is_mouse_over || scrollzonesize <= 0 || DateTime.Now.Subtract(lastmousemove_time).TotalMilliseconds < 100.0)
				{
					return;
				}
				lastmousemove_time = DateTime.Now;
				trackerlocation.X = e.X;
				trackerlocation.Y = e.Y;
				int num = 0;
				int num2 = 0;
				if (e.X < scrollzonesize)
				{
					num = -1;
				}
				else if (e.X > Width - scrollzonesize)
				{
					num = 1;
				}
				if (e.Y < scrollzonesize)
				{
					num2 = -1;
				}
				else if (e.Y > Height - scrollzonesize)
				{
					num2 = 1;
				}
				if (num != 0 || num2 != 0)
				{
					timer.StartScrolling(num, num2);
				}
				else if (timer.IsScrolling)
				{
					timer.StopScrolling();
				}
				if (is_selecting)
				{
					Point point = map_dimensions.CoordMap.FitToGrid(new Point(trackerlocation.X, trackerlocation.Y));
					if (map_dimensions.NormalizeX(point.X + (selection_crossed_dateline ? map_dimensions.Size.Width : 0)) > selected_area.X)
					{
						point.X += 32;
					}
					if (point.Y >= selected_area.Y)
					{
						point.Y += 32;
					}
					point.Offset(origin.X << 5, origin.Y << 5);
					point.X = map_dimensions.NormalizeX(point.X);
					Rectangle rectangle = Rectangle.FromLTRB(selected_area.X, selected_area.Y, point.X, point.Y);
					if (Math.Abs(rectangle.Width - selected_area.Width) > Lightmap.BaseWidth >> 1)
					{
						selection_crossed_dateline = !selection_crossed_dateline;
					}
					if (selected_area != rectangle)
					{
						selected_area = rectangle;
						OnSelectionChanged(EventArgs.Empty);
					}
				}
				Point point2 = trackerlocation;
				point2.Offset(origin.X << 5, origin.Y << 5);
				point2.X = map_dimensions.NormalizeX(point2.X);
				OnTrackerMoved(new TrackerMovedEventArgs(point2, map_dimensions.CoordMap.FitToGrid(point2)));
				Invalidate();
			}
			finally
			{
				base.OnMouseMove(e);
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			if (isloading)
			{
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				new Rectangle((ClientRectangle.Width - 32) / 2, (ClientRectangle.Height - 32) / 2, 32, 32);
				Point point = new Point(ClientRectangle.Width / 2, ClientRectangle.Height / 2);
				Pen pen = new Pen(loadingflag ? SystemBrushes.ControlDark : SystemBrushes.ControlDarkDark, 6f);
				try
				{
					for (int i = 0; i < 13; i++)
					{
						if (i >= loadingindex)
						{
							pen.Dispose();
							pen = new Pen(loadingflag ? SystemBrushes.ControlDarkDark : SystemBrushes.ControlDark, 6f);
						}
						double num = 0.483321946706122 * (double)i;
						Point pt = new Point(point.X + (int)(Math.Sin(num) * 32.0 / 3.0 * 2.0), point.Y - (int)(Math.Cos(num) * 32.0 / 3.0 * 2.0));
						Point pt2 = new Point(point.X + (int)(Math.Sin(num) * 32.0), point.Y - (int)(Math.Cos(num) * 32.0));
						graphics.DrawLine(pen, pt, pt2);
						graphics.FillEllipse(pen.Brush, pt.X - 3, pt.Y - 3, 6, 6);
						graphics.FillEllipse(pen.Brush, pt2.X - 3, pt2.Y - 3, 6, 6);
					}
				}
				finally
				{
					pen.Dispose();
				}
			}
			else
			{
				Rectangle rectangle = CalcCanvas();
				graphics.Clip = new Region(rectangle);
				PaintLayers(e.Graphics, rectangle);
				if (drawdateline)
				{
					PaintDateline(e.Graphics, rectangle);
				}
				PaintRegionBorders(e.Graphics, rectangle);
				PaintSelection(graphics, rectangle);
				if (gridmode == MapGridMode.Full)
				{
					PaintFullGrid(e.Graphics, rectangle);
				}
				else if (gridmode == MapGridMode.Near && is_mouse_over)
				{
					PaintNearGrid(e.Graphics, rectangle);
				}
				if (is_mouse_over)
				{
					if (drawtrackers)
					{
						PaintTrackers(e.Graphics, rectangle);
					}
					PaintScrollzones(e.Graphics, rectangle);
				}
				graphics.Clip = new Region();
			}
			PaintBorder(graphics);
			base.OnPaint(e);
		}

		private void PaintLayers(Graphics g, Rectangle canvas)
		{
			lock (surfaces)
			{
				for (int i = 0; i < surfaces.Count; i++)
				{
					if (surfaces[i] != null)
					{
						g.DrawImageUnscaled((Bitmap)surfaces[i], canvas.X, canvas.Y);
					}
				}
			}
		}

		private void PaintBorder(Graphics g)
		{
			if (border == BorderStyle.FixedSingle)
			{
				g.DrawRectangle(SystemPens.ControlDark, 0, 0, Width - 1, Height - 1);
			}
			else if (border == BorderStyle.Fixed3D)
			{
				ControlPaint.DrawBorder3D(g, 0, 0, Width, Height, Border3DStyle.Sunken);
			}
		}

		private void PaintNearGrid(Graphics g, Rectangle canvas)
		{
			int num = 160;
			Rectangle r = new Rectangle(trackerlocation.X - num / 2, trackerlocation.Y - num / 2, num, num);
			Rectangle area = new Rectangle(Lightmap.FitToGrid(r.X), Lightmap.FitToGrid(r.Y), Lightmap.FitToGrid(r.Width + 32), Lightmap.FitToGrid(r.Height + 32));
			Point point = map_dimensions.CoordMap.BlocksToActual(origin);
			Region clip = g.Clip;
			try
			{
				GraphicsPath graphicsPath = new GraphicsPath();
				graphicsPath.AddEllipse(r);
				g.Clip = new Region(graphicsPath);
				Pen pen = null;
				Pen pen2 = null;
				Pen pen3 = null;
				try
				{
					pen = new Pen(Color.FromArgb((int)(gridopacity * 255.0), gridcolor), 1f);
					pen.DashStyle = DashStyle.Dot;
					pen2 = new Pen(Color.FromArgb((int)(gridopacity * 255.0), gridcolor), 1f);
					pen2.DashStyle = DashStyle.Dash;
					pen3 = new Pen(Color.FromArgb((int)(gridopacity * 255.0), gridcolor), 1f);
					for (int i = area.Top - 1; i < area.Bottom; i += 32)
					{
						if ((point.Y + i + 1) % 128 == 0)
						{
							g.DrawLine(pen3, area.Left, i, area.Right, i);
						}
						else if ((point.Y + i + 1) % 64 == 0)
						{
							g.DrawLine(pen2, area.Left, i, area.Right, i);
						}
						else
						{
							g.DrawLine(pen, area.Left, i, area.Right, i);
						}
					}
					for (int j = area.Left - 1; j < area.Right; j += 32)
					{
						if ((point.X + j + 1) % 128 == 0)
						{
							g.DrawLine(pen3, j, area.Top, j, area.Bottom);
						}
						else if ((point.X + j + 1) % 64 == 0)
						{
							g.DrawLine(pen2, j, area.Top, j, area.Bottom);
						}
						else
						{
							g.DrawLine(pen, j, area.Top, j, area.Bottom);
						}
					}
				}
				finally
				{
					if (pen != null)
					{
						pen.Dispose();
					}
					if (pen2 != null)
					{
						pen2.Dispose();
					}
					if (pen3 != null)
					{
						pen3.Dispose();
					}
				}
				if (drawcoordinates)
				{
					PaintCoordinates(g, area);
				}
			}
			finally
			{
				g.Clip = clip;
			}
		}

		private void PaintFullGrid(Graphics g, Rectangle canvas)
		{
			using (Pen pen = new Pen(Color.FromArgb((int)(gridopacity * 255.0), gridcolor), 1f))
			{
				for (int i = canvas.Top + 32 - 1; i < canvas.Bottom; i += 32)
				{
					g.DrawLine(pen, canvas.Left, i, canvas.Right, i);
				}
				for (int j = canvas.Left + 32 - 1; j < canvas.Right; j += 32)
				{
					g.DrawLine(pen, j, canvas.Top, j, canvas.Bottom);
				}
			}
			if (drawcoordinates)
			{
				PaintCoordinates(g, canvas);
			}
		}

		private void PaintCoordinates(Graphics g, Rectangle area)
		{
			StringFormat format = new StringFormat();
			g.TextRenderingHint = TextRenderingHint.AntiAlias;
			Point point = map_dimensions.CoordMap.BlocksToActual(origin);
			using (Brush brush = new SolidBrush(Color.FromArgb((int)(coordinatesopacity * 255.0), coordinatescolor)))
			{
				for (int i = area.Top - 1; i < area.Bottom; i += 32)
				{
					for (int j = area.Left - 1; j < area.Right; j += 32)
					{
						g.DrawString(map_dimensions.NormalizeX(point.X + j + 1) + "\n" + map_dimensions.NormalizeX(point.Y + i + 1), coordinatesfont, brush, new RectangleF(j, i, 32f, 32f), format);
					}
				}
			}
		}

		private void PaintDateline(Graphics g, Rectangle canvas)
		{
			Point point = new Point(origin.X << 5, origin.Y << 5);
			if (point.X > Lightmap.BaseWidth - canvas.Width)
			{
				using (Pen pen = new Pen(Color.FromArgb((int)(datelineopacity * 255.0), datelinecolor), 3f))
				{
					pen.DashStyle = DashStyle.DashDot;
					g.DrawLine(pen, Lightmap.BaseWidth - point.X - 1, canvas.Top, Lightmap.BaseWidth - point.X - 1, canvas.Height);
				}
			}
		}

		private void PaintRegionBorders(Graphics g, Rectangle canvas)
		{
			if (drawRegionBorders == RegionDrawMode.None)
			{
				return;
			}
			Point point = new Point(origin.X << 5, origin.Y << 5);
			int num = (int)(Math.Ceiling((double)(point.X / 2048)) * 2048.0) - point.X;
			int num2 = (int)(Math.Ceiling((double)(point.Y / 2048)) * 2048.0) - point.Y;
			if (drawRegionBorders == RegionDrawMode.BordersAndOverlap)
			{
				using (Brush brush = new SolidBrush(Color.FromArgb((int)(regionoverlapopacity * 255.0), regionoverlapcolor)))
				{
					Region region = new Region();
					for (int i = 0; i < canvas.Height + 2048; i += 2048)
					{
						region.Exclude(new Rectangle(canvas.Left, num2 + i - 256, canvas.Width, 512));
					}
					g.SetClip(region, CombineMode.Replace);
					for (int j = 0; j < canvas.Width + 2048; j += 2048)
					{
						g.FillRectangle(brush, num + j - 256, canvas.Top, 512, canvas.Height);
					}
					g.ResetClip();
					for (int k = 0; k < canvas.Height + 2048; k += 2048)
					{
						g.FillRectangle(brush, canvas.Left, num2 + k - 256, canvas.Width, 512);
					}
				}
			}
			using (Pen pen = new Pen(Color.FromArgb((int)(regionborderopacity * 255.0), regionbordercolor), 3f))
			{
				pen.DashStyle = DashStyle.Dot;
				for (int l = 0; l < canvas.Width + 2048; l += 2048)
				{
					g.DrawLine(pen, num + l - 1, canvas.Top, num + l - 1, canvas.Height);
				}
				for (int m = 0; m < canvas.Height + 2048; m += 2048)
				{
					g.DrawLine(pen, canvas.Left, num2 + m - 1, canvas.Width, num2 + m - 1);
				}
			}
		}

		private void PaintSelection(Graphics g, Rectangle canvas)
		{
			if (selected_area != Rectangle.Empty)
			{
				Rectangle rect = ConvertToDrawSelection(selected_area);
				using (Brush brush = new SolidBrush(Color.FromArgb(30, Color.OrangeRed)))
				{
					rect.Inflate(-1, -1);
					g.FillRectangle(brush, rect);
					rect.Inflate(1, 1);
				}
				using (Pen pen = new Pen(Color.FromArgb(70, Color.White), 6f))
				{
					g.DrawRectangle(pen, rect);
				}
				using (Pen pen2 = new Pen(Color.OrangeRed, 2f))
				{
					g.DrawRectangle(pen2, rect);
				}
			}
		}

		private void PaintTrackers(Graphics g, Rectangle canvas)
		{
			using (Pen pen = new Pen(Color.FromArgb((int)(trackeropacity * 255.0), trackercolor), 1f))
			{
				g.DrawLine(pen, canvas.Left, trackerlocation.Y, canvas.Right, trackerlocation.Y);
				g.DrawLine(pen, trackerlocation.X, canvas.Top, trackerlocation.X, canvas.Bottom);
			}
		}

		private void PaintScrollzones(Graphics g, Rectangle canvas)
		{
			Rectangle[] array = new Rectangle[4];
			int num = scrollzonesize;
			array[0] = new Rectangle(canvas.Left, canvas.Top, canvas.Width, num);
			array[1] = new Rectangle(canvas.Left, canvas.Top + num, num, canvas.Height - num * 2);
			array[2] = new Rectangle(canvas.Left, canvas.Bottom - num, canvas.Width, num);
			array[3] = new Rectangle(canvas.Right - num, canvas.Top + num, num, canvas.Height - num * 2);
			using (Brush brush = new SolidBrush(Color.FromArgb((int)(scrollzoneopacity * 255.0), scrollzonecolor)))
			{
				g.FillRectangles(brush, array);
			}
		}

		protected virtual void OnBorderStyleChanged(EventArgs e)
		{
			BorderStyleChangedEventHandler borderStyleChanged = BorderStyleChanged;
			if (borderStyleChanged != null)
			{
				borderStyleChanged(this, e);
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
