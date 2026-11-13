using System.Drawing;

namespace EU2MapEditorControls
{
	public class TrackerMovedEventArgs
	{
		private int x;

		private int y;

		private int gx;

		private int gy;

		public int X
		{
			get
			{
				return x;
			}
		}

		public int Y
		{
			get
			{
				return y;
			}
		}

		public int GridX
		{
			get
			{
				return gx;
			}
		}

		public int GridY
		{
			get
			{
				return gy;
			}
		}

		public TrackerMovedEventArgs(Point real, Point gridfit)
		{
			x = real.X;
			y = real.Y;
			gx = gridfit.X;
			gy = gridfit.Y;
		}
	}
}
