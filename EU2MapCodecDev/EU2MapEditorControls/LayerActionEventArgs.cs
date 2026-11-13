using System;
using LayerPainter;

namespace EU2MapEditorControls
{
	public class LayerActionEventArgs : EventArgs
	{
		private int index;

		private ILayerPainter layer;

		public int Index
		{
			get
			{
				return index;
			}
		}

		public ILayerPainter Layer
		{
			get
			{
				return layer;
			}
		}

		public LayerActionEventArgs(int index, ILayerPainter layer)
		{
			this.index = index;
			this.layer = layer;
		}
	}
}
