using System;
using System.Collections;
using System.IO;
using System.Xml;
using EU2.IO;
using EU2.Enums;
using System.Collections.Generic;

namespace EU2.Data
{
	/// <summary>
	/// Summary description for TerrainList.
	/// </summary>
	public class ProvinceList : IEnumerable<Province>, IStreamWriteable {
        private Province[] list;
        private readonly EU2.Map.MapProfile profile;
        private FtgProvinceDocument ftgDocument;
        private readonly HashSet<ushort> defined = new HashSet<ushort>();
        public EU2.Map.MapProfile Profile { get { return profile; } }
        public bool IsFtg { get { return ftgDocument != null; } }
        public int ProvinceCount { get { return profile.ProvinceCount; } }

        public ProvinceList() : this(new EU2.Map.MapProfile()) { }
        public ProvinceList(EU2.Map.MapProfile profile) {
            this.profile = profile;
            list = new Province[0];
            EnsureStorage();
        }
        public ProvinceList(CSVReader reader) : this() { ReadFrom(reader); }
        public ProvinceList(CSVReader reader, EU2.Map.MapProfile profile) : this(profile) { ReadFrom(reader); }

        private void EnsureStorage() {
            if (list.Length == profile.InternalCount) return;
            int start = Math.Max(0,list.Length-2);
            Array.Resize(ref list, profile.InternalCount);
            for (int i=start;i<list.Length;i++) list[i] = new Province((ushort)i);
            list[profile.BorderID].Terrain = Terrain.Border;
            list[profile.BorderOutlineID].Terrain = Terrain.Border;
        }
        public static ProvinceList FromFtg(Stream stream, EU2.Map.MapProfile profile) {
            FtgProvinceDocument document = FtgProvinceDocument.Read(stream);
            profile.IsFtg = true;
            profile.EnsureProvinceCount(document.MaximumID+1);
            profile.SetDefinedProvinceCount(document.MaximumID+1);
            ProvinceList result = new ProvinceList(profile);
            result.ftgDocument = document;
            foreach (Province province in document.Provinces) result[province.ID] = province;
            return result;
        }
        public IEnumerable<Province> DefinedProvinces {
            get { foreach (ushort id in new SortedSet<ushort>(defined)) yield return list[id]; }
        }
        public Province this[int id] {
            get { return GetProvince(id); }
            set {
                if (value == null || value.ID != id || id < 0) throw new InvalidOperationException();
                profile.RegisterProvinceID(id);
                EnsureStorage(); list[id] = value; defined.Add((ushort)id);
            }
        }
        public Province GetProvince(ushort id) { return GetProvince((int)id); }
        public Province GetProvince(int id) { EnsureStorage(); return id >= 0 && id < list.Length ? list[id] : null; }
        public bool Contains(ushort id) { return Contains((int)id); }
        public bool Contains(int id) { return id >= 0 && id < profile.InternalCount && GetProvince(id) != null; }
        public bool Contains(Province province) { return province != null && Contains(province.ID); }

		public bool ReadFrom( CSVReader reader ) {
			// Skip first row
			string header = reader.ReadRow();
			bool newStyle = header.IndexOf( "ToT-SPA;ToT-POR;HRE;" ) >= 0;

			reader.SkipRow();
			for ( int i=0; i<profile.ProvinceCount; ++i ) {
				Province item = new Province();
				if ( !item.ReadFromCSV( reader, newStyle ) ) break;
				this[item.ID] = item;
			}

			return true;
		}

