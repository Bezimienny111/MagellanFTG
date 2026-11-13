using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using EU2.Enums;

namespace EU2.Data {
    /// <summary>
    /// A source-preserving FTG province document. Unknown fields and unedited
    /// formatting remain in the original source; projected Province objects are
    /// only an editing view, never the serialization authority for other fields.
    /// </summary>
    public sealed class FtgProvinceDocument {
        private readonly byte[] originalBytes;
        private readonly string source;
        private readonly Encoding encoding;
        private readonly byte[] preamble;
        private readonly string newline;
        private readonly List<Record> records = new List<Record>();
        private readonly Dictionary<ushort, Record> byID = new Dictionary<ushort, Record>();
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly string[] Climates = { "desertic", "tropical", "temperate", "ncontinental", "scontinental", "tundra", "arctic" };

        private sealed class Record {
            internal Node Node;
            internal Province Province;
            internal Dictionary<string, string> Snapshot;
        }
        private sealed class Node {
            internal string Key, Value;
            internal int ValueStart, ValueEnd, CloseStart;
            internal bool IsBlock;
            internal readonly List<Node> Children = new List<Node>();
            internal Node Child(string key) {
                for (int i=Children.Count-1; i>=0; i--)
                    if (string.Equals(Children[i].Key, key, StringComparison.OrdinalIgnoreCase)) return Children[i];
                return null;
            }
        }
        private sealed class Token {
            internal string Text;
            internal int Start, End;
            internal char Kind;
        }
        private sealed class Edit {
            internal int Start, End;
            internal string Text;
            internal Edit(int start, int end, string text) { Start=start; End=end; Text=text; }
        }
        private sealed class Change {
            internal string Value;
            internal readonly SortedDictionary<string, Change> Children = new SortedDictionary<string, Change>(StringComparer.Ordinal);
            internal void Add(string path, string value) {
                Change at=this;
                foreach (string part in path.Split('.')) {
                    Change next;
                    if (!at.Children.TryGetValue(part, out next)) { next=new Change(); at.Children.Add(part,next); }
                    at=next;
                }
                at.Value=value;
            }
        }

