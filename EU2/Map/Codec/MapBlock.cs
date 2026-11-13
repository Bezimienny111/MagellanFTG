using System;
using System.Text;
using System.IO;
using System.Collections;
using EU2.Data;

namespace EU2.Map.Codec
{
	/// <summary>
	/// Summary description for MapBlock.
	/// </summary>
	public class MapBlock : GenericMapBlock {
		#region Public Enumerations
		public enum WalkMode {
			Full,
			LeftOnly,
			TopLeftOnly,
			TopOnly
		}
		#endregion

		private const int IdTableSize = 256;
		private const int Terminator = 128;

		public MapBlock( ) {
			Erase();
		}

		public MapBlock( Node root ) {
			tree = root;
		}

		public MapBlock(CompressedBlock compressed, AdjacencyTable adjacent) : this(compressed,adjacent,adjacent==null ? new MapProfile() : adjacent.Profile) { }
        public MapBlock(CompressedBlock compressed, AdjacencyTable adjacent, MapProfile profile) { Decompress(compressed,adjacent,profile); }

		public static int CalculateCompressedSize( byte[] data ) {
			try {
				int index = 0;

				// Decompress the ID table from the data stream
				int idcount=0;
                do {
                    if(index+1>=data.Length || idcount>=IdTableSize) return -1;
                    idcount++; index+=2;
                } while(data[index-1] < Terminator);

				// Decompress the tree from the data stream
				Node tree = DecompressTree( data, ref index );
				double leafcount = tree.CalcLeafCount();
				int ownercount = (int)leafcount;

				if ( idcount <= 1 )
					ownercount = 0;
				else if ( idcount == 2 )
					ownercount = (int)Math.Ceiling( leafcount / 8 );
				else if ( idcount <= 4 )
					ownercount = (int)Math.Ceiling( leafcount / 4 );
				else if ( idcount <= 16 )
					ownercount = (int)Math.Ceiling( leafcount / 2 );

				if ( leafcount % 4 != 0 ) leafcount += 4 - (leafcount % 4);
				leafcount = Math.Ceiling(leafcount * 3 / 4);
				int result = index + ownercount + (int)leafcount;
				//if ( result % 4 != 0 ) result += 4 - (result % 4);

				return result;
			}
			catch {
				return -1;
			}
		}

		public void Decompress(CompressedBlock compressed, AdjacencyTable adjacent) { Decompress(compressed,adjacent,adjacent==null ? new MapProfile() : adjacent.Profile); }
        public void Decompress( CompressedBlock compressed, AdjacencyTable adjacent, MapProfile profile ) {
            profile=compressed.EncodingProfile ?? profile;
			int index = 0;

			// Decompress the ID table from the data stream
			Pixel[] owners;
			int ownercount = DecompressIdTable( compressed.Data, ref index, adjacent, profile, out owners );

			// Decompress the tree from the data stream
			tree = DecompressTree( compressed.Data, ref index );
			int leafcount = tree.CalcLeafCount();
			// Create the leaf array
			Pixel[] leafs = new Pixel[leafcount + (leafcount % 8 == 0 ? 0 : 8-(leafcount % 8))];

			// Decompress the ownership table next
			DecompressOwnership( compressed.Data, ref index, leafs, leafcount, owners, ownercount );

			// Finally, read the color data, which contains the "color" data for each leaf.
			DecompressColorInfo( compressed.Data, ref index, leafs, leafcount );

			// Assign the leaf data to the nodes
			int leafIndex = 0;
			PopulateNode( tree, leafs, ref leafIndex );
		}

