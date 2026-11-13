using System;
using System.Drawing;

namespace EU2.Map
{
	public struct MapSize : IEquatable<MapSize>, IComparable<MapSize>
	{
		public const int BlockFactor = 5;

		public const int BlockSize = 32;

		public const int PixelsPerBlock = 1024;

		private static readonly MapSize eu2default = new MapSize(18944, 7296);

		private Size sz;

		public static MapSize Default
		{
			get
			{
				return eu2default;
			}
		}

		public int BaseWidth
		{
			get
			{
				return sz.Width;
			}
		}

		public int BaseHeight
		{
			get
			{
				return sz.Height;
			}
		}

		public Size BaseSize
		{
			get
			{
				return sz;
			}
		}

		public MapSize(Size baseSize)
		{
			if (baseSize.Width <= 0)
			{
				throw new ArgumentOutOfRangeException("baseSize", "Width must be larger than 0.");
			}
			if (baseSize.Height <= 0)
			{
				throw new ArgumentOutOfRangeException("baseSize", "Height must be larger than 0.");
			}
			if (baseSize.Width % 32 != 0)
			{
				throw new ArgumentOutOfRangeException("baseSize", "Width must be a multiple of the blocksize.");
			}
			if (baseSize.Height % 32 != 0)
			{
				throw new ArgumentOutOfRangeException("baseSize", "Height must be a multiple of the blocksize.");
			}
			sz = baseSize;
		}

		public MapSize(int baseWidth, int baseHeight)
		{
			if (baseWidth <= 0)
			{
				throw new ArgumentOutOfRangeException("baseWidth", "Width must be larger than 0.");
			}
			if (baseHeight <= 0)
			{
				throw new ArgumentOutOfRangeException("baseHeight", "Height must be larger than 0.");
			}
			if (baseWidth % 32 != 0)
			{
				throw new ArgumentOutOfRangeException("baseWidth", "Width must be a multiple of the blocksize.");
			}
			if (baseHeight % 32 != 0)
			{
				throw new ArgumentOutOfRangeException("baseHeight", "Height must be a multiple of the blocksize.");
			}
			sz = new Size(baseWidth, baseHeight);
		}

		public int BaseNormalizeX(int x)
		{
			x %= BaseWidth;
			if (x < 0)
			{
				x += BaseWidth;
			}
			return x;
		}

		public int BaseNormalizeBlockX(int x)
		{
			x %= BaseWidth >> 5;
			if (x < 0)
			{
				x += BaseWidth >> 5;
			}
			return x;
		}

		public bool Equals(MapSize other)
		{
			return other.sz == sz;
		}

		public int CompareTo(MapSize other)
		{
			int num = BaseWidth.CompareTo(other.BaseWidth);
			if (num == 0)
			{
				num = BaseHeight.CompareTo(other.BaseHeight);
			}
			return num;
		}

		public static bool operator ==(MapSize a, MapSize b)
		{
			return a.sz == b.sz;
		}

		public static bool operator !=(MapSize a, MapSize b)
		{
			return a.sz != b.sz;
		}

		public override bool Equals(object other) { return other is MapSize && Equals((MapSize)other); }

		public override int GetHashCode() { return sz.GetHashCode(); }

		public override string ToString()
		{
			return string.Format("{0}x{1}", BaseWidth, BaseHeight);
		}
	}
}
