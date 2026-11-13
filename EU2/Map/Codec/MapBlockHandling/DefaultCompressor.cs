using System;
using System.Collections.Generic;
using EU2.Data;

namespace EU2.Map.Codec.MapBlockHandling
{
	/// <summary>
	/// Summary description for Compressor.
	/// </summary>
	public class DefaultCompressor : Compressor {
		public DefaultCompressor( int rx, int ry, int zoom, ProvinceList provinces, AdjacencyTable adjacent, IDMap idmap ) {
			this.rx = rx;
			this.ry = ry;
			this.zoom = zoom;
			this.provinces = provinces;
			this.adjacent = adjacent;
			this.idmap = idmap;

		}

		private sealed class BorderValue { internal ushort Province; internal int Bits; }
        private readonly Dictionary<long,ushort> borderKeys = new Dictionary<long,ushort>();
        private readonly Dictionary<ushort,BorderValue> borderValues = new Dictionary<ushort,BorderValue>();
        protected override bool AllowTemporaryOwnerIDs { get { return true; } }
        private ushort BorderKey(ushort province,int bits) {
            long key=((long)province<<9)|((long)bits&511);
            ushort token;
            if(borderKeys.TryGetValue(key,out token)) return token;
            token=(ushort)(0x8000+borderKeys.Count);
            borderKeys.Add(key,token); borderValues.Add(token,new BorderValue { Province=province, Bits=bits });
            return token;
        }
        protected override void OnAfterWalk() {
            base.OnAfterWalk();
            if(borderValues.Count==0) return;
            List<ushort> referenced=new List<ushort>();
            foreach(ushort key in orderedIDs) {
                BorderValue value;
                if(borderValues.TryGetValue(key,out value) && !referenced.Contains(value.Province)) referenced.Add(value.Province);
            }
            if(referenced.Count>60-OwnerPadding)
                throw new InvalidOperationException("Too many distinct border owners in one block for the FTG ID range.");
            Dictionary<ushort,byte> remap=new Dictionary<ushort,byte>();
            ushort[] final=new ushort[256]; int next=OwnerPadding;
            if(OwnerPadding>0) remap.Add(0,0);
            foreach(ushort province in referenced) { remap.Add(province,(byte)next); final[next++]=province; }
            for(int i=OwnerPadding;i<idindex;i++) {
                ushort key=orderedIDs[i];
                if(!remap.ContainsKey(key)) { if(next>=256) throw new InvalidOperationException("Too many owner entries in one block."); remap.Add(key,(byte)next++); }
            }
            foreach(KeyValuePair<ushort,byte> entry in remap) {
                BorderValue value;
                final[entry.Value]=borderValues.TryGetValue(entry.Key,out value)
                    ? (ushort)(((remap[value.Province]+4)<<9)|value.Bits) : entry.Key;
            }
            for(int i=0;i<leafindex;i++) owners[i]=remap[orderedIDs[owners[i]]];
            orderedIDs=final; idindex=next; idtable=null;
            idLookup=new Dictionary<ushort,byte>();
            for(int i=0;i<idindex;i++) if(!idLookup.ContainsKey(final[i])) idLookup.Add(final[i],(byte)i);
        }
        protected override int OwnerPadding { get { return provinces.Profile.BorderPadding; } }

        protected override void OnVisitNode( Node node, int x, int y ) {
			tree[treeindex++] = node.IsBranch();

			if ( node.IsBranch() && node.Level > 1 ) return;

			if ( node.IsBranch() ) { // 4 leaves on pixel level
				HandleNode( node.BottomRightChild, x+1, y+1 );
				HandleNode( node.BottomLeftChild, x, y+1 );
				HandleNode( node.TopRightChild, x+1, y );
				HandleNode( node.TopLeftChild, x, y );
			}
			else { // Leaf above pixel level
				owners[leafindex] = ConvertID( node.Data.ID );
				colors[leafindex++] = node.Data.Color;
			}
		}

