using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EU2.Data;
using EU2.Edit;
using EU2.Map;
using EU2.Map.Codec;
using ICSharpCode.SharpZipLib.Zip;
using MapFile = EU2.Edit.File;
using DiskFile = System.IO.File;

public static class FtgIntegrationTests {
    public static readonly string Source = Environment.GetEnvironmentVariable("MAGELLAN_FTG_MOD") ?? @"D:\Steam\steamapps\common\For The Glory\Mods\Gloriana_WatK4";
    private static int passed;
    private static string repository, output;
    private static byte[] originalProvinces;
    private static MapFile project;
    private static readonly Dictionary<string,string> sourceHashes = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

    public static int Run(string repositoryPath, string outputDirectory) {
        repository=Path.GetFullPath(repositoryPath);
        output=Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(output);
        if (output.Equals(Source, StringComparison.OrdinalIgnoreCase) || output.StartsWith(Source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("FTG tests must write outside the source mod.");
        passed=0;
        sourceHashes.Clear();
        string provincePath=Path.Combine(Source,"Db","Map","provinces.txt");
        originalProvinces=DiskFile.ReadAllBytes(provincePath);
        sourceHashes.Add(provincePath,Hash(originalProvinces));
        foreach(string file in Directory.GetFiles(Path.Combine(Source,"Map"),"*.tbl"))
            sourceHashes.Add(file,Hash(DiskFile.ReadAllBytes(file)));
        try {
            RunCheck("FTG provinces.txt parser preserves the real WatK4 bytes", ParserRoundTrip);
            RunCheck("WatK4 extraction reloads three lightmaps with its FTG profile", ExtractWatK4);
            RunCheck("FTG project/native export preserves provinces.txt and defines", ExportNative);
            RunCheck("FTG province ID 6000 and nested edits survive a text roundtrip", HighProvinceID);
            RunCheck("FTG parser rejects duplicate IDs, bad bounds and malformed quotes", InvalidDocuments);
            RunCheck("WatK4 river adjacency uses the correct per-province offset", RiverOffset);
            RunCheck("WatK4 exports a real map tile to a layered PSD", ExportPsd);
            return passed+1;
        }
        finally {
            foreach(KeyValuePair<string,string> file in sourceHashes)
                Check(file.Value==Hash(DiskFile.ReadAllBytes(file.Key)), "Source mod was modified: " + file.Key);
            Console.WriteLine("PASS FTG tests leave provinces.txt and all source .tbl files unchanged");
        }
    }

    private static void RunCheck(string name,Action action) {
        Stopwatch timer=Stopwatch.StartNew();
        action(); passed++;
        Console.WriteLine("PASS " + name + " (" + timer.ElapsedMilliseconds + " ms)");
    }
    private static void Check(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
    private static string Hash(byte[] bytes) { using(SHA256 hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-",""); }
    private static byte[] WriteDocument(FtgProvinceDocument document,IEnumerable<Province> provinces) {
        using(MemoryStream stream=new MemoryStream()) { document.WriteTo(stream,provinces); return stream.ToArray(); }
    }
    private static FtgProvinceDocument ReadDocument(string text) {
        return FtgProvinceDocument.Read(new MemoryStream(new UTF8Encoding(false).GetBytes(text)));
    }

    private static void ParserRoundTrip() {
        FtgProvinceDocument document=FtgProvinceDocument.Read(new MemoryStream(originalProvinces));
        Check(document.Count==2020 && document.MaximumID==2019,"Unexpected real WatK4 province inventory.");
        Check(Hash(originalProvinces)==Hash(WriteDocument(document,document.Provinces)),"Unedited FTG parser roundtrip changed bytes.");
    }

    private static void ExtractWatK4() {
        string archive=Path.Combine(output,"watk4.eu2map");
        EU2.Install install=EU2.Install.FromPath(Source);
        string colorFile=install.GetInputMapFile("colorscales.csv");
        Check(FtgSupport.ReadColorScales(install)!=null,"FTG terrain/common colorscales could not be loaded.");
        MapFile.Extract(install,archive,new FileExtractOptions(3,false,false));
        project=MapFile.FromFile(archive);
        Check(project.Profile.IsFtg,"FTG profile was lost during project serialization.");
        Check(project.Profile.ProvinceCount==2020,"WatK4 native province capacity should be 2020.");
        Check(project.Profile.ProvinceIDThreshold==2020,"WatK4 codec ID threshold should be max defined ID + 1 = 2020.");
        Check(project.Provinces.IsFtg,"Province database fell back to legacy CSV.");
        Check(project.Lightmap1!=null && project.Lightmap2!=null && project.Lightmap3!=null,"A WatK4 lightmap was dropped.");
        Check(project.ColorScales!=null,"Resolved colorscales were not loaded.");
    }

    private static void ExportNative() {
        bool sawText=false,sawCsv=false;
        byte[] archivedText=null;
        using(FileStream file=DiskFile.OpenRead(Path.Combine(output,"watk4.eu2map")))
        using(ZipInputStream zip=new ZipInputStream(file)) {
            ZipEntry entry;
            while((entry=zip.GetNextEntry())!=null) {
                if(entry.Name.Equals("province.csv",StringComparison.OrdinalIgnoreCase))sawCsv=true;
                if(entry.Name.Equals("provinces.txt",StringComparison.OrdinalIgnoreCase)) {
                    sawText=true;
                    using(MemoryStream bytes=new MemoryStream()) { zip.CopyTo(bytes); archivedText=bytes.ToArray(); }
                }
            }
        }
        Check(sawText && !sawCsv,"FTG project must contain provinces.txt instead of province.csv.");
        Check(Hash(archivedText)==Hash(originalProvinces),"The FTG archive changed provinces.txt.");
        string destination=Path.Combine(output,"nativeFtg");
        project.Inject(EU2.Install.FromPath(destination));
        string textPath=Path.Combine(destination,"Db","Map","provinces.txt");
        Check(DiskFile.Exists(textPath),"Native FTG export did not create Db/Map/provinces.txt.");
        Check(!DiskFile.Exists(Path.Combine(destination,"Db","province.csv")),"Native FTG export created a legacy province.csv.");
        Check(Hash(originalProvinces)==Hash(DiskFile.ReadAllBytes(textPath)),"Native FTG export changed unedited province data.");
        string defines=DiskFile.ReadAllText(Path.Combine(destination,"Db","defines.txt"));
        Check(Regex.IsMatch(defines,@"(?m)^\s*_MAP_MAX_PROVINCES_\s*=\s*2020\b"),"Native FTG defines do not specify 2020 province slots.");
    }

    private static void HighProvinceID() {
        const string input="# preserved\nprovince = { id = 1 terrain = plains size_modifier = 0.25 natives = { maxnatives = 99 } history = { custom = yes } }\n";
        FtgProvinceDocument document=ReadDocument(input);
        List<Province> provinces=new List<Province>(document.Provinces);
        Province original=provinces[0];
        original.CityPosition=new Point(123,456);
        original.NativeFerocity=1.25f;
        Province added=new Province(6000,"Added high province");
        added.Terrain=EU2.Enums.Terrain.River;
        added.ArmyPosition=new Point(4097,5123);
        provinces.Add(added);
        byte[] serialized=WriteDocument(document,provinces);
        FtgProvinceDocument reread=FtgProvinceDocument.Read(new MemoryStream(serialized));
        Check(reread.MaximumID==6000 && reread.Count==2,"FTG parser lost province ID 6000.");
        List<Province> roundtrip=new List<Province>(reread.Provinces);
        Check(roundtrip[0].CityPosition==original.CityPosition && roundtrip[0].NativeFerocity==1.25f,"Nested FTG edits did not survive.");
        Check(roundtrip[1].ID==6000 && roundtrip[1].ArmyPosition==added.ArmyPosition && roundtrip[1].IsRiver(),"High-ID FTG properties did not survive.");
        string text=Encoding.UTF8.GetString(serialized);
        Check(text.Contains("size_modifier = 0.25") && text.Contains("maxnatives = 99") && text.Contains("history = { custom = yes }"),"Unmodeled FTG data was dropped while editing.");
        Pixel pixel=new Pixel(17,6000,6001,2);
        Check(pixel.ID==6000 && pixel.RiverID==6001,"Pixel structure truncated a 16-bit province or river ID.");
    }

    private static void InvalidDocuments() {
        string[] invalid={
            "province = { id = 1 } province = { id = 1 }",
            "province = { id = -1 }",
            "province = { id = 65533 }",
            "province = { id = 1 name = \"unterminated }",
            "province = { id = 1"
        };
        foreach(string text in invalid) {
            bool rejected=false;
            try { ReadDocument(text); } catch(FormatException) { rejected=true; }
            Check(rejected,"Malformed FTG input was accepted: " + text);
        }
    }

    private static void RiverOffset() {
        Check(project.AdjacencyTable.RiverStart(27)==4,"WatK4 province 27 river list must start at adjacency index 4.");
        Check(project.AdjacencyTable[27].Length>4 && project.AdjacencyTable[27][4].ID==2006,"WatK4 province 27 river adjacency index 4 must resolve to province 2006.");
        Check(project.Provinces[2006].IsRiver(),"WatK4 province 2006 is not classified as a river.");
    }

    private static void ExportPsd() {
        string configuration=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Name;
        string executable=Path.Combine(repository,"artifacts","bin",configuration,"mimage.exe");
        Check(DiskFile.Exists(executable),"Build/package mimage.exe before running the FTG integration tests: " + executable);
        Point city=project.Provinces[27].CityPosition;
        int x=Math.Max(0,Math.Min(Lightmap.BaseWidth-128,city.X/32*32));
        int y=Math.Max(0,Math.Min(Lightmap.BaseHeight-128,city.Y/32*32));
        string psd=Path.Combine(output,"watk4-tile.psd");
        string arguments="/E " + Quote(Path.Combine(output,"watk4.eu2map")) + " " + Quote(psd) + " /R:" + x + "," + y + ",128,128 /O";
        ProcessStartInfo info=new ProcessStartInfo(executable,arguments);
        info.UseShellExecute=false; info.CreateNoWindow=true;
        info.RedirectStandardOutput=true; info.RedirectStandardError=true;
        using(Process process=Process.Start(info)) {
            var stdout=process.StandardOutput.ReadToEndAsync();
            var stderr=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(60000)) { process.Kill(); throw new InvalidOperationException("WatK4 PSD export timed out."); }
            string log=stdout.Result+stderr.Result;
            DiskFile.WriteAllText(Path.Combine(output,"watk4-psd-export.log"),log);
            Check(process.ExitCode==0 && DiskFile.Exists(psd),"WatK4 PSD export failed: " + log);
        }
        byte[] header=DiskFile.ReadAllBytes(psd);
        Check(header.Length>100 && Encoding.ASCII.GetString(header,0,4)=="8BPS","WatK4 export did not create a Photoshop document.");
    }
    private static string Quote(string value) { return "\"" + value + "\""; }
}