		public CompressedBlock Compress( int xreal, int yreal, int zoom, ProvinceList provinces, AdjacencyTable adjacent, IDMap idmap ) {
			// Walk the tree, storing the info along the way
			MapBlockHandling.Compressor compressor;
			if ( provinces == null || adjacent == null || idmap == null )
				compressor = new MapBlockHandling.LesserCompressor();
			else
				compressor = new MapBlockHandling.DefaultCompressor( xreal, yreal, zoom, provinces, adjacent, idmap );
			WalkTree( compressor );

			// Write away idtable
			int datasize = compressor.IDCount*2 + compressor.Tree.Length + (compressor.LeafCount+3)/4*3;
			if ( compressor.IDCount == 2 )
				datasize += (compressor.LeafCount+7)/8;
			else if ( compressor.IDCount == 3 || compressor.IDCount == 4 )
				datasize += (compressor.LeafCount+3)/4;
			else if ( compressor.IDCount > 4 && compressor.IDCount <= 16 )
				datasize += (compressor.LeafCount+1)/2;
			else if ( compressor.IDCount > 16 )
				datasize += compressor.LeafCount;

			byte[] data = new byte[datasize];
			int dataindex = 0;

            // Owner IDs are already recorded in insertion order by the compressor.
            ushort[] idtable = compressor.OrderedIDs;
            for (int i=0; i<compressor.IDCount; i++) {
                data[dataindex++] = (byte)(idtable[i] & 255);
                data[dataindex++] = (byte)(idtable[i] >> 8);
            }
			data[dataindex-1] |= Terminator;

			byte[] tree = compressor.Tree;
			for ( int i=0; i<tree.Length; ++i ) {
				data[dataindex++] = tree[i];
			}

			if ( compressor.IDCount == 2 ) {
				for ( int i=0; i<compressor.LeafCount; i+=8 ) {
					data[dataindex++] = (byte)(
						(compressor.Owners[i]) |
						(compressor.Owners[i+1] << 1) |
						(compressor.Owners[i+2] << 2) |
						(compressor.Owners[i+3] << 3) |
						(compressor.Owners[i+4] << 4) |
						(compressor.Owners[i+5] << 5) |
						(compressor.Owners[i+6] << 6) |
						(compressor.Owners[i+7] << 7) );
				}
			}
			else if ( compressor.IDCount == 3 || compressor.IDCount == 4 ) {
				for ( int i=0; i<compressor.LeafCount; i+=4 ) {
					data[dataindex++] = (byte)(
						(compressor.Owners[i]) |
						(compressor.Owners[i+1] << 2) |
						(compressor.Owners[i+2] << 4) |
						(compressor.Owners[i+3] << 6) );
				}
			}
			else if ( compressor.IDCount > 4 && compressor.IDCount <= 16 ) {
				for ( int i=0; i<compressor.LeafCount; i+=2 ) {
					data[dataindex++] = (byte)(
						(compressor.Owners[i]) |
						(compressor.Owners[i+1] << 4) );
				}
			}
			else if ( compressor.IDCount > 16 ) {
				for ( int i=0; i<compressor.LeafCount; ++i ) {
					data[dataindex++] = (byte)(compressor.Owners[i]);
				}
			}

			// Finally, the 6-bit color values
			for ( int i=0; i<compressor.LeafCount; i+=4) {
				data[dataindex++] = (byte)((compressor.Colors[i] & 63) | ((compressor.Colors[i+1] & 63) << 6));
				data[dataindex++] = (byte)(((compressor.Colors[i+1] & 63) >> 2) | ((compressor.Colors[i+2] & 63) << 4));
				data[dataindex++] = (byte)(((compressor.Colors[i+2] & 63) >> 4) | ((compressor.Colors[i+3] & 63) << 2));
			}

			return new CompressedBlock( data );
		}

		public override bool IsCompressed() {
			return false;
		}

		public Node Tree {
			get { return tree; }
		}

		public void Erase( ) {
			tree = new Node( false );
			tree.BottomRightChild = new Node( Pixel.Empty );
			tree.BottomLeftChild = new Node( Pixel.Empty );
			tree.TopRightChild = new Node( Pixel.Empty );
			tree.TopLeftChild = new Node( Pixel.Empty );
		}

		public static ushort[] GetRawIDTable( CompressedBlock compressed ) {
			byte[] data = compressed.Data;

			// -- Get a list of province ids
			int idCount = -1;
			int index = 0;
			ushort[] idTable = new ushort[IdTableSize];
			do {
				++idCount;
				idTable[idCount] = (ushort)(data[index] + ((data[index+1] & 127) << 8));
				index += 2;
			} while ( data[index-1] < Terminator );
			++idCount;

			ushort[] result = new ushort[idCount];
			Array.Copy( idTable, 0, result, 0, idCount );
			return result;
		}



