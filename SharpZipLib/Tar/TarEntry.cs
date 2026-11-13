using System;
using System.IO;
using System.Text;

namespace ICSharpCode.SharpZipLib.Tar
{
	public class TarEntry
	{
		private string file;

		private TarHeader header;

		public TarHeader TarHeader
		{
			get
			{
				return header;
			}
		}

		public string Name
		{
			get
			{
				return header.name.ToString();
			}
			set
			{
				header.name = new StringBuilder(value);
			}
		}

		public int UserId
		{
			get
			{
				return header.userId;
			}
			set
			{
				header.userId = value;
			}
		}

		public int GroupId
		{
			get
			{
				return header.groupId;
			}
			set
			{
				header.groupId = value;
			}
		}

		public string UserName
		{
			get
			{
				return header.userName.ToString();
			}
			set
			{
				header.userName = new StringBuilder(value);
			}
		}

		public string GroupName
		{
			get
			{
				return header.groupName.ToString();
			}
			set
			{
				header.groupName = new StringBuilder(value);
			}
		}

		public DateTime ModTime
		{
			get
			{
				return header.modTime;
			}
			set
			{
				header.modTime = value;
			}
		}

		public string File
		{
			get
			{
				return file;
			}
		}

		public long Size
		{
			get
			{
				return header.size;
			}
			set
			{
				header.size = value;
			}
		}

		public bool IsDirectory
		{
			get
			{
				if (file != null)
				{
					return Directory.Exists(file);
				}
				if (header != null && (header.typeFlag == 53 || header.name.ToString().EndsWith("/")))
				{
					return true;
				}
				return false;
			}
		}

		private TarEntry()
		{
		}

		public TarEntry(byte[] headerBuf)
		{
			Initialize();
			header.ParseBuffer(headerBuf);
		}

		public TarEntry(TarHeader header)
		{
			file = null;
			this.header = header;
		}

		public static TarEntry CreateTarEntry(string name)
		{
			TarEntry tarEntry = new TarEntry();
			tarEntry.Initialize();
			tarEntry.NameTarHeader(tarEntry.header, name);
			return tarEntry;
		}

		public static TarEntry CreateEntryFromFile(string fileName)
		{
			TarEntry tarEntry = new TarEntry();
			tarEntry.Initialize();
			tarEntry.GetFileTarHeader(tarEntry.header, fileName);
			return tarEntry;
		}

		private void Initialize()
		{
			file = null;
			header = new TarHeader();
		}

		public override bool Equals(object it)
		{
			if (!(it is TarEntry))
			{
				return false;
			}
			return header.name.ToString().Equals(((TarEntry)it).header.name.ToString());
		}

		public override int GetHashCode()
		{
			return header.name.ToString().GetHashCode();
		}

		public bool IsDescendent(TarEntry desc)
		{
			return desc.header.name.ToString().StartsWith(header.name.ToString());
		}

		public void SetIds(int userId, int groupId)
		{
			UserId = userId;
			GroupId = groupId;
		}

		public void SetNames(string userName, string groupName)
		{
			UserName = userName;
			GroupName = groupName;
		}

		public void AdjustEntryName(byte[] outbuf, string newName)
		{
			int offset = 0;
			offset = TarHeader.GetNameBytes(new StringBuilder(newName), outbuf, offset, TarHeader.NAMELEN);
		}

		public void GetFileTarHeader(TarHeader hdr, string file)
		{
			this.file = file;
			string text = file;
			if (Environment.CurrentDirectory == Path.GetDirectoryName(text))
			{
				text = Path.GetFileName(text);
			}
			text = text.Replace(Path.DirectorySeparatorChar, '/').ToLower();
			while (text.StartsWith("/"))
			{
				text = text.Substring(1);
			}
			hdr.linkName = new StringBuilder(string.Empty);
			hdr.name = new StringBuilder(text);
			if (Directory.Exists(file))
			{
				hdr.mode = 1003;
				hdr.typeFlag = 53;
				if (hdr.name.Length == 0 || hdr.name[hdr.name.Length - 1] != '/')
				{
					hdr.name.Append("/");
				}
				hdr.size = 0L;
			}
			else
			{
				hdr.mode = 33216;
				hdr.typeFlag = 48;
				hdr.size = new FileInfo(file.Replace('/', Path.DirectorySeparatorChar)).Length;
			}
			hdr.modTime = System.IO.File.GetLastWriteTime(file.Replace('/', Path.DirectorySeparatorChar)).ToUniversalTime();
			hdr.checkSum = 0;
			hdr.devMajor = 0;
			hdr.devMinor = 0;
		}

		public TarEntry[] GetDirectoryEntries()
		{
			if (file == null || !Directory.Exists(file))
			{
				return new TarEntry[0];
			}
			string[] fileSystemEntries = Directory.GetFileSystemEntries(file);
			TarEntry[] array = new TarEntry[fileSystemEntries.Length];
			for (int i = 0; i < fileSystemEntries.Length; i++)
			{
				array[i] = CreateEntryFromFile(fileSystemEntries[i]);
			}
			return array;
		}

		public void WriteEntryHeader(byte[] outbuf)
		{
			header.WriteHeader(outbuf);
		}

		public void NameTarHeader(TarHeader hdr, string name)
		{
			bool flag = name.EndsWith("/");
			hdr.checkSum = 0;
			hdr.name = new StringBuilder(name);
			hdr.mode = (flag ? 1003 : 33216);
			hdr.userId = 0;
			hdr.groupId = 0;
			hdr.size = 0L;
			hdr.checkSum = 0;
			hdr.modTime = DateTime.UtcNow;
			hdr.typeFlag = (byte)(flag ? 53 : 48);
			hdr.linkName = new StringBuilder(string.Empty);
			hdr.userName = new StringBuilder(string.Empty);
			hdr.groupName = new StringBuilder(string.Empty);
			hdr.devMajor = 0;
			hdr.devMinor = 0;
		}
	}
}
