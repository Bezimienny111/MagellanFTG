namespace EU2.Edit
{
	public class InjectParams
	{
		private bool noTOTAndHRE;

		public bool NoTOTAndHRE
		{
			get
			{
				return noTOTAndHRE;
			}
			set
			{
				noTOTAndHRE = value;
			}
		}

		public InjectParams()
		{
		}

		public InjectParams(bool noTOTAndHRE)
		{
			this.noTOTAndHRE = noTOTAndHRE;
		}
	}
}
