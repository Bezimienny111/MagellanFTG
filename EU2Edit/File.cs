using System;
using System.IO;
using EU2.Data;
using EU2.IO;
using EU2.Map;
using EU2.Map.Drawing;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip;

namespace EU2.Edit
{
	public class File : IFile
	{
		public const int LightmapCount = 3;

		private ProvinceList provinces;

		private Lightmap[] lightmap;

		private IDMap idmap;

		private AdjacencyTable adjacent;

		private ColorScales colorscales;

		private BoundBoxes boundboxes;

		private IDGrid idgrid;

		private IncognitaGrid incgrid;

		private MapSize mapSize;
        private MapProfile profile = new MapProfile();
        private byte[] nativeDefines;
        public MapProfile Profile { get { return profile; } }
        public void EnsureProvinceCount(int count) { profile.EnsureProvinceCount(count); SyncProfile(); }
        private void SyncProfile() {
            if(provinces!=null) profile=provinces.Profile;
            if(idmap!=null) idmap.Profile=profile;
            if(adjacent!=null) { adjacent.Profile=profile; adjacent.Provinces=provinces; }
            if(boundboxes!=null) boundboxes.Profile=profile;
            if(idgrid!=null) idgrid.Profile=profile;
            if(lightmap!=null) foreach(Lightmap map in lightmap) if(map!=null) { map.Profile=profile; map.Attach(provinces,adjacent,idmap); }
        }


		public MapSize MapSize
		{
			get
			{
				return mapSize;
			}
		}

		public ProvinceList Provinces
		{
			get
			{
				return provinces;
			}
			set
			{
				provinces = value; SyncProfile();
			}
		}

		public IDMap IDMap
		{
			get
			{
				return idmap;
			}
			set
			{
				idmap = value; SyncProfile();
			}
		}

		public IDGrid IDGrid
		{
			get
			{
				return idgrid;
			}
			set
			{
				idgrid = value; SyncProfile();
			}
		}

		public IncognitaGrid IncognitaGrid
		{
			get
			{
				return incgrid;
			}
			set
			{
				incgrid = value;
			}
		}

		public AdjacencyTable AdjacencyTable
		{
			get
			{
				return adjacent;
			}
			set
			{
				adjacent = value; SyncProfile();
			}
		}

		public ColorScales ColorScales
		{
			get
			{
				return colorscales;
			}
			set
			{
				colorscales = value;
			}
		}

		public BoundBoxes BoundBoxes
		{
			get
			{
				return boundboxes;
			}
			set
			{
				boundboxes = value; SyncProfile();
			}
		}

		public Lightmap Lightmap1
		{
			get
			{
				return lightmap[0];
			}
			set
			{
				lightmap[0] = value; SyncProfile();
			}
		}

		public Lightmap Lightmap2
		{
			get
			{
				return lightmap[1];
			}
			set
			{
				lightmap[1] = value; SyncProfile();
			}
		}

		public Lightmap Lightmap3
		{
			get
			{
				return lightmap[2];
			}
			set
			{
				lightmap[2] = value; SyncProfile();
			}
		}

		public event FileProcessingHandler FileProcessing;

		public static File CreateEmpty(MapSize size)
		{
			File file = new File(size);
			file.Lightmap1 = Lightmap.CreateEmpty(0);
			file.Provinces = new ProvinceList();
			file.AdjacencyTable = new AdjacencyTable();
			file.IDMap = new IDMap();
			return file;
		}

		public static File FromFile(string path)
		{
			File file = new File(MapSize.Default);
			file.ReadFrom(path);
			return file;
		}

		public File() : this(MapSize.Default) { }

		public File(MapSize useMapSize)
		{
			provinces = null;
			idmap = null;
			lightmap = new Lightmap[3];
			for (int i = 0; i < 3; i++)
			{
				lightmap[i] = null;
			}
			boundboxes = null;
			idgrid = null;
			incgrid = null;
			if (useMapSize != MapSize.Default)
				throw new NotSupportedException("This restored upstream core supports 18944x7296 maps only.");
			mapSize = useMapSize;
		}