        /// <summary>
        /// Migrates the owner-table address space without re-encoding the quadtree,
        /// shading or adjacency choices made by the original map author.
        /// </summary>
        public static CompressedBlock RebaseOwnerTable(CompressedBlock source, MapProfile target) {
            if (source == null) throw new ArgumentNullException("source");
            if (target == null) throw new ArgumentNullException("target");
            MapProfile before = source.EncodingProfile;
            if (before == null)
                throw new InvalidOperationException("An owner-table migration requires the source block's encoding profile.");
            if (before.IsFtg != target.IsFtg)
                throw new InvalidOperationException("Owner-table migration cannot change FTG river semantics.");
            int oldThreshold = before.ProvinceIDThreshold;
            int newThreshold = target.ProvinceIDThreshold;
            if (newThreshold < oldThreshold)
                throw new InvalidOperationException("Owner-table migration cannot lower the defined-province threshold.");
            byte[] data = source.Data;
            if (data == null) throw new InvalidDataException("The compressed block has no data.");

            ushort[] oldIDs = new ushort[IdTableSize];
            int oldCount = 0, position = 0;
            bool terminated = false;
            while (oldCount < IdTableSize) {
                if (position + 1 >= data.Length)
                    throw new EndOfStreamException("The block owner table has no complete terminator.");
                ushort value = (ushort)(data[position] | ((data[position+1] & 127) << 8));
                terminated = (data[position+1] & Terminator) != 0;
                oldIDs[oldCount++] = value;
                position += 2;
                if (terminated) break;
            }
            if (!terminated)
                throw new InvalidDataException("A block owner table cannot exceed 256 entries.");

            // Only normal province-ID entries may be referenced by a border token.
            // Put these entries first, so a high old physical index (e.g. 54) does
            // not become an unencodable index after adding the leading zero slots.
            bool[] referenced = new bool[oldCount];
            int referenceCount = 0;
            for (int i=0; i<oldCount; i++) {
                if (oldIDs[i] < oldThreshold) {
                    if (oldIDs[i] >= newThreshold)
                        throw new InvalidDataException("A normal province ID would become a border token.");
                    continue;
                }
                int owner = ((oldIDs[i] >> 9) & 63) - 4;
                if (owner < 0 || owner >= oldCount || oldIDs[owner] >= oldThreshold)
                    throw new InvalidDataException("A border token references an invalid normal-owner entry.");
                if (!referenced[owner]) { referenced[owner] = true; referenceCount++; }
            }
            int padding = target.BorderPadding;
            if (referenceCount > 60 - padding)
                throw new InvalidOperationException("The block has too many border owners for the target province threshold.");
            int newCount = oldCount + padding;
            if (newCount > IdTableSize)
                throw new InvalidOperationException("Owner-table migration would exceed 256 physical entries.");

            int[] remap = new int[oldCount];
            for (int i=0; i<oldCount; i++) remap[i] = -1;
            int next = padding;
            for (int i=0; i<oldCount; i++) if (referenced[i]) remap[i] = next++;
            for (int i=0; i<oldCount; i++) if (remap[i] < 0) remap[i] = next++;
            ushort[] newIDs = new ushort[newCount]; // Leading padding is raw0.
            for (int i=0; i<oldCount; i++) {
                int value = oldIDs[i];
                if (value >= oldThreshold) {
                    int owner = ((value >> 9) & 63) - 4;
                    int newOwner = remap[owner];
                    if (newOwner + 4 >= 64)
                        throw new InvalidOperationException("A migrated border owner exceeds its six-bit index.");
                    value = ((newOwner + 4) << 9) | (value & 511);
                    if (value < newThreshold)
                        throw new InvalidOperationException("A migrated border token overlaps the target province IDs.");
                }
                if (value >= 0x8000)
                    throw new InvalidDataException("An owner-table value overlaps the terminator bit.");
                newIDs[remap[i]] = (ushort)value;
            }

            int treeStart = position;
            Node decodedTree;
            try { decodedTree = DecompressTree(data, ref position); }
            catch (Exception error) {
                if (!(error is IndexOutOfRangeException) && !(error is ArgumentException)) throw;
                throw new InvalidDataException("The compressed block contains an invalid quadtree.", error);
            }
            int treeBytes = position - treeStart;
            int leafCount = decodedTree.CalcLeafCount();
            if (leafCount < 1 || leafCount > 1024 || position > data.Length)
                throw new InvalidDataException("The compressed block has an invalid leaf count.");
            int oldBits = OwnerIndexBits(oldCount), newBits = OwnerIndexBits(newCount);
            int oldOwnerBytes = (leafCount * oldBits + 7) / 8;
            int newOwnerBytes = (leafCount * newBits + 7) / 8;
            int oldOwnerStart = position;
            int colorStart = oldOwnerStart + oldOwnerBytes;
            int minimumColorBytes = ((leafCount + 3) / 4) * 3;
            if (colorStart > data.Length || data.Length - colorStart < minimumColorBytes)
                throw new EndOfStreamException("The compressed block is missing ownership or shading bytes.");
            int colorBytes = data.Length - colorStart;
            byte[] result = new byte[newCount * 2 + treeBytes + newOwnerBytes + colorBytes];
            for (int i=0; i<newCount; i++) {
                result[i*2] = (byte)newIDs[i];
                result[i*2+1] = (byte)(newIDs[i] >> 8);
            }
            result[newCount*2-1] |= Terminator;
            Array.Copy(data, treeStart, result, newCount*2, treeBytes);
            int newOwnerStart = newCount*2 + treeBytes;
            int oldMask = oldBits == 0 ? 0 : (1 << oldBits) - 1;
            for (int i=0; i<leafCount; i++) {
                int bit = i * oldBits;
                int oldOwner = oldBits == 0 ? 0 : (data[oldOwnerStart + (bit >> 3)] >> (bit & 7)) & oldMask;
                if (oldOwner >= oldCount)
                    throw new InvalidDataException("A leaf references an owner outside the block owner table.");
                int newOwner = remap[oldOwner];
                bit = i * newBits;
                if (newBits != 0) result[newOwnerStart + (bit >> 3)] |= (byte)(newOwner << (bit & 7));
            }
            Array.Copy(data, colorStart, result, newOwnerStart + newOwnerBytes, colorBytes);
            CompressedBlock migrated = new CompressedBlock(result);
            migrated.EncodingProfile = target.Snapshot();
            return migrated;
        }

