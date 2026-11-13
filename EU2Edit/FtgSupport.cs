using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using EU2.Data;
using EU2.IO;
using EU2.Map;
using EU2.Map.Drawing;
namespace EU2.Edit {
    public static class FtgSupport {
        public static MapProfile CreateProfile(Install install) {
            bool ftg=System.IO.File.Exists(install.GetInputDBFile(Path.Combine("Map","provinces.txt")));
            int count=2020;
            string definitions=install.GetInputDBFile("defines.txt");
            if(ftg && System.IO.File.Exists(definitions)) {
                string text=Encoding.GetEncoding(1252).GetString(System.IO.File.ReadAllBytes(definitions));
                Match match=Regex.Match(text,@"(?m)^\s*_MAP_MAX_PROVINCES_\s*=\s*(\d+)");
                if(match.Success) count=int.Parse(match.Groups[1].Value);
            }
            string boxes=install.GetInputMapFile("boundbox.tbl");
            if(ftg && count==2020 && System.IO.File.Exists(boxes)) count=(int)(new FileInfo(boxes).Length/16);
            return new MapProfile(count,ftg);
        }
        public static ProvinceList ReadProvinces(Install install,MapProfile profile) {
            string native=install.GetInputDBFile(Path.Combine("Map","provinces.txt"));
            if(profile.IsFtg && System.IO.File.Exists(native))
                using(FileStream stream=System.IO.File.OpenRead(native)) return ProvinceList.FromFtg(stream,profile);
            using(FileStream stream=System.IO.File.OpenRead(install.GetInputDBFile("province.csv")))
                return new ProvinceList(new CSVReader(stream),profile);
        }
        public static ColorScales ReadColorScales(Install install) {
            string csv=install.GetInputMapFile("colorscales.csv");
            if(System.IO.File.Exists(csv))
                using(FileStream stream=System.IO.File.OpenRead(csv)) return new ColorScales(new CSVReader(stream));
            ColorScales result;
            using(Stream stream=typeof(FtgSupport).Assembly.GetManifestResourceStream("EU2.Edit.DefaultColorScales"))
                result=new ColorScales(new CSVReader(stream));
            foreach(string category in new string[] { "common", "terrains" }) {
                string path=install.GetInputDBFile(Path.Combine("Map","Colorscales","Glory","colorscales_"+category+".txt"));
                if(!System.IO.File.Exists(path)) continue;
                string text=Encoding.GetEncoding(1252).GetString(System.IO.File.ReadAllBytes(path));
                foreach(Match block in Regex.Matches(text,@"(?m)^\s*(\w+)\s*=\s*\{((?:[^{}]|\{[^{}]*\})*)\}")) {
                    MapColor color;
                    if(!Enum.TryParse<MapColor>(block.Groups[1].Value,true,out color)) continue;
                    MatchCollection points=Regex.Matches(block.Groups[2].Value,@"color\s*=\s*\{\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\}");
                    if(points.Count!=4) continue;
                    RGBI[] values=new RGBI[4];
                    for(int i=0;i<4;i++) values[i]=new RGBI(int.Parse(points[i].Groups[4].Value),int.Parse(points[i].Groups[1].Value),int.Parse(points[i].Groups[2].Value),int.Parse(points[i].Groups[3].Value));
                    result.Scales[(int)color]=new ColorScale(block.Groups[1].Value,values);
                }
            }
            return result;
        }
        public static byte[] ReadDefines(Install install) {
            string path=install.GetInputDBFile("defines.txt");
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null;
        }
        public static byte[] UpdateDefines(byte[] bytes,int count) {
            if(bytes==null) throw new InvalidOperationException("FTG export requires Db/defines.txt.");
            Encoding encoding=Encoding.GetEncoding(1252);
            string text=encoding.GetString(bytes);
            Regex expression=new Regex(@"(?m)^(\s*_MAP_MAX_PROVINCES_\s*=\s*)\d+");
            if(!expression.IsMatch(text)) {
                Match section=Regex.Match(text,@"(?m)^\s*province\s*=\s*\{");
                int brace=section.Success ? section.Index+section.Length-1 : -1;
                if(brace<0) text += "\r\nprovince = { _MAP_MAX_PROVINCES_ = " + count + " }\r\n";
                else text=text.Insert(brace+1,"\r\n\t_MAP_MAX_PROVINCES_ = " + count + "\r\n");
            } else text=expression.Replace(text,delegate(Match match) { return match.Groups[1].Value+count; });
            return encoding.GetBytes(text);
        }
    }
    internal sealed class BinaryData : IStreamWriteable {
        internal readonly byte[] Bytes;
        internal BinaryData(byte[] bytes) { Bytes=bytes; }
        public void WriteTo(Stream stream) { stream.Write(Bytes,0,Bytes.Length); }
    }
}
