using System;
using System.Collections;
using LayerPainter;

namespace EU2MapEditorControls
{
	public class LayerPainterCollection : ICollection, IEnumerable
	{
		public delegate void LayerAddedEventHandler(object sender, LayerActionEventArgs e);

		public delegate void LayerRemovedEventHandler(object sender, LayerActionEventArgs e);

		public delegate void LayerPropertyChangedEventHandler(object sender, LayerActionEventArgs e);

		private ArrayList layers;

		public LayerPainterBase this[int index]
		{
			get
			{
				if (index < 0 || index >= layers.Count)
				{
					throw new ArgumentOutOfRangeException();
				}
				return (LayerPainterBase)layers[index];
			}
		}

		public LayerPainterBase this[string name]
		{
			get
			{
				if (name == null || name.Length == 0)
				{
					throw new ArgumentOutOfRangeException();
				}
				int layerIndex = getLayerIndex(name);
				if (layerIndex < 0 || layerIndex >= layers.Count)
				{
					throw new ArgumentOutOfRangeException();
				}
				return (LayerPainterBase)layers[layerIndex];
			}
		}

		public bool IsSynchronized
		{
			get
			{
				return layers.IsSynchronized;
			}
		}

		public int Count
		{
			get
			{
				return layers.Count;
			}
		}

		public object SyncRoot
		{
			get
			{
				return layers.SyncRoot;
			}
		}

		public event LayerAddedEventHandler LayerAdded;

		public event LayerRemovedEventHandler LayerRemoved;

		public event LayerPropertyChangedEventHandler LayerPropertyChanged;

		public LayerPainterCollection()
		{
			layers = new ArrayList();
		}

		public LayerPainterCollection(LayerPainterBase layer)
		{
			layers = new ArrayList();
			layers.Add(layer);
		}

		public LayerPainterCollection(LayerPainterBase[] startlayers)
		{
			if (startlayers != null)
			{
				layers = new ArrayList(startlayers);
			}
			else
			{
				layers = new ArrayList();
			}
		}

		public bool Contains(string name)
		{
			if (name == null || name.Length == 0)
			{
				throw new ArgumentOutOfRangeException();
			}
			int layerIndex = getLayerIndex(name);
			if (layerIndex >= 0)
			{
				return layerIndex < layers.Count;
			}
			return false;
		}

		public int Add(LayerPainterBase layer)
		{
			if (layer == null)
			{
				throw new ArgumentNullException();
			}
			for (int i = 0; i < layers.Count; i++)
			{
				if (layers[i] == layer)
				{
					return i;
				}
			}
			layer.PropertyChanged += layer_PropertyChanged;
			int num = layers.Add(layer);
			OnLayerAdded(new LayerActionEventArgs(num, layer));
			return num;
		}

		public int Remove(int index)
		{
			if (index < 0 || index >= layers.Count)
			{
				throw new ArgumentOutOfRangeException();
			}
			LayerPainterBase layer = (LayerPainterBase)layers[index];
			layers.RemoveAt(index);
			OnLayerRemoved(new LayerActionEventArgs(index, layer));
			return index;
		}

		public int Remove(string name)
		{
			return Remove(getLayerIndex(name));
		}

		public int Remove(ILayerPainter layer)
		{
			return Remove(getLayerIndex(layer));
		}

		private void layer_PropertyChanged(object sender, EventArgs e)
		{
			ILayerPainter layer = (ILayerPainter)sender;
			OnLayerPropertyChanged(new LayerActionEventArgs(getLayerIndex(layer), layer));
		}

		private int getLayerIndex(string name)
		{
			for (int i = 0; i < layers.Count; i++)
			{
				if (((ILayerPainter)layers[i]).Name == name)
				{
					return i;
				}
			}
			return -1;
		}

		private int getLayerIndex(ILayerPainter layer)
		{
			return layers.IndexOf(layer);
		}

		protected virtual void OnLayerAdded(LayerActionEventArgs e)
		{
			LayerAddedEventHandler layerAdded = LayerAdded;
			if (layerAdded != null)
			{
				layerAdded(this, e);
			}
		}

		protected virtual void OnLayerRemoved(LayerActionEventArgs e)
		{
			LayerRemovedEventHandler layerRemoved = LayerRemoved;
			if (layerRemoved != null)
			{
				layerRemoved(this, e);
			}
		}

		protected virtual void OnLayerPropertyChanged(LayerActionEventArgs e)
		{
			LayerPropertyChangedEventHandler layerPropertyChanged = LayerPropertyChanged;
			if (layerPropertyChanged != null)
			{
				layerPropertyChanged(this, e);
			}
		}

		public void CopyTo(Array array, int index)
		{
			layers.CopyTo(array, index);
		}

		public IEnumerator GetEnumerator()
		{
			return layers.GetEnumerator();
		}
	}
}