        private static int OwnerIndexBits(int count) {
            if (count == 1) return 0;
            if (count == 2) return 1;
            if (count <= 4) return 2;
            if (count <= 16) return 4;
            return 8;
        }

		public virtual bool Equals( MapBlock other ) {
			return this.ToString( true ) == other.ToString( true );
		}

		public virtual string ToString( bool deep ) {
			if ( !deep ) return ToString();

			return WalkTree( new MapBlockHandling.Stringizer() ).ToString();
		}


		#region Leaf Walking stuff
		public ITreeWalker WalkTree( ITreeWalker walker ) {
			walker.WalkTree( this );
			return walker;
		}

		public ITreeWalker WalkTree( ITreeWalker walker, TreeWalkerMode mode ) {
			walker.WalkTree( this, mode );
			return walker;
		}

		public ITreeWalker WalkTree( DelegateWalker.VisitDelegate visitor ) {
			DelegateWalker walker = new DelegateWalker( visitor );
			walker.WalkTree( this );
			return walker;
		}

		public ITreeWalker WalkTree( DelegateWalker.VisitDelegate visitor, int stopAtLevel ) {
			DelegateWalker walker = new DelegateWalker( visitor );
			walker.StopAtLevel = stopAtLevel;
			walker.WalkTree( this );
			return walker;
		}

		public ITreeWalker WalkTree( DelegateWalker.VisitDelegate visitor, TreeWalkerMode mode ) {
			DelegateWalker walker = new DelegateWalker( visitor );
			walker.WalkTree( this, mode );
			return walker;
		}

		public ITreeWalker WalkTree( DelegateWalker.VisitDelegate visitor, bool visitBranches, TreeWalkerMode mode ) {
			DelegateWalker walker = new DelegateWalker( visitor, visitBranches, mode );
			walker.WalkTree( this );
			return walker;
		}

		public ITreeWalker WalkTree( DelegateWalker.VisitDelegate visitor, bool visitBranches ) {
			DelegateWalker walker = new DelegateWalker( visitor, visitBranches );
			walker.WalkTree( this );
			return walker;
		}

		#endregion

		#region Decompression

