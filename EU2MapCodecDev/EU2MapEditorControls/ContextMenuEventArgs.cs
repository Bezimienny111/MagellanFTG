namespace EU2MapEditorControls
{
	public class ContextMenuEventArgs
	{
		private int x;

		private int y;

		private int blockindex;

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

		public int BlockIndex
		{
			get
			{
				return blockindex;
			}
		}

		public ContextMenuEventArgs(int x, int y, int blockindex)
		{
			this.x = x;
			this.y = y;
			this.blockindex = blockindex;
		}
	}
}
