using EU2.Map.Codec;
using EU2.Map.Drawing;

namespace LayerPainter
{
	public class DefaultShaderProxy : ShaderProxy
	{
		public override short[] Shade16(RawImage image)
		{
			return new GrayScaledShader(drawborders).Shade16(image);
		}

		public override int[] Shade32(RawImage image)
		{
			return new GrayScaledShader(drawborders).Shade32(image);
		}
	}
}
