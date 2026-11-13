using System;
using System.Collections;
using System.Collections.Generic;

namespace EU2.Map.Codec.MapBlockHandling
{
	/// <summary>
	/// Summary description for Compressor.
	/// </summary>
	public abstract class Compressor : BreadthFirstTreeWalker {
		protected Compressor() : base( true ) {
		}

		protected virtual bool AllowTemporaryOwnerIDs { get { return false; } }
        protected virtual int OwnerPadding { get { return 0; } }

        protected override void OnBeforeWalk() {
			colors = new byte[1024];
			owners = new byte[1024];
			idtable = null;
            idLookup = new Dictionary<ushort, byte>();
            orderedIDs = new ushort[256];
			tree = new BitArray( 8192 );
			treeindex = 0;
			leafindex = 0;
			idindex = OwnerPadding;
            if(idindex>0) idLookup.Add(0,0);


		}

		protected override void OnAfterWalk() {
			tree.Length = treeindex;
		}

		public int IDCount { get { return idindex; } }
		// Compatibility view for callers of the old API; the encoder uses OrderedIDs.
        public byte[] IDTable {
            get {
                if (idtable == null) {
                    idtable = new byte[ushort.MaxValue];
                    for (int i=0; i<idtable.Length; i++) idtable[i] = byte.MaxValue;
                    for (int i=0; i<idindex; i++) idtable[orderedIDs[i]] = (byte)i;
                }
                return idtable;
            }
        }
        public ushort[] OrderedIDs { get { return orderedIDs; } }

		public int LeafCount { get { return leafindex; } }
		public byte[] Owners { get { return owners; } }
		public byte[] Colors { get { return colors; } }

		public byte[] Tree {
			get {
				byte[] result = new byte[(treeindex+7)/8];
				tree.CopyTo( result, 0 );
				return result;
			}
		}

		protected byte ConvertID(ushort id) {
            byte index;
            if (idLookup.TryGetValue(id, out index)) return index;
            if (idindex >= orderedIDs.Length)
                throw new InvalidOperationException("A lightmap block cannot contain more than 256 owner entries.");
            if (id >= 0x8000 && !AllowTemporaryOwnerIDs)
                throw new InvalidOperationException("A lightmap owner entry must fit in 15 bits.");
            index = (byte)idindex;
            orderedIDs[idindex++] = id;
            idLookup.Add(id, index);
            idtable = null;
            return index;
        }

		#region Protected Fields
		protected BitArray tree;
		protected byte[] idtable;
        protected Dictionary<ushort, byte> idLookup;
        protected ushort[] orderedIDs;
		protected byte[] owners;
		protected byte[] colors;
		protected int leafindex;
		protected int idindex;
		protected int treeindex;
		#endregion
	}
}
