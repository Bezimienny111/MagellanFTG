using EU2.Data;
using EU2.Map.Codec;
using EU2.Map.Drawing;
using LayerPainter;

namespace EU2MapEditorControls
{
	public class ColoredShaderProxy : ShaderProxy
	{
		private ColorScales scales;

		private ProvinceList provinces;

		public ColoredShaderProxy(ColorScales colorscales, ProvinceList provinces)
		{
			this.provinces = provinces;
			scales = colorscales;
		}

		public override short[] Shade16(RawImage image)
		{
			return new ColoredShader(scales, provinces, drawborders).Shade16(image);
		}

		public override int[] Shade32(RawImage image)
		{
			return new ColoredShader(scales, provinces, drawborders).Shade32(image);
		}
	}
}