		public void ReadFrom(Stream stream)
		{
			try
			{
				ZipInputStream zipInputStream = new ZipInputStream(stream);
                profile=new MapProfile(); nativeDefines=null;
				idmap = null;
				adjacent = null;
				for (int i = 0; i < 3; i++)
				{
					lightmap[i] = null;
				}
				provinces = null;
				colorscales = null;
				idgrid = null;
				incgrid = null;
				boundboxes = null;
				ZipEntry nextEntry;
				while ((nextEntry = zipInputStream.GetNextEntry()) != null)
				{
					switch (nextEntry.Name.ToLower())
					{
					case "id.tbl":
						idmap = new IDMap(new BinaryReader(zipInputStream));
						break;
					case "adjacent.tbl":
						adjacent = new AdjacencyTable(new BinaryReader(zipInputStream),profile);
						break;
					case "lightmap1.tbl":
						lightmap[0] = Lightmap.FromStream(0, new BinaryReader(zipInputStream));
						break;
					case "lightmap2.tbl":
						lightmap[1] = Lightmap.FromStream(1, new BinaryReader(zipInputStream));
						break;
					case "lightmap3.tbl":
						lightmap[2] = Lightmap.FromStream(2, new BinaryReader(zipInputStream));
						break;
					case "idgrid.tbl":
						idgrid = new IDGrid(new BinaryReader(zipInputStream));
						break;
					case "incgrid.tbl":
						incgrid = new IncognitaGrid(new BinaryReader(zipInputStream));
						break;
					case "boundbox.tbl":
						boundboxes = new BoundBoxes(new BinaryReader(zipInputStream),profile);
						break;
					case "provinces.txt":
                        provinces=ProvinceList.FromFtg(zipInputStream,profile);
                        break;
                    case "ftg-defines.txt":
                        using(MemoryStream bytes=new MemoryStream()) { zipInputStream.CopyTo(bytes); nativeDefines=bytes.ToArray(); }
                        break;
                    case "province.csv":
						provinces = new ProvinceList(new CSVReader(zipInputStream),profile);
						break;
					case "colorscales.csv":
						colorscales = new ColorScales(new CSVReader(zipInputStream));
						break;
					case "config.csv":
						ReadConfig(new CSVReader(zipInputStream));
						break;
					}
				}
				zipInputStream.Close();
			}
			catch (ZipException e)
			{
				throw new InvalidFileFormatException(e);
			}
			SyncProfile();
            for (int j = 0; j < 3; j++)
			{
				if (lightmap[j] != null)
				{
					lightmap[j].Attach(provinces, adjacent, idmap);
				}
			}
		}

		public void WriteTo(Stream stream)
		{
			SyncProfile();
            ZipOutputStream zipOutputStream = new ZipOutputStream(stream);
			zipOutputStream.SetLevel(4);
			WriteConfig(zipOutputStream);
			WriteItem(zipOutputStream, provinces, provinces!=null && provinces.IsFtg ? "provinces.txt" : "province.csv", FileProcessing);
            if(nativeDefines!=null) WriteItem(zipOutputStream,new BinaryData(nativeDefines),"ftg-defines.txt",FileProcessing);
			WriteItem(zipOutputStream, idmap, "id.tbl", FileProcessing);
			WriteItem(zipOutputStream, adjacent, "adjacent.tbl", FileProcessing);
			WriteItem(zipOutputStream, boundboxes, "boundbox.tbl", FileProcessing);
			WriteItem(zipOutputStream, idgrid, "idgrid.tbl", FileProcessing);
			WriteItem(zipOutputStream, incgrid, "incgrid.tbl", FileProcessing);
			for (int i = 0; i < 3; i++)
			{
				WriteItem(zipOutputStream, lightmap[i], "lightmap" + (i + 1) + ".tbl", FileProcessing);
			}
			WriteItem(zipOutputStream, colorscales, "colorscales.csv", FileProcessing);
			zipOutputStream.Finish();
			zipOutputStream.Close();
		}

		public void ReadFrom(string path)
		{
			FileStream fileStream = null;
			try
			{
				fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
				ReadFrom(fileStream);
			}
			finally
			{
				if (fileStream != null)
				{
					fileStream.Close();
				}
			}
		}

        public void WriteTo(string path) {
            WriteAtomically(path, WriteTo);
        }