		private void HandleNode( Node node, int x, int y ) {
			ushort neighbourid = 0;
			ushort provid = node.Data.ID;

            try {
                if (!provinces.Contains(provid))
                    throw new InvalidProvinceIDException(provid);
                if (node.Data.Border > 0 && provinces != null && (provinces[provid].IsLand() || provinces[provid].IsRiver())) {
                    byte color = (byte)(node.Data.Border - 1);
                    int adjx = rx + (x << zoom);
                    int adjy = ry + (y << zoom);

                    ushort riverid = provinces.Profile.BorderID;
                    if (provinces[provid].IsRiver()) { // Is River, convert province id to nearest land
                        riverid = provid;
                        provid = idmap.FindClosestLand(adjx, adjy, riverid, provinces);
                    }
                    neighbourid = idmap.FindClosestLand(adjx, adjy, provid, provinces, adjacent); // Get nearest neighbour

                    if (provid != neighbourid && provid != riverid && provinces[neighbourid].IsLand()) {
                        // We have 3 things now: 2 provinces and a possible river.
                        int neighadj = adjacent.GetAdjacencyIndex(provid, neighbourid);
                        if (neighadj >= 0 && neighadj < 16) {
                            int riveradj = Adjacent.Invalid;
                            if (riverid < provinces.Profile.ProvinceIDThreshold) { // Convert river to adjacency
                                riveradj = adjacent.GetAdjacencyIndex(provid, riverid);
                                if(provinces.Profile.IsFtg && riveradj >= 0) riveradj -= adjacent.RiverStart(provid);
                            }

                            if (riveradj >= 0) {
                                if (riveradj >= 16 || (riverid < provinces.Profile.ProvinceIDThreshold && riveradj == Adjacent.Invalid))
                                    throw new AdjacencyIndexOutOfRangeException(provid, riverid, riveradj);

                                ConvertID(provid);
                                owners[leafindex]=ConvertID(BorderKey(provid,color+(neighadj<<1)+(riveradj<<5)));
                            }
                            else {
                                owners[leafindex] = ConvertID(riverid); // Store only the river
                            }
                        }
                        else {
                            owners[leafindex] = ConvertID(provid); // Store the province
                        }
                    }
                    else {
                        owners[leafindex] = ConvertID(provid); // Store the province
                    }
                }
                else {
                    owners[leafindex] = ConvertID(provid); // Store the province
                }
                colors[leafindex++] = node.Data.Color;
            }
            catch (Exception ex) {
                throw new NodeCompressionException(node, x+rx, y+ry, ex);
            }
		}

		#region Private Fields
		private int zoom;
		private ProvinceList provinces;
		private AdjacencyTable adjacent;
		private IDMap idmap;
		private int rx;
		private int ry;
		#endregion
	}

    public class NodeCompressionException : Exception {
        Node node;
        int x, y;

        public NodeCompressionException() {
            node = null;
            x = 0;
            y = 0;
        }

        public NodeCompressionException(Node node, int x, int y, Exception innerException)
            : base(string.Format("Node compression failed at ({0},{1}): {2}", x, y, innerException.Message), innerException) {
            this.node = node;
            this.x = x;
            this.y = y;
        }

        public NodeCompressionException(string message)
            : base(message) {
            node = null;
            x = 0;
            y = 0;
        }

        public int X {
            get { return x; }
        }

        public int Y {
            get { return y; }
        }
    }

    public class ProvinceIndexOutOfRangeException : Exception {
        private int index;
        private int provinceId;

        public ProvinceIndexOutOfRangeException()
            : base("Converting a province id to a block based index failed: the index is out of range.") {
        }

        public ProvinceIndexOutOfRangeException(int provinceId, int index)
            : base(string.Format("Converting a province id (={0}) to a block based index (={1}) failed: the index is out of range.", provinceId, index)) {
            this.index = index;
            this.provinceId = provinceId;
        }

        public ProvinceIndexOutOfRangeException(string message)
            : base(message) {
        }

        public int Index {
            get { return index; }
        }

        public int ProvinceId {
            get { return provinceId; }
        }
    }

    public class AdjacencyIndexOutOfRangeException : Exception {
        private int riveradj;
        private int provinceId;
        private int riverId;

        public AdjacencyIndexOutOfRangeException()
            : base("River adjacency index between province and river produced is out of range") {
        }

        public AdjacencyIndexOutOfRangeException(int provid, int riverid, int riveradj)
            : base(string.Format("River adjacency index (={2}) lookup between province (={0}) and river (={1}) is out of range.", provid, riverid, riveradj)) {
            this.provinceId = provid;
            this.riverId = riverid;
            this.riveradj = riveradj;
        }

        public AdjacencyIndexOutOfRangeException(string message)
            : base(message) {
        }

        public int ProvinceId {
            get { return provinceId; }
        }

        public int RiverId {
            get { return riverId; }
        }

        public int RiverAdj {
            get { return riveradj; }
        }

    }

}
