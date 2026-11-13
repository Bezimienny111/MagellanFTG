using System;
namespace EU2.Map {
    // Per-map limits: no process-global switch when different maps are open.
    public sealed class MapProfile {
        public const int MaximumEncodableProvinceCount = 32766;
        public const int FtgMaximumProvinceCount = 10000;
        private int count;
        private int threshold;
        public int ProvinceIDThreshold { get { return threshold; } }
        public int BorderPadding { get { return Math.Max(0, (threshold+511)/512-4); } }
        public void SetDefinedProvinceCount(int value) { if(value < 1 || value > count) throw new ArgumentOutOfRangeException("value"); threshold=value; }
        public void RegisterProvinceID(int id) { EnsureProvinceCount(id+1); threshold=Math.Max(threshold,id+1); }
        public int ProvinceCount { get { return count; } }
        public int LastProvinceID { get { return count - 1; } }
        public int LastPlayableID { get { return IsFtg ? threshold-1 : count-2; } }
        public int InternalCount { get { return count + 2; } }
        public ushort BorderID { get { return (ushort)count; } }
        public ushort BorderOutlineID { get { return (ushort)(count+1); } }
        public bool IsFtg { get; set; }
        public int BorderBase { get { return 4; } }
        public MapProfile() : this(2020, false) { }
        public MapProfile(int count, bool ftg) { SetProvinceCount(count); threshold=count; IsFtg = ftg; }
        public void EnsureProvinceCount(int required) { if (required > count) SetProvinceCount(required); }
        private void SetProvinceCount(int value) {
            if (value < 2 || value > MaximumEncodableProvinceCount)
                throw new ArgumentOutOfRangeException("value", "Province count must fit the map format (2..32766).");
            count = value;
        }
        public MapProfile Snapshot() { MapProfile result=new MapProfile(count,IsFtg); result.SetDefinedProvinceCount(threshold); return result; }
        public void ValidateFtgExport() {
            if (IsFtg && count > FtgMaximumProvinceCount)
                throw new InvalidOperationException("FTG supports at most 10000 provinces; this project contains " + count + ".");
        }
    }
}
