using System;

namespace EU2.Edit
{
	public class InvalidFileFormatException : Exception
	{
		public InvalidFileFormatException(Exception e)
			: base("The file is in an incorrect format.", e)
		{
		}
	}
}
