using System;
using System.IO;
using EU2.IO;
using EU2.Map;
using EU2.Map.Drawing;
using EU2.Data;

namespace EU2.Edit {
    // Adapter used by mimage /D when reading an installed game's native files.
    // Replaces the no-op adapter in the locally recovered source tree.
    public class EU2Proxy : File, IFile {
        public new void ReadFrom(string path) {
            if (path == null) throw new ArgumentNullException("path");
            Install install = Install.FromPath(Path.GetFullPath(path));
            MapProfile profile=FtgSupport.CreateProfile(install);
            Provinces=FtgSupport.ReadProvinces(install,profile);
            AdjacencyTable=new AdjacencyTable(install.GetInputMapFile("adjacent.tbl"),profile);
            IDMap=new IDMap(install.GetInputMapFile("id.tbl"));
            ColorScales=FtgSupport.ReadColorScales(install);
            Lightmap1 = LoadLightmap(install, 0);
            Lightmap2 = LoadLightmap(install, 1);
            Lightmap3 = LoadLightmap(install, 2);
            LoadAuxiliaryTables(install);
        }

        void IFile.ReadFrom(string path) { ReadFrom(path); }

        private Lightmap LoadLightmap(Install install, int zoom) {
            string path = install.GetInputMapFile("lightmap" + (zoom + 1) + ".tbl");
            return System.IO.File.Exists(path)
                ? Lightmap.FromFile(zoom, Provinces, AdjacencyTable, IDMap, path)
                : null;
        }
    }
}
