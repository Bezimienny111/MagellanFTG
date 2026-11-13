using System.IO;

namespace ICSharpCode.SharpZipLib.Tar
{
	public class InvalidHeaderException : IOException
	{
		public InvalidHeaderException()
		{
		}

		public InvalidHeaderException(string msg)
			: base(msg)
		{
		}
	}
}
