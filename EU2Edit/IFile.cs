using System.IO;
using EU2.Data;
using EU2.Map;
using EU2.Map.Drawing;

namespace EU2.Edit
{
	public interface IFile
	{
		ProvinceList Provinces { get; set; }

		IDMap IDMap { get; set; }

		IDGrid IDGrid { get; set; }

		IncognitaGrid IncognitaGrid { get; set; }

		AdjacencyTable AdjacencyTable { get; set; }

		ColorScales ColorScales { get; set; }

		BoundBoxes BoundBoxes { get; set; }

		Lightmap Lightmap1 { get; set; }

		Lightmap Lightmap2 { get; set; }

		Lightmap Lightmap3 { get; set; }

		event FileProcessingHandler FileProcessing;

		void ReadFrom(Stream stream);

		void WriteTo(Stream stream);

		void ReadFrom(string path);

		void WriteTo(string path);
	}
}
