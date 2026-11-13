using System;
using System.Runtime.InteropServices;
namespace EU2.Map.Codec {
    [StructLayout(LayoutKind.Sequential)]
    public struct Pixel {
        private byte color, border;
        private ushort id, riverID;
        public static readonly Pixel Empty = new Pixel(0,0,0,0);
        public Pixel(byte color, ushort id) : this(color,id,0,0) { }
        public Pixel(byte color, ushort id, ushort riverid, byte border) {
            this.color = color; this.id = id; this.riverID = riverid; this.border = border;
        }
        public byte Color { get { return color; } set { if(value>63) throw new ArgumentOutOfRangeException("value"); color=value; } }
        public byte Border { get { return border; } set { if(value>3) throw new ArgumentOutOfRangeException("value"); border=value; } }
        public ushort ID { get { return id; } set { id=value; } }
        public ushort RiverID { get { return riverID; } set { riverID=value; } }
        public bool IsBorder() { return border>0; }
        public override bool Equals(object value) { return value is Pixel && this == (Pixel)value; }
        public static bool operator ==(Pixel a, Pixel b) { return a.color==b.color && a.border==b.border && a.id==b.id && a.riverID==b.riverID; }
        public static bool operator !=(Pixel a, Pixel b) { return !(a==b); }
        public override int GetHashCode() { return id ^ (riverID<<16) ^ (color<<3) ^ border; }
    }
}