        private static void WriteAtomically(string path, Action<Stream> write) {
            string destination = Path.GetFullPath(path);
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    write(stream);
                if (System.IO.File.Exists(destination))
                    System.IO.File.Replace(temporary, destination, null);
                else
                    System.IO.File.Move(temporary, destination);
            }
            finally {
                if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary);
            }
        }

		public void Inject(Install target)
		{
			Inject(target, new InjectParams());
		}

		public void Inject(Install target, InjectParams injParams)
        {
            SyncProfile(); profile.ValidateFtgExport();
            Directory.CreateDirectory(target.DBPath); Directory.CreateDirectory(target.MapPath);
            if(profile.IsFtg) Directory.CreateDirectory(Path.Combine(target.DBPath,"Map"));
			using (GlobalConfigChange globalConfigChange = new GlobalConfigChange())
			{
				globalConfigChange.AllowTOTAndHREInProvinceList = !injParams.NoTOTAndHRE;
				WriteFile(provinces, target.GetDBFile(profile.IsFtg ? Path.Combine("Map","provinces.txt") : "province.csv"), FileProcessing);
			}
			WriteFile(idmap, target.GetMapFile("id.tbl"), FileProcessing);
			WriteFile(adjacent, target.GetMapFile("adjacent.tbl"), FileProcessing);
			WriteFile(boundboxes, target.GetMapFile("boundbox.tbl"), FileProcessing);
			WriteFile(idgrid, target.GetMapFile("idgrid.tbl"), FileProcessing);
			WriteFile(incgrid, target.GetMapFile("incgrid.tbl"), FileProcessing);
			for (int i = 0; i < 3; i++)
			{
				WriteFile(lightmap[i], target.GetMapFile("lightmap" + (i + 1) + ".tbl"), FileProcessing);
			}
			if(!profile.IsFtg) WriteFile(colorscales, target.GetMapFile("colorscales.csv"), FileProcessing);
            if(profile.IsFtg) {
                string path=target.GetDBFile("defines.txt");
                byte[] original=System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : nativeDefines;
                WriteFile(new BinaryData(FtgSupport.UpdateDefines(original,profile.ProvinceCount)),path,FileProcessing);
            }
		}

		public static void Extract(Install install, string targetPath)
		{
			Extract(install, targetPath, new FileExtractOptions(), null);
		}

		public static void Extract(Install install, string targetPath, FileExtractOptions options)
		{
			Extract(install, targetPath, options, null);
		}

		public static void Extract(Install install, string targetPath, FileExtractOptions options, FileProcessingHandler callback)
		{
            File file = new File();
            file.profile=FtgSupport.CreateProfile(install);
            file.nativeDefines=FtgSupport.ReadDefines(install);
            file.Provinces=FtgSupport.ReadProvinces(install,file.profile);
            file.AdjacencyTable=new AdjacencyTable(install.GetInputMapFile("adjacent.tbl"),file.profile);
            file.ColorScales=FtgSupport.ReadColorScales(install);
			file.Lightmap1 = Lightmap.FromFile(0, install.GetInputMapFile("lightmap1.tbl"));
			if (options.IncludeLightmaps > 1)
			{
				file.Lightmap2 = Lightmap.FromFile(1, install.GetInputMapFile("lightmap2.tbl"));
				if (options.IncludeLightmaps > 2)
				{
					file.Lightmap3 = Lightmap.FromFile(2, install.GetInputMapFile("lightmap3.tbl"));
				}
			}
			file.IDMap = null;
			IDMap iDMap = new IDMap(install.GetInputMapFile("id.tbl"));
            iDMap.Profile=file.profile;
			if (iDMap.IsOldMap() || options.ForceGenerateIDMap)
			{
				try
				{
					if (callback != null)
					{
						callback(file, new FileProcessingEventArgs("id.tbl", FileProcessingAction.Generate));
					}
					file.Lightmap1.Attach(file.Provinces, file.AdjacencyTable, null);
					file.IDMap = IDMap.Build(file.Lightmap1);
					if (options.FixIdMap)
					{
						if (callback != null)
						{
							callback(file, new FileProcessingEventArgs("id.tbl", FileProcessingAction.Fix));
						}
						for (int i = 0; i < Lightmap.BaseHeight; i++)
						{
							for (int j = 0; j < Lightmap.BaseWidth; j++)
							{
								ushort num = iDMap[j, i];
								ushort num2 = file.IDMap[j, i];
								if (num != 0 && num2 != 2020 && num != num2)
								{
									file.IDMap[j, i] = num;
								}
							}
						}
					}
				}
				catch
				{
					file.IDMap = null;
				}
			}
			if (file.IDMap == null)
			{
				file.IDMap = iDMap;
			}
			file.LoadAuxiliaryTables(install);
			file.FileProcessing = (FileProcessingHandler)Delegate.Combine(file.FileProcessing, callback);
			file.WriteTo(targetPath);
		}

		protected void LoadAuxiliaryTables(Install install) {
            string path = install.GetInputMapFile("boundbox.tbl");
            if (System.IO.File.Exists(path))
                using (BinaryReader reader = new BinaryReader(System.IO.File.OpenRead(path)))
                    boundboxes = new BoundBoxes(reader,profile);
            path = install.GetInputMapFile("idgrid.tbl");
            if (System.IO.File.Exists(path))
                using (BinaryReader reader = new BinaryReader(System.IO.File.OpenRead(path)))
                    idgrid = new IDGrid(reader);
            path = install.GetInputMapFile("incgrid.tbl");
            if (System.IO.File.Exists(path))
                using (BinaryReader reader = new BinaryReader(System.IO.File.OpenRead(path)))
                    incgrid = new IncognitaGrid(reader);
            SyncProfile();
        }

        protected void WriteFile(IStreamWriteable item, string path, FileProcessingHandler callback)
		{
			WriteFile(this, item, path, callback);
		}

        protected static void WriteFile(File file, IStreamWriteable item, string path, FileProcessingHandler callback) {
            if (item == null) return;
            if (callback != null) callback(file, new FileProcessingEventArgs(Path.GetFileName(path), FileProcessingAction.Write));
            WriteAtomically(path, item.WriteTo);
        }

		protected void WriteItem(ZipOutputStream zipstream, IStreamWriteable item, string name, FileProcessingHandler callback)
		{
			if (item != null)
			{
				ZipEntry zipEntry = new ZipEntry(name);
				zipEntry.DateTime = DateTime.Now;
				if (callback != null)
				{
					callback(this, new FileProcessingEventArgs(name, FileProcessingAction.Write));
				}
				zipstream.PutNextEntry(zipEntry);
				item.WriteTo(zipstream);
				zipstream.Flush();
			}
		}

		protected void ReadConfig(CSVReader reader)
		{
			while (!reader.EOF)
			{
				string text = reader.ReadString().ToLower();
				if(text=="provincecount") { profile=new MapProfile(reader.ReadInt(),profile.IsFtg); }
                else if(text=="ftg") { profile.IsFtg=reader.ReadBoolean(); }
                else if(text=="idthreshold") { profile.SetDefinedProvinceCount(reader.ReadInt()); }
                else if (text == "mapsize")
				{
					MapSize declaredSize = new MapSize(reader.ReadSize());
					if (declaredSize != MapSize.Default)
						throw new NotSupportedException("This map size is not supported by the upstream core: " + declaredSize);
					mapSize = declaredSize;
				}
				else
				{
					reader.SkipRow();
				}
			}
		}

		protected void WriteConfig(ZipOutputStream zipstream)
		{
			FileProcessingHandler fileProcessing = FileProcessing;
			if (fileProcessing != null)
			{
				fileProcessing(this, new FileProcessingEventArgs("config.csv", FileProcessingAction.Write));
			}
			ZipEntry zipEntry = new ZipEntry("config.csv");
			zipEntry.DateTime = DateTime.Now;
			zipstream.PutNextEntry(zipEntry);
			CSVWriter cSVWriter = new CSVWriter(zipstream);
			cSVWriter.Write("provincecount"); cSVWriter.Write(profile.ProvinceCount); cSVWriter.EndRow();
            cSVWriter.Write("ftg"); cSVWriter.Write(profile.IsFtg); cSVWriter.EndRow();
            cSVWriter.Write("idthreshold"); cSVWriter.Write(profile.ProvinceIDThreshold); cSVWriter.EndRow();
            cSVWriter.Write("mapsize");
			cSVWriter.Write(mapSize.BaseSize);
			cSVWriter.EndRow();
			cSVWriter.Flush();
			zipstream.Flush();
		}

		private static BorderMap GenerateBorderMap(Install install)
		{
			FileStream fileStream = null;
			try
			{
				fileStream = new FileStream(install.GetMapFile("lightmap1.tbl"), FileMode.Open, FileAccess.Read, FileShare.Read);
				return BorderMap.Build(Lightmap.FromStream(0, new BinaryReader(fileStream)));
			}
			finally
			{
				if (fileStream != null)
				{
					fileStream.Close();
				}
			}
		}
	}
}