		/// <summary>
		/// Decompresses the idtable stored in the byte stream.
		/// </summary>
		/// <param name="data">The byte stream containing the compressed table. </param>
		/// <param name="index"></param>
		private static int DecompressIdTable( byte[] data, ref int index, AdjacencyTable adjacent, MapProfile profile, out Pixel[] owners ) {

			// -- Get a list of province ids
			int idCount = -1;
			ushort[] idTable = new ushort[IdTableSize];
			do {
				++idCount;
				idTable[idCount] = (ushort)(data[index] + ((data[index+1] & 127) << 8));
				index += 2;
			} while ( data[index-1] < Terminator );
			++idCount;

			// -- Fill the ownertable
			owners = new Pixel[IdTableSize];
			for ( int i=0; i<idCount; ++i ) {
				if ( idTable[i] < profile.ProvinceIDThreshold ) {
					owners[i] = new Pixel( 0, idTable[i] );
				}
				else {
					// We're dealing with a pixel that's on a border. Decode some more...
					int val = idTable[i];
					int color = val&1;
					ushort id1 = (ushort)idTable[((val>>9)&63)-4];
					if ( id1 >= profile.ProvinceIDThreshold ) id1 = Province.TerraIncognitaID;
					//ushort id2 = adjacent == null ? Province.TerraIncognitaID : adjacent.GetAdjacency( id1, (val>>1)&15 ).ID;
					ushort id2 = Province.TerraIncognitaID;
					byte riveradj = (byte)((val>>5)&15);
					if ( riveradj != Adjacent.Invalid && adjacent != null ) {
						id2 = id1;
						id1 = adjacent.GetAdjacency(id2, riveradj + (profile.IsFtg ? adjacent.RiverStart(id2) : 0)).ID;
					}

					owners[i] = new Pixel( 0, id1, id2, (byte)(color+1) );
				}
			}

			return idCount;
		}

		private static Node DecompressTree( byte[] data, ref int index ) {
			// Copy the tree into a bit array
			// We know there can be at most 43 bytes of data (1024 pixels or 668 nodes), so we copy those
			// first and resize it to the proper size later.
			int srcsize = index+45 < data.Length ? 45 : data.Length-index;
			byte[] srcdata = new byte[srcsize];
			Array.Copy( data, index, srcdata, 0, srcsize );
			BitArray srctree = new BitArray( srcdata );
			srcdata = null;

			int nodeIndex = 0;
			Node tree = new Node(true);
			ConvertBitTree( 5, srctree, ref nodeIndex, tree );
			index += (int)((nodeIndex+7) / 8); // Set proper data index
			//index += (int)(nodeIndex / 8); // Set proper data index

			return tree;
		}

		private void DecompressOwnership( byte[] data, ref int index, Pixel[] leafs, int leafcount, Pixel[] owners, int ownercount ) {
			// Read the ownership table
			if ( ownercount == 1 ) {
				for ( int i=0; i<leafcount; ++i ) leafs[i] = owners[0];
				return;
			}

			if ( ownercount == 2 ) {
				for ( int i=0; i<leafcount; ++index ) {
					leafs[i++] = owners[(data[index] >> 0) & 1];
					leafs[i++] = owners[(data[index] >> 1) & 1];
					leafs[i++] = owners[(data[index] >> 2) & 1];
					leafs[i++] = owners[(data[index] >> 3) & 1];
					leafs[i++] = owners[(data[index] >> 4) & 1];
					leafs[i++] = owners[(data[index] >> 5) & 1];
					leafs[i++] = owners[(data[index] >> 6) & 1];
					leafs[i++] = owners[(data[index] >> 7) & 1];
				}
				return;
			}

			if ( ownercount <= 4 ) {
				for ( int i=0; i<leafcount; ++index ) {
					leafs[i++] = owners[(data[index] >> 0) & 3];
					leafs[i++] = owners[(data[index] >> 2) & 3];
					leafs[i++] = owners[(data[index] >> 4) & 3];
					leafs[i++] = owners[(data[index] >> 6) & 3];
				}
				return;
			}

			if ( ownercount <= 16 ) {
				for ( int i=0; i<leafcount; index++ ) {
					leafs[i++] = owners[(data[index] >> 0) & 15];
					leafs[i++] = owners[(data[index] >> 4) & 15];
				}
				return;
			}

			for ( int i=0; i<leafcount; i++, ++index )
				leafs[i] = owners[data[index]];
		}

		private void DecompressColorInfo( byte[] data, ref int index, Pixel[] leafs, int leafcount ) {
			for ( int i=0; i<leafcount; ) {
				leafs[i++].Color = (byte)(data[index] & 63);
				if ( i>=leafcount ) break;
				leafs[i++].Color = (byte)((data[index] >> 6 | data[index+1] << 2) & 63);
				if ( i>=leafcount ) break;
				leafs[i++].Color = (byte)((data[index+1] >> 4 | data[index+2] << 4) & 63);
				if ( i>=leafcount ) break;
				leafs[i++].Color = (byte)(data[index+2] >> 2);
				index += 3;
			}
		}

