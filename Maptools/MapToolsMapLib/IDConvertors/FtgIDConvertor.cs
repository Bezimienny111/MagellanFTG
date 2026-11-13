using System;
namespace MapToolsLib {
    public sealed class FtgIDConvertor : IIDConvertor {
        public int ConvertID(ushort id) { return ConvertID32(id); }
        public int ConvertID(ushort id, IDConvertorMode mode) { return mode==IDConvertorMode.RGB32 ? ConvertID32(id) : ConvertID16(id); }
        public int ConvertID32(ushort id) { return unchecked((int)0xFF000000) | id; }
        public int ConvertID16(ushort id) { return id; }
        public ushort ConvertRGB(int rgb) { int value=rgb & 0xFFFFFF; return value<=ushort.MaxValue ? (ushort)value : ushort.MaxValue; }
        public override string ToString() { return "FTG16"; }
    }
}