		public void WriteTo( CSVWriter writer ) {
			// Write top row
			if ( GlobalConfig.AllowTOTAndHREInProvinceList )
				writer.EndRow( "Id;Name;PIW name;Religion;Culture;SizeModifier;Pashas;Climate;Ice?;Storm?;Galleys;Manpower;Income;Terrain;ToT-SPA;ToT-POR;HRE;Mine(1) ?;MineValue;Goods;Uprgadable;CoT Historical Modifier;Difficulty for Colonization;Native Combat Strength;Ferocity;Efficiency of Natives in combat;Negotiation Value for Trading Posts;Natives Tolerance value ;City XPos;City YPos;Army XPos;ArmyYPos;PortXPos;Port YPos;Manufactory XPos; Manufactory YPos;Port/sea Adjacency;Terrain x;Terrain Y;Terrain variant;Terrain x;Terrain Y;Terrain variant;Terrain x;Terrain Y;Terrain variant;Terrain x;Terrain Y;Terrain variant;Area;Region;Continent;;Extra River Desc Link 1;Extra River Desc Link 2;Extra River Desc Link 3;;Fill coord X;Fill coord Y;;;;;;;;;;;;;;;;" );
			else
				writer.EndRow( "Id;Name;PIW name;Religion;Culture;SizeModifier;Pashas;Climate;Ice?;Storm?;Galleys;Manpower;Income;Terrain;Mine(1) ?;MineValue;Goods;Uprgadable;CoT Historical Modifier;Difficulty for Colonization;Native Combat Strength;Ferocity;Efficiency of Natives in combat;Negotiation Value for Trading Posts;Natives Tolerance value ;City XPos;City YPos;Army XPos;ArmyYPos;PortXPos;Port YPos;Manufactory XPos; Manufactory YPos;Port/sea Adjacency;Terrain x;Terrain Y;Terrain variant;Terrain x;Terrain Y;Terrain variant;Terrain x;Terrain Y;Terrain variant;Terrain x;Terrain Y;Terrain variant;Area;Region;Continent;;Extra River Desc Link 1;Extra River Desc Link 2;Extra River Desc Link 3;;Fill coord X;Fill coord Y;;;;;;;;;;;;;;;;" );

			for ( ushort i=0; i<profile.ProvinceCount; ++i ) {
				if ( list[i] == null ) list[i] = new Province( i, "Dummy" );

				list[i].WriteToCSV( writer );
			}
			Province.Filler.WriteToCSV( writer );
		}

		public bool ReadFrom( XmlReader reader ) {
			reader.Read();
			if ( reader.NodeType != XmlNodeType.XmlDeclaration ) throw new XmlException( "There is no XML declaration." );

			reader.Read();
			if ( reader.NodeType != XmlNodeType.Element || reader.Name != "Provinces" ) throw new XmlException( "Expected <Provinces> node, but got <" + reader.Name + ">." );

			while ( reader.Read() ) {
				if ( reader.NodeType == XmlNodeType.Element ) {
					if ( reader.Name != "Province" ) throw new XmlException( "Expected 'Province' node, but got <" + reader.Name + ">" );

					Province item = new Province();
					if ( !item.ReadFrom( reader ) ) break;
					this[item.ID] = item;
				}
			}

			return true;
		}

		public bool ReadFrom( XmlDocument doc ) {
			foreach ( XmlNode node in doc.GetElementsByTagName( "Province" ) ) {
				Province item = new Province();
				if ( !item.ReadFrom( node ) ) break;
				this[item.ID] = item;
			}

			return true;
		}

		public void WriteTo( XmlWriter writer ) {
			writer.WriteStartDocument();
			writer.WriteStartElement("Provinces");

			for ( ushort i=0; i<profile.ProvinceCount; ++i ) {
				if ( list[i] == null ) list[i] = new Province( i, "Dummy" );
				list[i].WriteTo( writer );
			}
			Province.Filler.WriteTo( writer );

			writer.WriteEndElement();
			writer.WriteEndDocument();
			writer.Flush();
		}

		public void WriteTo( Stream stream ) {
			if (ftgDocument != null) { ftgDocument.WriteTo(stream, DefinedProvinces); return; }
            CSVWriter writer = new CSVWriter( stream );
			WriteTo( writer );
			writer.Flush();
		}

		public Province TerraIncognita {
			get { return list[0]; }
			set {
				if ( value.ID != 0 ) throw new InvalidOperationException();
				list[0] = value;
			}
		}


        #region IEnumerable<Province> Members

        public IEnumerator<Province> GetEnumerator() {
            return new List<Province>(list).GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return list.GetEnumerator();
        }

        #endregion
    }
}