		private static void ConvertBitTree( int level, BitArray srctree, ref int nodeIndex, Node parent ) {
			// Get flag, and advance node pointer
			bool flag = srctree[nodeIndex++];

			if ( flag && level > 1 ) {
				parent.CreateBranches();

				ConvertBitTree( level - 1, srctree, ref nodeIndex, parent.BottomRightChild );
				ConvertBitTree( level - 1, srctree, ref nodeIndex, parent.BottomLeftChild );
				ConvertBitTree( level - 1, srctree, ref nodeIndex, parent.TopRightChild );
				ConvertBitTree( level - 1, srctree, ref nodeIndex, parent.TopLeftChild );
			}
			else {
				if ( flag ) {
					parent.CreateLeaves();
				}
				else {
					parent.BecomeLeaf();
				}
			}
		}

		private void PopulateNode( Node node, Pixel[] leafs, ref int index ) {
			if ( !node.IsLeaf() ) {
				PopulateNode( node.BottomRightChild, leafs, ref index );
				PopulateNode( node.BottomLeftChild, leafs, ref index );
				PopulateNode( node.TopRightChild, leafs, ref index );
				PopulateNode( node.TopLeftChild, leafs, ref index );
			}
			else {
				node.Data = leafs[index++];
			}
		}
		#endregion

		public static MapBlock Combine( MapBlock bottomright, MapBlock bottomleft, MapBlock topright, MapBlock topleft ) {
			// Merge 4 trees
			Node root = new Node( false, bottomright.Tree.Level+1 );
			root.BottomRightChild = bottomright.Tree.Clone();
			root.BottomLeftChild = bottomleft.Tree.Clone();
			root.TopRightChild = topright.Tree.Clone();
			root.TopLeftChild = topleft.Tree.Clone();

			// Use for new block
			MapBlock result = new MapBlock( root );

			// Clip the tree
			result.WalkTree( new MapBlockHandling.TreeClipper() );
			result.tree.Level = 5;

			// Optimise the tee
			result.WalkTree( new MapBlockHandling.TreeOptimiser( true ) );

			return result;
		}

		public static MapBlock Combine( MapBlock[,] matrix ) {
			Node root = new Node( false );

			// Create 4 children
			root.BottomRightChild = new Node( false );
			root.BottomLeftChild = new Node( false );
			root.TopRightChild = new Node( false );
			root.TopLeftChild  = new Node( false );

			// Add items to children
			root.BottomRightChild.BottomRightChild = matrix[3,3].Tree.Clone();
			root.BottomRightChild.BottomLeftChild = matrix[2,3].Tree.Clone();
			root.BottomRightChild.TopRightChild = matrix[3,2].Tree.Clone();
			root.BottomRightChild.TopLeftChild = matrix[2,2].Tree.Clone();

			root.BottomLeftChild.BottomRightChild = matrix[1,3].Tree.Clone();
			root.BottomLeftChild.BottomLeftChild = matrix[0,3].Tree.Clone();
			root.BottomLeftChild.TopRightChild = matrix[1,2].Tree.Clone();
			root.BottomLeftChild.TopLeftChild = matrix[0,2].Tree.Clone();

			root.TopRightChild.BottomRightChild = matrix[3,1].Tree.Clone();
			root.TopRightChild.BottomLeftChild = matrix[2,1].Tree.Clone();
			root.TopRightChild.TopRightChild = matrix[3,0].Tree.Clone();
			root.TopRightChild.TopLeftChild = matrix[2,0].Tree.Clone();

			root.TopLeftChild.BottomRightChild = matrix[1,1].Tree.Clone();
			root.TopLeftChild.BottomLeftChild = matrix[0,1].Tree.Clone();
			root.TopLeftChild.TopRightChild = matrix[1,0].Tree.Clone();
			root.TopLeftChild.TopLeftChild = matrix[0,0].Tree.Clone();

			root.Level = matrix[3,3].Tree.Level+2;

			// Use for new block
			MapBlock result = new MapBlock( root );

			// Clip the tree
			result.WalkTree( new MapBlockHandling.TreeClipperDeep( ) );
			result.tree.Level = 5;

			// Optimise the tee
			result.WalkTree( new MapBlockHandling.TreeOptimiser( true ) );

			return result;
		}

		#region Private Fields
		private Node tree;
		#endregion
	}
}