        private FtgProvinceDocument(byte[] bytes) {
            originalBytes=bytes;
            int skip=0;
            if (bytes.Length>=3 && bytes[0]==239 && bytes[1]==187 && bytes[2]==191) { encoding=new UTF8Encoding(false,true); skip=3; }
            else if (bytes.Length>=2 && bytes[0]==255 && bytes[1]==254) { encoding=new UnicodeEncoding(false,false,true); skip=2; }
            else if (bytes.Length>=2 && bytes[0]==254 && bytes[1]==255) { encoding=new UnicodeEncoding(true,false,true); skip=2; }
            else {
                bool high=false;
                for (int i=0; i<bytes.Length; i++) if (bytes[i]>=128) { high=true; break; }
                encoding=Encoding.GetEncoding(1252,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
                if (high) {
                    try { new UTF8Encoding(false,true).GetString(bytes); encoding=new UTF8Encoding(false,true); }
                    catch (DecoderFallbackException) { }
                }
            }
            preamble=new byte[skip];
            Array.Copy(bytes,preamble,skip);
            source=encoding.GetString(bytes,skip,bytes.Length-skip);
            newline=source.IndexOf("\r\n",StringComparison.Ordinal)>=0 ? "\r\n" : "\n";
            List<Node> roots=new Parser(source).Read();
            foreach (Node block in roots) {
                if (!string.Equals(block.Key,"province",StringComparison.OrdinalIgnoreCase)) continue;
                if (!block.IsBlock) throw new FormatException("A province entry must contain a block.");
                Node id=block.Child("id");
                int number;
                if (id==null || id.IsBlock || !int.TryParse(id.Value,NumberStyles.Integer,Invariant,out number) || number<0 || number>65532)
                    throw new FormatException("Province IDs must be integers between 0 and 65532.");
                ushort pid=(ushort)number;
                if (byID.ContainsKey(pid)) throw new FormatException("Duplicate province ID " + pid + ".");
                Province province=Project(pid,block);
                Record record=new Record { Node=block, Province=province, Snapshot=Values(province) };
                records.Add(record); byID.Add(pid,record);
                if (number>MaximumID) MaximumID=number;
            }
            if (records.Count==0) throw new FormatException("The document contains no province blocks.");
        }

        public int MaximumID { get; private set; }
        public int Count { get { return records.Count; } }
        public IEnumerable<Province> Provinces {
            get { foreach (Record record in records) yield return record.Province; }
        }
        public static FtgProvinceDocument Read(Stream stream) {
            if (stream==null) throw new ArgumentNullException("stream");
            using (MemoryStream copy=new MemoryStream()) { stream.CopyTo(copy); return new FtgProvinceDocument(copy.ToArray()); }
        }
        public string GetTerrainName(ushort id) {
            Record record;
            if (!byID.TryGetValue(id,out record)) return null;
            Node terrain=record.Node.Child("terrain");
            return terrain==null ? "terra_incognita" : terrain.Value;
        }

        /// <summary>
        /// Classify a mod-defined terrain for rendering without replacing its
        /// original FTG terrain key when the document is saved.
        /// </summary>
        public void SetTerrainClassification(ushort id, EU2.Enums.Terrain terrain) {
            if (terrain == null) throw new ArgumentNullException("terrain");
            Record record;
            if (!byID.TryGetValue(id, out record)) throw new KeyNotFoundException("Province " + id + " is not defined in this document.");
            record.Province.Terrain = terrain;
            record.Snapshot["terrain"] = TerrainName(terrain);
        }
        /// <summary>
        /// Existing entries omitted from updatedProvinces are retained. Include
        /// only explicitly defined/new provinces, not unused capacity fillers.
        /// This method leaves the output stream open.
        /// </summary>
        public void WriteTo(Stream stream, IEnumerable<Province> updatedProvinces) {
            if (stream==null) throw new ArgumentNullException("stream");
            if (updatedProvinces==null) throw new ArgumentNullException("updatedProvinces");
            List<Edit> edits=new List<Edit>();
            SortedDictionary<ushort,Province> additions=new SortedDictionary<ushort,Province>();
            HashSet<ushort> seen=new HashSet<ushort>();
            foreach (Province province in updatedProvinces) {
                if (province==null) continue;
                if (province.ID>65532) throw new ArgumentOutOfRangeException("updatedProvinces","IDs 65533 through 65535 are reserved.");
                if (!seen.Add(province.ID)) throw new ArgumentException("Duplicate updated province ID " + province.ID + ".");
                Record record;
                if (!byID.TryGetValue(province.ID,out record)) { additions.Add(province.ID,province); continue; }
                Change changes=new Change();
                foreach (KeyValuePair<string,string> pair in Values(province)) {
                    string initial;
                    if (!record.Snapshot.TryGetValue(pair.Key,out initial) || pair.Value!=initial) changes.Add(pair.Key,pair.Value);
                }
                if (changes.Children.Count!=0)
                    edits.Add(new Edit(record.Node.ValueStart,record.Node.ValueEnd,RenderBlock(record.Node,changes,0)));
            }
            if (edits.Count==0 && additions.Count==0) { stream.Write(originalBytes,0,originalBytes.Length); return; }
            string text=Apply(source,edits,0);
            StringBuilder result=new StringBuilder(text);
            foreach (KeyValuePair<ushort,Province> pair in additions) {
                Change changes=new Change();
                changes.Add("id",pair.Key.ToString(Invariant));
                Dictionary<string,string> defaults=Values(new Province(pair.Key));
                foreach (KeyValuePair<string,string> value in Values(pair.Value)) {
                    if (value.Key=="name" || value.Key=="terrain" || value.Value!=defaults[value.Key]) changes.Add(value.Key,value.Value);
                }
                result.Append(newline).Append("province = ").Append(RenderNewBlock(changes,0)).Append(newline);
            }
            byte[] content=encoding.GetBytes(result.ToString());
            stream.Write(preamble,0,preamble.Length); stream.Write(content,0,content.Length);
        }

        private string RenderBlock(Node node, Change changes, int depth) {
            List<Edit> edits=new List<Edit>();
            StringBuilder insertion=new StringBuilder();
            foreach (KeyValuePair<string,Change> pair in changes.Children) {
                Node child=node.Child(pair.Key);
                if (child==null) {
                    insertion.Append(Indent(depth+1)).Append(pair.Key).Append(" = ")
                        .Append(pair.Value.Value ?? RenderNewBlock(pair.Value,depth+1)).Append(newline);
                } else {
                    string value=pair.Value.Value;
                    if (value==null) {
                        if (!child.IsBlock) throw new FormatException("Cannot edit nested values inside scalar " + pair.Key + ".");
                        value=RenderBlock(child,pair.Value,depth+1);
                    }
                    edits.Add(new Edit(child.ValueStart,child.ValueEnd,value));
                }
            }
            if (insertion.Length!=0) {
                int line=source.LastIndexOf('\n',Math.Max(node.ValueStart,node.CloseStart-1));
                int insert=line<0 ? node.ValueStart : line+1;
                bool ownLine=true;
                for (int i=insert;i<node.CloseStart;i++) if (!char.IsWhiteSpace(source[i])) { ownLine=false; break; }
                if (!ownLine) { insert=node.CloseStart; insertion.Insert(0,newline); insertion.Append(Indent(depth)); }
                edits.Add(new Edit(insert,insert,insertion.ToString()));
            }
            return Apply(source.Substring(node.ValueStart,node.ValueEnd-node.ValueStart),edits,node.ValueStart);
        }
        private string RenderNewBlock(Change changes,int depth) {
            StringBuilder result=new StringBuilder("{").Append(newline);
            foreach (KeyValuePair<string,Change> pair in changes.Children)
                result.Append(Indent(depth+1)).Append(pair.Key).Append(" = ")
                    .Append(pair.Value.Value ?? RenderNewBlock(pair.Value,depth+1)).Append(newline);
            return result.Append(Indent(depth)).Append('}').ToString();
        }
        private static string Indent(int depth) { return new string('\t',depth); }
        private static string Apply(string text,List<Edit> edits,int origin) {
            edits.Sort(delegate(Edit left,Edit right) { return right.Start.CompareTo(left.Start); });
            StringBuilder result=new StringBuilder(text);
            int boundary=origin+text.Length;
            foreach (Edit edit in edits) {
                if (edit.Start<origin || edit.End>boundary || edit.End<edit.Start)
                    throw new InvalidOperationException("Overlapping or invalid source edits.");
                result.Remove(edit.Start-origin,edit.End-edit.Start).Insert(edit.Start-origin,edit.Text);
                boundary=edit.Start;
            }
            return result.ToString();
        }

        private static string Scalar(Node node,string key,string fallback) {
            Node value=node==null ? null : node.Child(key);
            return value==null || value.IsBlock ? fallback : value.Value;
        }
        private static int Integer(Node node,string key,int fallback) {
            string text=Scalar(node,key,null); if(text==null)return fallback;
            decimal value;
            if(!decimal.TryParse(text,NumberStyles.Float,Invariant,out value) || value<int.MinValue || value>int.MaxValue)
                throw new FormatException("Invalid numeric value for " + key + ": " + text);
            return (int)value;
        }
        private static float Number(Node node,string key,float fallback) {
            string text=Scalar(node,key,null); if(text==null)return fallback;
            float value;
            if(!float.TryParse(text,NumberStyles.Float,Invariant,out value) || float.IsNaN(value) || float.IsInfinity(value))
                throw new FormatException("Invalid numeric value for " + key + ": " + text);
            return value;
        }
        private static int Boolean(Node node,string key,int fallback) {
            string text=Scalar(node,key,null); if(text==null)return fallback;
            if(text.Equals("yes",StringComparison.OrdinalIgnoreCase) || text=="1")return 1;
            if(text.Equals("no",StringComparison.OrdinalIgnoreCase) || text=="0")return 0;
            throw new FormatException("Invalid yes/no value for " + key + ": " + text);
        }
        private static Point Position(Node parent,string key) {
            Node node=parent==null ? null : parent.Child(key);
            return new Point(Integer(node,"x",-1),Integer(node,"y",-1));
        }
        private static Terrain ProjectTerrain(string name) {
            switch((name ?? "terra_incognita").ToLowerInvariant()) {
                case "plains": return Terrain.Plains;
                case "forest": return Terrain.Forest;
                case "mountain": case "mountains": return Terrain.Mountain;
                case "desert": return Terrain.Desert;
                case "marsh": case "swamp": return Terrain.Marsh;
                case "sea": case "ocean": return Terrain.Ocean;
                case "river": return Terrain.River;
                case "terra_incognita": return Terrain.TerraIncognita;
                default:return Terrain.Unknown;
            }
        }
        private static Province Project(ushort id,Node node) {
            Province p=new Province(id,Scalar(node,"name",""));
            p.PIW=PlaceInWorld.FromName(Scalar(node,"type","inland"));
            p.Terrain=ProjectTerrain(Scalar(node,"terrain","terra_incognita"));
            p.Religion=new Religion(Scalar(node,"religion","pagan"));
            p.Culture=new Culture(Scalar(node,"culture","none"));
            p.Goods=new Goods(Scalar(node,"goods","nothing"));
            p.SizeModifier=Integer(node,"size_modifier",0);
            p.Ice=Boolean(node,"ice",0); p.Storm=Boolean(node,"storm",0);
            p.Galleys=Boolean(node,"galleys",0)!=0;
            p.Manpower=Integer(node,"manpower",0); p.Income=Integer(node,"income",0);
            p.MineValue=Integer(node,"value",0); p.Mine=p.MineValue!=0;
            p.COTHistoricalModifier=Integer(node,"cot_modifier",0);
            p.ColonizationDifficulty=Integer(node,"colonization_difficulty",0);
            p.PortAdjacency=Integer(node,"sea_adjacency",0);
            p.Area=Scalar(node,"area",""); p.Region=Scalar(node,"region",""); p.Continent=Scalar(node,"continent","");
            p.DefaultCityName=Scalar(node,"city_name",null);
            string climate=Scalar(node,"climate","arctic");
            p.Climate=Array.IndexOf(Climates,climate.ToLowerInvariant());
            Node natives=node.Child("natives");
            p.NativeCombatStrength=Integer(natives,"combat",0);
            p.NativeFerocity=Number(natives,"ferocity",0);
            p.NativeCombatEfficiency=Number(natives,"efficiency",0);
            p.TradingPostsNegotiation=Integer(natives,"tp_negotiation",0);
            p.NativesTolerance=Integer(natives,"tolerance",0);
            Node gfx=node.Child("gfx");
            p.CityPosition=Position(gfx,"city"); p.ArmyPosition=Position(gfx,"army");
            p.PortPosition=Position(gfx,"port"); p.ManufactoryPosition=Position(gfx,"manufactory");
            p.FillCoordinates=Position(gfx,"center");
            TerrainVariant[] variants=new TerrainVariant[4];
            for(int i=0;i<variants.Length;i++) {
                string key="terrain"+(i+1).ToString(Invariant);
                Node terrain=gfx==null ? null : gfx.Child(key);
                variants[i]=new TerrainVariant(Position(gfx,key),Integer(terrain,"variant",0));
            }
            p.TerrainVariants=variants;
            Node river=gfx==null ? null : gfx.Child("river");
            if(river!=null && river.IsBlock) {
                int[] values=new int[3]; int index=0;
                foreach(Node value in river.Children) {
                    if(index>=values.Length)break;
                    int parsed;
                    if(value.Key==null && int.TryParse(value.Value,NumberStyles.Integer,Invariant,out parsed)) values[index++]=parsed;
                }
                p.ExtraRiverDiscover=values;
            }
            return p;
        }

        private static Dictionary<string,string> Values(Province p) {
            Dictionary<string,string> result=new Dictionary<string,string>(StringComparer.Ordinal);
            result.Add("name",Quote(p.Name ?? ""));
            result.Add("type",Atom(p.PIW==null ? "inland" : p.PIW.Name));
            result.Add("terrain",TerrainName(p.Terrain));
            result.Add("religion",Atom(p.Religion==null ? "pagan" : p.Religion.Name));
            result.Add("culture",Atom(p.Culture==null ? "none" : p.Culture.Name));
            result.Add("goods",Atom(p.Goods==null ? "nothing" : p.Goods.Name));
            result.Add("size_modifier",p.SizeModifier.ToString(Invariant));
            result.Add("climate",p.Climate>=0 && p.Climate<Climates.Length ? Climates[p.Climate] : p.Climate.ToString(Invariant));
            result.Add("ice",p.Ice>0 ? "yes" : "no"); result.Add("storm",p.Storm>0 ? "yes" : "no");
            result.Add("galleys",p.Galleys ? "yes" : "no");
            result.Add("manpower",p.Manpower.ToString(Invariant)); result.Add("income",p.Income.ToString(Invariant));
            result.Add("value",p.MineValue.ToString(Invariant));
            result.Add("cot_modifier",p.COTHistoricalModifier.ToString(Invariant));
            result.Add("colonization_difficulty",p.ColonizationDifficulty.ToString(Invariant));
            result.Add("sea_adjacency",p.PortAdjacency.ToString(Invariant));
            result.Add("area",Quote(p.Area ?? "")); result.Add("region",Quote(p.Region ?? "")); result.Add("continent",Quote(p.Continent ?? ""));
            result.Add("city_name",Quote(p.DefaultCityName ?? "#N/A"));
            result.Add("natives.combat",p.NativeCombatStrength.ToString(Invariant));
            result.Add("natives.ferocity",p.NativeFerocity.ToString("R",Invariant));
            result.Add("natives.efficiency",p.NativeCombatEfficiency.ToString("R",Invariant));
            result.Add("natives.tp_negotiation",p.TradingPostsNegotiation.ToString(Invariant));
            result.Add("natives.tolerance",p.NativesTolerance.ToString(Invariant));
            AddPosition(result,"gfx.city",p.CityPosition); AddPosition(result,"gfx.army",p.ArmyPosition);
            AddPosition(result,"gfx.port",p.PortPosition); AddPosition(result,"gfx.manufactory",p.ManufactoryPosition);
            AddPosition(result,"gfx.center",p.FillCoordinates);
            TerrainVariant[] variants=p.TerrainVariants;
            for(int i=0;i<4;i++) {
                TerrainVariant variant=variants!=null && i<variants.Length ? variants[i] : new TerrainVariant(-1,-1,0);
                string key="gfx.terrain"+(i+1).ToString(Invariant);
                AddPosition(result,key,variant.Location); result.Add(key+".variant",variant.Variant.ToString(Invariant));
            }
            StringBuilder rivers=new StringBuilder("{");
            if(p.ExtraRiverDiscover!=null)foreach(int river in p.ExtraRiverDiscover)if(river!=0)rivers.Append(' ').Append(river.ToString(Invariant));
            result.Add("gfx.river",rivers.Append(" }").ToString());
            return result;
        }
        private static string TerrainName(Terrain terrain) {
            if(terrain==null || terrain==Terrain.Unknown || terrain==Terrain.TerraIncognita)return "terra_incognita";
            return terrain==Terrain.Ocean ? "sea" : Atom(terrain.Name.ToLowerInvariant());
        }
        private static void AddPosition(Dictionary<string,string> result,string key,Point point) {
            result.Add(key+".x",point.X.ToString(Invariant)); result.Add(key+".y",point.Y.ToString(Invariant));
        }
        private static string Quote(string text) { return "\""+text.Replace("\\","\\\\").Replace("\"","\\\"")+"\""; }
        private static string Atom(string text) {
            if(string.IsNullOrEmpty(text))return Quote(text ?? "");
            foreach(char value in text)if(char.IsWhiteSpace(value) || "{}=#\"".IndexOf(value)>=0)return Quote(text);
            return text;
        }

        private sealed class Parser {
            private readonly string text;
            private readonly List<Token> tokens=new List<Token>();
            private int index;
            internal Parser(string source) { text=source; Lex(); }
            internal List<Node> Read() { return Entries(false); }
            private List<Node> Entries(bool nested) {
                List<Node> result=new List<Node>();
                while(index<tokens.Count) {
                    Token first=tokens[index];
                    if(first.Kind=='}') { if(!nested)throw Error("Unexpected closing brace",first.Start); return result; }
                    if(first.Kind=='{') { result.Add(Value(null)); continue; }
                    if(first.Kind=='=')throw Error("Unexpected assignment operator",first.Start);
                    index++;
                    if(index<tokens.Count && tokens[index].Kind=='=') {
                        index++;
                        if(index>=tokens.Count || tokens[index].Kind=='}')throw Error("Missing assigned value",first.Start);
                        result.Add(Value(first.Text));
                    } else result.Add(new Node { Key=null, Value=first.Text, ValueStart=first.Start, ValueEnd=first.End });
                }
                if(nested)throw Error("Unclosed block",text.Length);
                return result;
            }
            private Node Value(string key) {
                Token token=tokens[index++];
                Node result=new Node { Key=key, ValueStart=token.Start, ValueEnd=token.End, Value=token.Text };
                if(token.Kind=='{') {
                    result.IsBlock=true; result.Value=null;
                    result.Children.AddRange(Entries(true));
                    if(index>=tokens.Count || tokens[index].Kind!='}')throw Error("Unclosed block",token.Start);
                    result.CloseStart=tokens[index].Start; result.ValueEnd=tokens[index++].End;
                } else if(token.Kind=='=' || token.Kind=='}')throw Error("Invalid value",token.Start);
                return result;
            }
            private void Lex() {
                int cursor=0;
                while(cursor<text.Length) {
                    char value=text[cursor];
                    if(char.IsWhiteSpace(value) || value=='\ufeff') { cursor++; continue; }
                    if(value=='#') { while(cursor<text.Length && text[cursor]!='\n')cursor++; continue; }
                    int start=cursor++;
                    if(value=='{' || value=='}' || value=='=') { tokens.Add(new Token { Text=value.ToString(),Kind=value,Start=start,End=cursor }); continue; }
                    if(value=='\"') {
                        StringBuilder decoded=new StringBuilder(); bool closed=false;
                        while(cursor<text.Length) {
                            char next=text[cursor++];
                            if(next=='\"') { closed=true; break; }
                            if(next=='\\' && cursor<text.Length && (text[cursor]=='\\' || text[cursor]=='\"'))next=text[cursor++];
                            decoded.Append(next);
                        }
                        if(!closed)throw Error("Unclosed quoted string",start);
                        tokens.Add(new Token { Text=decoded.ToString(),Kind='s',Start=start,End=cursor });
                    } else {
                        while(cursor<text.Length && !char.IsWhiteSpace(text[cursor]) && "{}=#\"".IndexOf(text[cursor])<0)cursor++;
                        tokens.Add(new Token { Text=text.Substring(start,cursor-start),Kind='a',Start=start,End=cursor });
                    }
                }
            }
            private FormatException Error(string message,int offset) {
                int line=1;
                for(int i=0;i<offset && i<text.Length;i++)if(text[i]=='\n')line++;
                return new FormatException(message+" at line "+line+".");
            }
        }
    }
}
