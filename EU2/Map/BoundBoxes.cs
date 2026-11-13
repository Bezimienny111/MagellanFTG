using System;
using System.Drawing;
using System.Collections;
using System.IO;
using EU2.Data;

namespace EU2.Map
{
	/// <summary>
	/// Summary description for Boundboxes.
	/// </summary>
	public class BoundBoxes : EU2.IO.IStreamWriteable
	{
        private ProvinceBoundBox[] boxes;
        private MapProfile profile;
        public MapProfile Profile { get { return profile; } set { profile=value; EnsureStorage(); } }
        public BoundBoxes() : this(new MapProfile()) { }
        public BoundBoxes(MapProfile profile) { this.profile=profile; boxes=new ProvinceBoundBox[0]; EnsureStorage(); }
        public BoundBoxes(ProvinceBoundBox[] originals) : this(new MapProfile(originals.Length-2,false)) { originals.CopyTo(boxes,0); }
        public BoundBoxes(ProvinceBoundBox[] originals,MapProfile profile) : this(profile) { Array.Copy(originals,boxes,Math.Min(originals.Length,boxes.Length)); }
        public BoundBoxes(BinaryReader reader) : this(reader,new MapProfile()) { }
        public BoundBoxes(BinaryReader reader,MapProfile profile) : this(profile) { ReadFrom(reader); }
        private void EnsureStorage() {
            int old=boxes.Length; Array.Resize(ref boxes,profile.InternalCount);
            for(int i=old;i<boxes.Length;i++) boxes[i]=new ProvinceBoundBox(Rectangle.Empty,(ushort)i);
        }
        public void ReadFrom(BinaryReader reader) {
            EnsureStorage();
            for(int i=0;i<profile.ProvinceCount;i++) boxes[i].ReadFrom(reader);
        }
        public void WriteTo(BinaryWriter writer) {
            EnsureStorage();
            for(int i=0;i<profile.ProvinceCount;i++) boxes[i].WriteTo(writer);
        }

		public void WriteTo( Stream stream ) {
			WriteTo( new BinaryWriter( stream ) );
		}

		public ProvinceBoundBox this[int index] {
			get {
				if ( index < 0 || index >= profile.InternalCount ) throw new ArgumentOutOfRangeException();
				EnsureStorage(); return boxes[index];
			}
			set {
				if ( index < 0 || index >= profile.InternalCount ) throw new ArgumentOutOfRangeException();
				EnsureStorage(); boxes[index] = value;
			}
		}

		public ProvinceBoundBox[] Boxes {
			get { return boxes; }
		}

		public Rectangle[] GetBoxes( ushort[] ids ) {
			Rectangle[] result = new Rectangle[ids.Length];
			for ( int i=0; i<result.Length; ++i ) {
				result[i] = boxes[ids[i]].Box;
			}

			return result;
		}

		public ProvinceBoundBox[] GetAllIntersectingWith( Rectangle rect  ) {
			ArrayList resultList = new ArrayList();

			for ( int i=0; i<boxes.Length; ++i ) {
				if ( rect.IntersectsWith( boxes[i].Box ) ) resultList.Add( boxes[i] );
			}

			ProvinceBoundBox[] result = new ProvinceBoundBox[resultList.Count];
			resultList.CopyTo( result );
			return result;
		}

		public ProvinceBoundBox[] GetAllIntersectingWith( Point point ) {
			return GetAllIntersectingWith( new Rectangle( point, new Size( 1, 1 ) ) );
		}

		public ProvinceBoundBox[] GetAllIntersectingWith( ProvinceBoundBox box ) {
			return GetAllIntersectingWith( box.Box );
		}

		public ProvinceBoundBox[] GetAllContainedIn( Rectangle rect ) {
			ArrayList resultList = new ArrayList();

			for ( int i=0; i<boxes.Length; ++i ) {
				if ( rect.Contains( boxes[i].Box ) ) resultList.Add( boxes[i] );
			}

			ProvinceBoundBox[] result = new ProvinceBoundBox[resultList.Count];
			resultList.CopyTo( result );
			return result;
		}
	}
}
