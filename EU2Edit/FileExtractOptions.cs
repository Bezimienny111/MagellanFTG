using System;

namespace EU2.Edit
{
	public class FileExtractOptions
	{
		private int includeLightmaps;

		private bool fixIdMap;

		private bool forceGenerateIDMap;

		public int IncludeLightmaps
		{
			get
			{
				return includeLightmaps;
			}
			set
			{
				if (value < 1 || value > 3)
				{
					throw new ArgumentOutOfRangeException();
				}
				includeLightmaps = value;
			}
		}

		public bool FixIdMap
		{
			get
			{
				return fixIdMap;
			}
			set
			{
				fixIdMap = value;
			}
		}

		public bool ForceGenerateIDMap
		{
			get
			{
				return forceGenerateIDMap;
			}
			set
			{
				forceGenerateIDMap = value;
			}
		}

		public FileExtractOptions()
			: this(3, false, true)
		{
		}

		public FileExtractOptions(int includeLightmaps, bool forceGenerateIDMap, bool fixIdMap)
		{
			IncludeLightmaps = includeLightmaps;
			this.forceGenerateIDMap = forceGenerateIDMap;
			this.fixIdMap = fixIdMap;
		}
	}
}
