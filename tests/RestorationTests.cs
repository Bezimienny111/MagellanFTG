using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using EU2.Data;
using EU2.Edit;
using EU2.IO;
using EU2.Map;
using EU2.Map.Codec;
using EU2.Map.Drawing;
using MapFile = EU2.Edit.File;
using DiskFile = System.IO.File;

class RestorationTests {
    static string output, repository;
    static MapFile fixture, loaded;
    static int passed;

    [STAThread]
    static int Main(string[] args) {
        try {
            if (args.Length > 0 && args[0] == "--benchmark") { Benchmark(); return 0; }
            if (args.Length > 0 && args[0] == "--border-markers") { BorderMarkers(); return 0; }
            repository = Path.GetFullPath(args[0]);
            output = Path.Combine(repository, "artifacts", "tests", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(output);
            Run("project save/reload preserves all map tables", RoundTrip);
            Run("native injection and IFile game-directory adapter", NativeRoundTrip);
            Run("extractor preserves auxiliary map tables", Extraction);
            Run("CLI PSD export and simulated import", ImageRoundTrip);
            Run("WinForms controls and Explorer construct without vendor DLLs", Controls);
            Run("unsupported map dimensions are rejected", UnsupportedSize);
            Run("truncated block fails instead of hanging", TruncatedBlock);
            Run("failed project save preserves the previous file", FailedSave);
            Run("256 block owners survive compression; overflow is rejected", CodecOwners);
            BorderMarkers();
            passed += FtgIntegrationTests.Run(repository,output);
            Run("WatK4 river PSD regeneration and minject preserve province capacity", RiverImportAndInject);
            Run("WatK4 migration to 10000 preserves every decoded block", HighProvinceMigration);
            Run("province ID 9999 survives native and PSD roundtrip", HighProvincePainting);
            Run("mprovince TXT and mextract FTG command-line workflow", FtgCommandLine);
            DiskFile.WriteAllText(Path.Combine(output,"test-success.txt"),"All checks passed.");
            Console.WriteLine("PASS: " + passed + " integration checks. Artifacts: " + output);
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    static void Run(string name, Action action) {
        Stopwatch timer = Stopwatch.StartNew(); action(); passed++;
        Console.WriteLine("PASS " + name + " (" + timer.ElapsedMilliseconds + " ms)");
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static string Hash(byte[] data) { using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", ""); }
    static byte[] Bytes(IStreamWriteable value) { using (MemoryStream stream = new MemoryStream()) { value.WriteTo(stream); return stream.ToArray(); } }

    static void RoundTrip() {
        fixture = MapFile.CreateEmpty(MapSize.Default);
        fixture.Provinces[7].Name = "Restored Province Seven";
        fixture.Provinces[7].CityPosition = new Point(12, 15);
        fixture.Provinces[7].Terrain = EU2.Enums.Terrain.Plains;
        fixture.Provinces[8].Terrain = EU2.Enums.Terrain.Plains;
        fixture.AdjacencyTable.Add(7, new Adjacent(8, AdjacencyType.Normal));
        fixture.AdjacencyTable.Add(8, new Adjacent(7, AdjacencyType.Normal));
        fixture.BoundBoxes = new BoundBoxes();
        fixture.BoundBoxes[7] = new ProvinceBoundBox(new Rectangle(0, 0, 16, 32), 7);
        fixture.IDGrid = new IDGrid();
        fixture.IncognitaGrid = new IncognitaGrid();
        IDWeight[] weights = new IDWeight[4];
        weights[0].ID = 7; weights[0].Weight = 181;
        fixture.IncognitaGrid[0, 0] = new IncognitaGridItem(weights);
        using (FileStream stream = DiskFile.OpenRead(Path.Combine(repository, "Maptools", "MapExplorer", "Resources", "colorscales.csv")))
            fixture.ColorScales = new ColorScales(new CSVReader(stream));
        RawImage image = new RawImage(0, new Rectangle(0, 0, 32, 32));
        ushort[] ids = new ushort[1024];
        for (int y=0; y<32; y++) for (int x=0; x<32; x++) {
            ushort id = (ushort)(x < 16 ? 7 : 8);
            image.Memory[x,y] = new Pixel(24, id); ids[y*32+x] = id;
        }
        fixture.IDMap.ImportBitmapBuffer(image.Bounds, ids);
        fixture.Lightmap1.EncodeImage(image);
        string path = Path.Combine(output, "fixture.eu2map");
        fixture.WriteTo(path);
        loaded = MapFile.FromFile(path);
        Check(loaded.Provinces[7].Name == fixture.Provinces[7].Name, "Province name changed");
        Check(loaded.Provinces[7].CityPosition == new Point(12,15), "City position changed");
        Check(loaded.IDMap[3,4] == 7 && loaded.IDMap[24,4] == 8, "Province ownership changed");
        CompareTables(fixture, loaded);
        RawImage decoded = loaded.Lightmap1.DecodeImage(new Rectangle(0,0,32,32));
        Check(decoded[3,4].ID == 7 && decoded[24,4].ID == 8, "Lightmap ownership changed");
    }

    static void CompareTables(MapFile expected, MapFile actual) {
        Check(actual.IDGrid != null && actual.IncognitaGrid != null && actual.BoundBoxes != null, "An auxiliary table was dropped");
        Check(Hash(Bytes(expected.IDMap)) == Hash(Bytes(actual.IDMap)), "id.tbl changed");
        Check(Hash(Bytes(expected.AdjacencyTable)) == Hash(Bytes(actual.AdjacencyTable)), "adjacent.tbl changed");
        Check(Hash(Bytes(expected.BoundBoxes)) == Hash(Bytes(actual.BoundBoxes)), "boundbox.tbl changed");
        Check(Hash(Bytes(expected.IDGrid)) == Hash(Bytes(actual.IDGrid)), "idgrid.tbl changed");
        Check(Hash(Bytes(expected.IncognitaGrid)) == Hash(Bytes(actual.IncognitaGrid)), "incgrid.tbl changed");
    }

    static void NativeRoundTrip() {
        string native = Path.Combine(output, "native");
        Directory.CreateDirectory(Path.Combine(native, "db"));
        Directory.CreateDirectory(Path.Combine(native, "map"));
        loaded.Inject(EU2.Install.FromPath(native));
        IFile proxy = new EU2Proxy();
        proxy.ReadFrom(native);
        Check(proxy.Lightmap1 != null && proxy.Provinces[7].Name == loaded.Provinces[7].Name, "Directory adapter did not load the map");
        CompareTables(loaded, (MapFile)proxy);
    }

    static void Extraction() {
        string path = Path.Combine(output, "extracted.eu2map");
        MapFile.Extract(EU2.Install.FromPath(Path.Combine(output,"native")), path, new FileExtractOptions(1,false,false));
        CompareTables(loaded, MapFile.FromFile(path));
    }

    static void ImageRoundTrip() {
        string path = Path.Combine(output,"fixture.eu2map"), psd = Path.Combine(output,"tile.psd");
        string export = Tool("mimage.exe", "/E " + Quote(path) + " " + Quote(psd) + " /R:0,0,32,32 /O");
        Check(DiskFile.Exists(psd) && new FileInfo(psd).Length > 100, "PSD was not produced: " + export);
        string before = Hash(DiskFile.ReadAllBytes(path));
        string import = Tool("mimage.exe", "/SI " + Quote(psd) + " " + Quote(path) + " /G:0");
        Check(import.IndexOf("tested successfully", StringComparison.OrdinalIgnoreCase) >= 0, "Simulated import failed: " + import);
        Check(before == Hash(DiskFile.ReadAllBytes(path)), "Simulated import modified the map");
        string edited = Path.Combine(output,"edited.eu2map");
        DiskFile.Copy(path,edited);
        string actualImport = Tool("mimage.exe", "/I " + Quote(psd) + " " + Quote(edited) + " /G:0");
        CompareTables(loaded, MapFile.FromFile(edited));
    }

    static string Quote(string value) { return "\"" + value + "\""; }

    static void BorderMarkers() {
        Run("border markers never become river or land neighbours", MarkerAdjacency);
        Run("255 real IDs fit beside current, stale and outline markers", MarkerGrid);
        Run("defined high IDs remain valid in grids and adjacency", HighIDAdjacency);
        Run("256 real IDs overflow; forced build leaves the previous region intact", GridOverflow);
        Run("empty and marker-only regions contain only TI", EmptyGrid);
    }

    static IDMap TestIDMap(MapProfile profile, int[] pixels, int width, int height) {
        IDMap map=new IDMap(); map.Profile=profile;
        map.ImportBitmapBuffer(100,100,width,height,pixels);
        return map;
    }
    static void MarkerAdjacency() {
        foreach(bool river in new bool[] { false,true }) foreach(int capacity in new int[] {2020,2021}) {
            MapProfile profile=new MapProfile(capacity,true); profile.SetDefinedProvinceCount(2020);
            ProvinceList provinces=new ProvinceList(profile);
            provinces[2001].Terrain=river ? EU2.Enums.Terrain.River : EU2.Enums.Terrain.Plains;
            int[] pixels=new int[64*64];
            for(int y=0;y<64;y++) for(int x=0;x<64;x++) pixels[y*64+x]=x<32 ? 2001 : 2020;
            IDMap map=TestIDMap(profile,pixels,64,64);
            AdjacencyTable table=map.BuildAdjacencyTable(new Rectangle(100,100,64,64),provinces);
            Check(!table.IsAdjacent(2001,2020),"Border marker became a neighbour");
            Check(profile.ProvinceCount==capacity && profile.ProvinceIDThreshold==2020,"Adjacency changed the map profile");
        }
    }
    static IDMap DenseIDMap(MapProfile profile,int count,params int[] markers) {
        int[] pixels=new int[count+markers.Length];
        for(int i=0;i<count;i++) pixels[i]=i+1;
        Array.Copy(markers,0,pixels,count,markers.Length);
        return TestIDMap(profile,pixels,pixels.Length,1);
    }
    static void MarkerGrid() {
        foreach(int capacity in new int[] {2020,2021,10000}) {
            MapProfile profile=new MapProfile(capacity,true); profile.SetDefinedProvinceCount(2020);
            IDMap map=DenseIDMap(profile,255,2020,2021);
            IDGrid grid=new IDGrid(); grid.BuildRegion(0,0,map);
            ushort[] ids=grid.GetIDs(0,0);
            for(int i=0;i<256;i++) Check(ids[i]==i,"Grid lost a real ID or retained a marker");
            Check(profile.ProvinceCount==capacity,"Grid changed capacity");
        }
    }
    static void HighIDAdjacency() {
        MapProfile profile=new MapProfile(10000,true);
        ProvinceList provinces=new ProvinceList(profile);
        provinces[2020].Terrain=EU2.Enums.Terrain.Plains;
        provinces[9999].Terrain=EU2.Enums.Terrain.River;
        int[] pixels=new int[64*64];
        for(int y=0;y<64;y++) for(int x=0;x<64;x++) pixels[y*64+x]=x<32 ? 2020 : 9999;
        IDMap map=TestIDMap(profile,pixels,64,64);
        AdjacencyTable table=map.BuildAdjacencyTable(new Rectangle(100,100,64,64),provinces);
        Check(table.IsAdjacent(2020,9999) && table.IsAdjacent(9999,2020),"High-ID river adjacency was removed");
        IDGrid grid=new IDGrid(); grid.BuildRegion(0,0,map);
        Check(grid.IsInRegion(2020,0,0) && grid.IsInRegion(9999,0,0),"Defined high IDs were filtered from grid");
    }
    static void GridOverflow() {
        MapProfile profile=new MapProfile(2020,true);
        IDGrid grid=new IDGrid(); grid.BuildRegion(0,0,DenseIDMap(profile,255));
        string before=Hash(Bytes(grid));
        IDMap overflow=DenseIDMap(profile,256,2020);
        bool rejected=false;
        try { grid.BuildRegion(0,0,overflow); } catch(EU2.RegionSizeOverflowException) { rejected=true; }
        Check(rejected,"256 real IDs were accepted");
        Check(before==Hash(Bytes(grid)),"Failed build modified the region");
        grid.BuildRegion(0,0,overflow,true);
        Check(before==Hash(Bytes(grid)),"Forced overflow modified the region");
    }
    static void EmptyGrid() {
        MapProfile profile=new MapProfile(2020,true);
        foreach(int marker in new int[] {0,2020}) {
            IDGrid grid=new IDGrid(); grid.BuildRegion(0,0,DenseIDMap(profile,1));
            grid.BuildRegion(0,0,TestIDMap(profile,new int[] {marker},1,1));
            foreach(ushort id in grid.GetIDs(0,0)) Check(id==0,"Empty region retained an ID");
        }
    }

    static void RiverImportAndInject() {
        MapFile map=MapFile.FromFile(Path.Combine(output,"watk4.eu2map"));
        Rectangle bounds=map.BoundBoxes[2001].Box;
        Point river=Point.Empty;
        bool found=false;
        for(int y=Math.Max(0,bounds.Top); y<Math.Min(Lightmap.BaseHeight,bounds.Bottom) && !found; y++) {
            ushort[] row=map.IDMap.ExportBitmapBuffer(0,y,Lightmap.BaseWidth,1);
            for(int x=0;x<row.Length;x++) if(row[x]==2001) { river=new Point(x,y); found=true; break; }
        }
        Check(found,"WatK4 river 2001 is absent from ID map");
        Rectangle tile=new Rectangle((river.X/32)*32,(river.Y/32)*32,32,32);
        string path=Path.Combine(output,"watk4-river.eu2map"), psd=Path.Combine(output,"watk4-river.psd");
        map.WriteTo(path);
        Tool("mimage.exe","/E "+Quote(path)+" "+Quote(psd)+" /R:"+tile.X+","+tile.Y+",32,32 /O");
        string import=Tool("mimage.exe","/I "+Quote(psd)+" "+Quote(path)+" /G:2");
        Check(import.Contains("Import successful"),"PSD regeneration did not finish: "+import);
        MapFile restored=MapFile.FromFile(path);
        Check(restored.Profile.ProvinceCount==2020 && restored.Profile.ProvinceIDThreshold==2020,"River PSD import changed capacity");
        Check(!restored.AdjacencyTable.IsAdjacent(2001,2020),"River import stored a border neighbour");
        string native=Path.Combine(output,"native-river"); Directory.CreateDirectory(native);
        string inject=Tool("minject.exe",Quote(path)+" /D:"+Quote(native));
        Check(inject.Contains("Injection successful"),"Native injection did not finish: "+inject);
        IFile direct=new EU2Proxy(); direct.ReadFrom(native);
        Check(((MapFile)direct).Profile.ProvinceCount==2020,"Native export changed capacity");
        Check(direct.IDMap[river.X,river.Y]==restored.IDMap[river.X,river.Y],"Native river ownership changed");
        string defines=DiskFile.ReadAllText(Path.Combine(native,"Db","defines.txt"));
        Check(System.Text.RegularExpressions.Regex.IsMatch(defines,@"(?m)^\s*_MAP_MAX_PROVINCES_\s*=\s*2020\b"),"Native capacity define changed");
    }

    static string Tool(string executable, string arguments) {
        string bin = Path.Combine(repository,"artifacts","bin", "Debug");
        string configuration = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Name;
        bin = Path.Combine(repository,"artifacts","bin", configuration);
        ProcessStartInfo info = new ProcessStartInfo(Path.Combine(bin,executable), arguments);
        info.UseShellExecute = false; info.CreateNoWindow = true;
        info.RedirectStandardOutput = true; info.RedirectStandardError = true;
        using (Process process = Process.Start(info)) {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(); throw new Exception("Tool timed out: " + executable); }
            string result = stdout.Result + stderr.Result;
            Check(process.ExitCode == 0, executable + " failed: " + result);
            DiskFile.WriteAllText(Path.Combine(output, executable + "-" + Guid.NewGuid().ToString("N") + ".log"), result);
            return result;
        }
    }

    static void Controls() {
        using (EU2MapEditorControls.MapView view = new EU2MapEditorControls.MapView()) { view.CreateControl(); }
        using (EU2MapEditorControls.LayeredMapView view = new EU2MapEditorControls.LayeredMapView()) {
            view.Size = new Size(320,240); view.CreateControl();
            using (Bitmap image = new Bitmap(320,240)) view.DrawToBitmap(image,new Rectangle(0,0,320,240));
        }
        using (MapExplorer.MainForm explorer = new MapExplorer.MainForm()) explorer.CreateControl();
    }
    static void UnsupportedSize() {
        try { new MapFile(new MapSize(1024,1024)); } catch (NotSupportedException) { return; }
        throw new Exception("Unsupported map dimensions were silently accepted");
    }
    static void TruncatedBlock() {
        try { new CompressedBlock(0,10).ReadFrom(new BinaryReader(new MemoryStream(new byte[3]))); }
        catch (EndOfStreamException) { return; }
        throw new Exception("Truncated block was accepted");
    }
    static void FailedSave() {
        string path = Path.Combine(output,"fixture.eu2map");
        string before = Hash(DiskFile.ReadAllBytes(path));
        FileProcessingHandler fail = delegate(IFile f, FileProcessingEventArgs e) { throw new IOException("Simulated write failure"); };
        loaded.FileProcessing += fail;
        bool failed=false;
        try { loaded.WriteTo(path); } catch (IOException) { failed=true; }
        finally { loaded.FileProcessing -= fail; }
        Check(failed && Hash(DiskFile.ReadAllBytes(path)) == before, "Failed save destroyed the original file");
    }
    static Node OwnerTree(int level, ref int sequence, int owners) {
        if (level == 0) return new Node(new Pixel((byte)(sequence % 64),(ushort)(1 + sequence++ % owners)),0);
        Node node = new Node(false,level);
        node.BottomRightChild=OwnerTree(level-1,ref sequence,owners);
        node.BottomLeftChild=OwnerTree(level-1,ref sequence,owners);
        node.TopRightChild=OwnerTree(level-1,ref sequence,owners);
        node.TopLeftChild=OwnerTree(level-1,ref sequence,owners);
        return node;
    }
    static void CodecOwners() {
        int sequence=0;
        MapBlock source = new MapBlock(OwnerTree(5,ref sequence,256));
        CompressedBlock compressed=source.Compress(0,0,0,null,null,null);
        Check(MapBlock.GetRawIDTable(compressed).Length == 256, "256-owner table was corrupted");
        Check(source.Equals(new MapBlock(compressed,null)), "256-owner block did not survive decompression");
        sequence=0;
        try { new MapBlock(OwnerTree(5,ref sequence,257)).Compress(0,0,0,null,null,null); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Block with more than 256 owners was not rejected");
    }
    static void WriteNode(BinaryWriter writer,Node node) {
        writer.Write((byte)node.Level); writer.Write(node.IsBranch());
        writer.Write(node.Data.Color); writer.Write(node.Data.Border); writer.Write(node.Data.ID); writer.Write(node.Data.RiverID);
        if(node.IsBranch()) { WriteNode(writer,node.BottomRightChild); WriteNode(writer,node.BottomLeftChild); WriteNode(writer,node.TopRightChild); WriteNode(writer,node.TopLeftChild); }
    }
    static string LightmapFingerprint(Lightmap map) {
        using(SHA256 hash=SHA256.Create()) using(MemoryStream buffer=new MemoryStream(32768)) using(BinaryWriter writer=new BinaryWriter(buffer)) {
            for(int i=0;i<map.BlockCount;i++) {
                buffer.SetLength(0); WriteNode(writer,map.GetDecompressedBlock(i).Tree); writer.Flush();
                byte[] bytes=buffer.GetBuffer(); hash.TransformBlock(bytes,0,(int)buffer.Length,bytes,0);
            }
            hash.TransformFinalBlock(new byte[0],0,0); return BitConverter.ToString(hash.Hash);
        }
    }
    static string highPath;
    static void HighProvinceMigration() {
        string path=Path.Combine(output,"watk4.eu2map");
        if(!DiskFile.Exists(path)) throw new Exception("WatK4 extraction fixture missing");
        MapFile map=MapFile.FromFile(path);
        string[] before={LightmapFingerprint(map.Lightmap1),LightmapFingerprint(map.Lightmap2),LightmapFingerprint(map.Lightmap3)};
        Province extra=new Province(9999,"PROV_Magellan_Test_9999");
        extra.Terrain=EU2.Enums.Terrain.Plains;
        map.Provinces[9999]=extra;
        Check(map.Profile.ProvinceCount==10000 && map.Profile.ProvinceIDThreshold==10000,"High ID did not expand the map profile");
        highPath=Path.Combine(output,"watk4-10000.eu2map");
        map.WriteTo(highPath);
        MapFile restored=MapFile.FromFile(highPath);
        Check(restored.Profile.ProvinceCount==10000 && restored.Profile.ProvinceIDThreshold==10000,"10000-slot map profile did not survive saving");
        Check(before[0]==LightmapFingerprint(restored.Lightmap1),"Lightmap1 changed during ID-range migration");
        Check(before[1]==LightmapFingerprint(restored.Lightmap2),"Lightmap2 changed during ID-range migration");
        Check(before[2]==LightmapFingerprint(restored.Lightmap3),"Lightmap3 changed during ID-range migration");
        DiskFile.WriteAllLines(Path.Combine(output,"watk4-migration-fingerprints.txt"),before);
        string native=Path.Combine(output,"nativeFtg10000");
        restored.Inject(EU2.Install.FromPath(native));
        Check(new FileInfo(Path.Combine(native,"Map","boundbox.tbl")).Length==10000*16,"boundbox.tbl has the wrong province capacity");
        IFile direct=new EU2Proxy(); direct.ReadFrom(native);
        Check(((MapFile)direct).Profile.ProvinceCount==10000 && direct.Provinces[9999].Name==extra.Name,"Native FTG map did not reload ID9999");
    }
    static void HighProvincePainting() {
        MapFile map=MapFile.FromFile(highPath);
        Rectangle tile=new Rectangle(64,64,32,32);
        RawImage image=new RawImage(0,tile);
        ushort[] ids=new ushort[32*32];
        for(int y=0;y<32;y++) for(int x=0;x<32;x++) { ids[y*32+x]=9999; image.Memory[x,y]=new Pixel(30,9999); }
        map.IDMap.ImportBitmapBuffer(tile,ids); map.Lightmap1.EncodeImage(image);
        map.BoundBoxes[9999]=new ProvinceBoundBox(tile,9999);
        string painted=Path.Combine(output,"watk4-id9999.eu2map");
        map.WriteTo(painted);
        MapFile restored=MapFile.FromFile(painted);
        Check(restored.IDMap[70,70]==9999,"id.tbl truncated province9999");
        Check(restored.Lightmap1.DecodeImage(tile)[6,6].ID==9999,"lightmap truncated province9999");
        string psd=Path.Combine(output,"watk4-id9999.psd");
        Tool("mimage.exe","/E " + Quote(painted) + " " + Quote(psd) + " /R:64,64,32,32 /O");
        string result=Tool("mimage.exe","/I " + Quote(psd) + " " + Quote(painted) + " /G:0");
        restored=MapFile.FromFile(painted);
        Check(restored.IDMap[70,70]==9999 && restored.Lightmap1.DecodeImage(tile)[6,6].ID==9999,"PSD roundtrip truncated ID9999");
        int[] oldIndices={1,2};
        restored.Lightmap1.Recompress(oldIndices);
        restored.WriteTo(Path.Combine(output,"watk4-id9999-recompressed.eu2map"));
    }
    static void FtgCommandLine() {
        string metadata=Path.Combine(output,"exported-provinces.txt");
        Tool("mprovince.exe","/E " + Quote(highPath) + " " + Quote(metadata) + " /TXT /O");
        MapFile map=MapFile.FromFile(highPath);
        Check(Hash(Bytes(map.Provinces))==Hash(DiskFile.ReadAllBytes(metadata)),"mprovince /TXT export changed the province document");
        string copy=Path.Combine(output,"metadata-import.eu2map"); DiskFile.Copy(highPath,copy);
        Tool("mprovince.exe","/I " + Quote(metadata) + " " + Quote(copy) + " /TXT");
        Check(MapFile.FromFile(copy).Provinces[9999].Name==map.Provinces[9999].Name,"mprovince /TXT import lost province9999");
        string extraction=Path.Combine(output,"cli-watk4.eu2map");
        Tool("mextract.exe",Quote(extraction) + " /D:" + Quote(FtgIntegrationTests.Source) + " /3");
        Check(MapFile.FromFile(extraction).Provinces.IsFtg,"mextract CLI did not select FTG provinces.txt");
        string color=Tool("mcolor.exe","/ID:9999 /C:FTG16");
        Check(color.Contains("270F"),"FTG color lookup for province9999 is incorrect");
    }
    static void Benchmark() {
        MapBlock[] blocks = new MapBlock[32];
        for(int i=0;i<blocks.Length;i++) blocks[i]=new MapBlock(new Node(new Pixel((byte)(i+1),(ushort)(1700+i))));
        using(MemoryStream stream=new MemoryStream()) {
            for(int i=0;i<100;i++) blocks[i%32].Compress(0,0,0,null,null,null);
            GC.Collect(); int collections=GC.CollectionCount(0);
            Stopwatch watch=Stopwatch.StartNew();
            for(int i=0;i<12000;i++) { byte[] bytes=blocks[i%32].Compress(0,0,0,null,null,null).Data; stream.Write(bytes,0,bytes.Length); }
            watch.Stop();
            Console.WriteLine("blocks=12000 milliseconds=" + watch.ElapsedMilliseconds + " gen0_collections=" + (GC.CollectionCount(0)-collections) + " sha256=" + Hash(stream.ToArray()));
        }
    }
}
