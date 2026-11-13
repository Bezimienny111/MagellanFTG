using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using EU2.Data;
using EU2.Map;
using EU2.Map.Codec;
using EU2.Map.Drawing;
using MapFile=EU2.Edit.File;
using DiskFile=System.IO.File;

class MapTest144Builder {
    const int Grid=12, Cell=16, Patch=256, Margin=32;
    static int firstID;
    static MapFile map;
    static Rectangle patch, land;
    static ushort sea;
    static readonly List<Province> added=new List<Province>();
    static int Main(string[] args) {
        try {
            string source=Path.GetFullPath(args[0]), destination=Path.GetFullPath(args[1]), project=Path.GetFullPath(args[2]);
            Console.WriteLine("Reading WatK4 map project...");
            map=MapFile.FromFile(source);
            FindOcean();
            land=new Rectangle(patch.X+Margin,patch.Y+Margin,Grid*Cell,Grid*Cell);
            firstID=map.Profile.ProvinceIDThreshold;
            Console.WriteLine("Drawing 144 provinces at " + land + ", original sea ID " + sea + ", new IDs " + firstID + "-" + (firstID+143));
            AddProvinceDefinitions();
            PaintIDMap();
            AddAdjacencies();
            ExtendIDGrid();
            UpdateDiscoveryGrid();
            PaintLightmap(map.Lightmap1,0);
            PaintLightmap(map.Lightmap2,1);
            PaintLightmap(map.Lightmap3,2);
            Console.WriteLine("Writing native FTG map and province data...");
            map.Inject(EU2.Install.FromPath(destination));
            Console.WriteLine("Writing editable Magellan project...");
            map.WriteTo(project);
            WriteManifest(destination);
            DrawPreview(Path.Combine(Path.GetDirectoryName(project),"MapTest144-preview.png"));
            Console.WriteLine("Prepared " + destination);
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static void FindOcean() {
        var waters=map.Provinces.DefinedProvinces.Where(p=>p.IsOcean() && (p.Region.IndexOf("Atlantic",StringComparison.OrdinalIgnoreCase)>=0 || p.Name.IndexOf("Atlantic",StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(p=>Math.Abs(map.BoundBoxes[p.ID].Box.Top-2500));
        foreach(Province province in waters) {
            Rectangle box=map.BoundBoxes[province.ID].Box;
            int startY=Math.Max(1024,((box.Top+127)/128)*128), endY=Math.Min(5504,box.Bottom-Patch-32);
            int startX=Math.Max(256,((box.Left+127)/128)*128), endX=Math.Min(Lightmap.BaseWidth-Patch-256,box.Right-Patch-32);
            for(int y=startY;y<=endY;y+=128) for(int x=startX;x<=endX;x+=128) {
                int rx=x/2048, ry=y/2048;
                if(x%2048<384 || x%2048+Patch>1664 || y%2048<384 || y%2048+Patch>1664) continue;
                ushort[] local=map.IDGrid.GetIDs(rx,ry);
                if(local.Where(v=>v!=0).Distinct().Count()>90) continue;
                Rectangle candidate=new Rectangle(x-32,y-32,Patch+64,Patch+64);
                ushort[] ids=map.IDMap.GetIDs(candidate,false);
                if(ids.Length!=1 || ids[0]!=province.ID) continue;
                patch=new Rectangle(x,y,Patch,Patch); sea=province.ID;
                return;
            }
        }
        throw new InvalidOperationException("No sufficiently large Atlantic sea patch with room in its local ID table was found.");
    }
    static bool Edge(int row,int col) { return row==0 || col==0 || row==Grid-1 || col==Grid-1; }
    static void AddProvinceDefinitions() {
        for(int row=0;row<Grid;row++) for(int col=0;col<Grid;col++) {
            int ordinal=row*Grid+col, id=firstID+ordinal;
            Point center=new Point(land.X+col*Cell+Cell/2,land.Y+row*Cell+Cell/2);
            Province province=new Province((ushort)id,"PROV_MGT_"+(ordinal+1).ToString("000"));
            province.DefaultCityName="CITY_MGT_"+(ordinal+1).ToString("000");
            province.Terrain=EU2.Enums.Terrain.Plains;
            province.Religion=EU2.Enums.Religion.FromName("orthodox"); province.Culture=EU2.Enums.Culture.FromName("greek");
            province.Goods=EU2.Enums.Goods.FromName("grain");
            province.Region="Southern Europe"; province.Area="Iberia"; province.Continent="Europe";
            province.PIW=Edge(row,col) ? EU2.Enums.PlaceInWorld.Coastal : EU2.Enums.PlaceInWorld.Inland;
            province.PortAdjacency=Edge(row,col) ? sea : 0;
            province.SizeModifier=0; province.Climate=2; province.Ice=0; province.Storm=0;
            province.Manpower=1; province.Income=1; province.MineValue=0; province.COTHistoricalModifier=0;
            province.ColonizationDifficulty=0; province.NativeCombatStrength=0; province.NativeFerocity=0;
            province.NativeCombatEfficiency=0; province.TradingPostsNegotiation=0; province.NativesTolerance=0;
            province.CityPosition=center; province.ArmyPosition=new Point(center.X,center.Y+5);
            province.ManufactoryPosition=new Point(center.X+6,center.Y+4); province.FillCoordinates=center;
            if(Edge(row,col)) {
                Point port=center;
                if(row==0) port.Y=land.Top-6;
                else if(row==Grid-1) port.Y=land.Bottom+6;
                else if(col==0) port.X=land.Left-6;
                else port.X=land.Right+6;
                province.PortPosition=port;
            }
            map.Provinces[id]=province; added.Add(province);
        }
        map.AdjacencyTable.Profile=map.Profile; map.BoundBoxes.Profile=map.Profile;
        foreach(Province province in added) {
            int ordinal=province.ID-firstID;
            map.BoundBoxes[province.ID]=new ProvinceBoundBox(new Rectangle(land.X+(ordinal%Grid)*Cell,land.Y+(ordinal/Grid)*Cell,Cell,Cell),province.ID);
        }
    }
    static void PaintIDMap() {
        ushort[] pixels=map.IDMap.ExportBitmapBuffer(patch);
        for(int y=0;y<land.Height;y++) for(int x=0;x<land.Width;x++)
            pixels[(y+Margin)*patch.Width+x+Margin]=(ushort)(firstID+(y/Cell)*Grid+x/Cell);
        map.IDMap.ImportBitmapBuffer(patch,pixels);
    }
    static void AddAdjacencies() {
        List<Adjacent> coast=new List<Adjacent>();
        for(int row=0;row<Grid;row++) for(int col=0;col<Grid;col++) {
            ushort id=(ushort)(firstID+row*Grid+col);
            List<Adjacent> neighbours=new List<Adjacent>();
            if(col>0) neighbours.Add(new Adjacent((ushort)(id-1),AdjacencyType.Normal));
            if(col<Grid-1) neighbours.Add(new Adjacent((ushort)(id+1),AdjacencyType.Normal));
            if(row>0) neighbours.Add(new Adjacent((ushort)(id-Grid),AdjacencyType.Normal));
            if(row<Grid-1) neighbours.Add(new Adjacent((ushort)(id+Grid),AdjacencyType.Normal));
            if(Edge(row,col)) { neighbours.Add(new Adjacent(sea,AdjacencyType.Normal)); coast.Add(new Adjacent(id,AdjacencyType.Normal)); }
            map.AdjacencyTable[id]=neighbours.ToArray();
        }
        Adjacent[] old=map.AdjacencyTable[sea] ?? new Adjacent[0];
        map.AdjacencyTable[sea]=old.Where(a=>!map.Provinces[a.ID].IsRiver()).Concat(coast).Concat(old.Where(a=>map.Provinces[a.ID].IsRiver())).ToArray();
    }
    static void ExtendIDGrid() {
        byte[] bytes;
        using(MemoryStream stream=new MemoryStream()) { map.IDGrid.WriteTo(stream); bytes=stream.ToArray(); }
        int rx=land.X/2048, ry=land.Y/2048, offset=(rx*map.IDGrid.Height+ry)*256;
        HashSet<int> existing=new HashSet<int>();
        for(int i=0;i<256;i++) existing.Add(BitConverter.ToInt32(bytes,(offset+i)*4));
        int slot=1;
        foreach(Province province in added) {
            if(existing.Contains(province.ID)) continue;
            while(slot<256 && BitConverter.ToInt32(bytes,(offset+slot)*4)!=0) slot++;
            if(slot>=256) throw new InvalidOperationException("The selected region's ID table is full.");
            Buffer.BlockCopy(BitConverter.GetBytes((int)province.ID),0,bytes,(offset+slot)*4,4); slot++;
        }
        using(BinaryReader reader=new BinaryReader(new MemoryStream(bytes))) map.IDGrid=new IDGrid(reader);
    }
    static void UpdateDiscoveryGrid() {
        Console.WriteLine("Updating discovery grid around the new island...");
        int left=(land.Left-96)/32, right=(land.Right+96)/32, top=(land.Top-96)/32, bottom=(land.Bottom+96)/32;
        for(int by=top;by<=bottom;by++) for(int bx=left;bx<=right;bx++) {
            int x=bx*32, y=by*32;
            var nearest=added.Select(p=>new { Province=p, Distance=DistanceToBox(x,y,map.BoundBoxes[p.ID].Box) }).OrderBy(p=>p.Distance).ThenBy(p=>p.Province.ID).Take(4).ToArray();
            IDWeight[] values=new IDWeight[4];
            for(int i=0;i<4;i++) { values[i].ID=map.IDGrid.MapID(x,y,nearest[i].Province.ID); values[i].Weight=(byte)Math.Min(255,Math.Max(0,(int)(Math.Sqrt(nearest[i].Distance)*8))); }
            map.IncognitaGrid[bx,by]=new IncognitaGridItem(values);
        }
    }
    static double DistanceToBox(int x,int y,Rectangle box) {
        int dx=Math.Max(0,Math.Max(box.Left-x,x-box.Right+1));
        int dy=Math.Max(0,Math.Max(box.Top-y,y-box.Bottom+1));
        return dx*dx+dy*dy;
    }
    static void PaintLightmap(Lightmap lightmap,int zoom) {
        int scale=1<<zoom;
        Rectangle region=new Rectangle(patch.X/scale,patch.Y/scale,patch.Width/scale,patch.Height/scale);
        RawImage image=lightmap.DecodeImage(region);
        int cell=Cell/scale, inset=Margin/scale;
        for(int y=0;y<Grid*cell;y++) for(int x=0;x<Grid*cell;x++) {
            ushort id=(ushort)(firstID+(y/cell)*Grid+x/cell);
            bool border=x%cell==0 || y%cell==0 || x%cell==cell-1 || y%cell==cell-1;
            image.Memory[x+inset,y+inset]=new Pixel((byte)(border?38:24),id,0,(byte)(border?2:0));
        }
        lightmap.EncodeImage(image);
        Console.WriteLine("Painted lightmap"+(zoom+1));
    }
    static void WriteManifest(string destination) {
        StringBuilder csv=new StringBuilder("id;name;x;y;coastal;sea_adjacency\r\n");
        foreach(Province province in added) csv.AppendFormat("{0};Magellan {1:000};{2};{3};{4};{5}\r\n",province.ID,province.ID-firstID+1,province.CityPosition.X,province.CityPosition.Y,province.PortAdjacency!=0?"yes":"no",province.PortAdjacency);
        DiskFile.WriteAllText(Path.Combine(destination,"MAPTEST144_PROVINCES.csv"),csv.ToString(),new UTF8Encoding(false));
        DiskFile.WriteAllText(Path.Combine(destination,"MAPTEST144_LOCATION.txt"),"first_id="+firstID+"\r\nlast_id="+(firstID+143)+"\r\nsea_id="+sea+"\r\nx="+land.X+"\r\ny="+land.Y+"\r\nwidth="+land.Width+"\r\nheight="+land.Height+"\r\n",Encoding.ASCII);
    }
    static void DrawPreview(string path) {
        Rectangle view=Rectangle.Inflate(land,24,24);
        using(Bitmap image=new Bitmap(view.Width*2,view.Height*2)) using(Graphics graphics=Graphics.FromImage(image)) {
            graphics.Clear(Color.FromArgb(50,105,160));
            for(int row=0;row<Grid;row++) for(int col=0;col<Grid;col++) {
                int ordinal=row*Grid+col;
                Rectangle box=new Rectangle((24+col*Cell)*2,(24+row*Cell)*2,Cell*2,Cell*2);
                using(Brush brush=new SolidBrush(Color.FromArgb(180+(ordinal%4)*12,185+(ordinal%3)*12,125+(ordinal%5)*10))) graphics.FillRectangle(brush,box);
                graphics.DrawRectangle(Pens.Black,box); graphics.DrawString((firstID+ordinal).ToString(),SystemFonts.DefaultFont,Brushes.Black,box.X+2,box.Y+16);
            }
            image.Save(path,System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
