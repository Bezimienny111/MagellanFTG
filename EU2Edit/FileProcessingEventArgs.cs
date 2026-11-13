namespace EU2.Edit
{
	public class FileProcessingEventArgs
	{
		private string item;

		private FileProcessingAction action;

		public string Item
		{
			get
			{
				return item;
			}
		}

		public FileProcessingAction Action
		{
			get
			{
				return action;
			}
		}

		public FileProcessingEventArgs(string item, FileProcessingAction action)
		{
			this.item = item;
			this.action = action;
		}
	}
}
