using System;

namespace ICSharpCode.SharpZipLib
{
	public class ZipException : ApplicationException
	{
		public ZipException()
		{
		}

		public ZipException(string msg)
			: base(msg)
		{
		}
	}
}
