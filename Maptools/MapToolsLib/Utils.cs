using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace MapToolsLib
{
	/// <summary>
	/// Summary description for Utils.
	/// </summary>
	public abstract class Utils
	{
		public static void LogError( Exception e ) {
			if ( e == null ) return;

			Console.WriteLine( );

			string msg = e.ToString();

			try {
				msg = "*** Environment Info ***\n\n" +
                    "Magellan Build Version: " + MapToolsVersion.Version.BuildName + " " + MapToolsVersion.Version.BuildVersion + "\n" +
                    "EU2 Lib Version: " + typeof(EU2.Map.Lightmap).Assembly.GetName().Version + "\n" +
					"Date: " + DateTime.Now.ToString( "yyyy/MM/dd HH:mm:ss" ) + "\n" +
					"Tool: " + System.Reflection.Assembly.GetCallingAssembly().FullName + " (v" + System.Reflection.Assembly.GetCallingAssembly().GetName().Version.ToString() + ")\n" +
					"OSVersion: " + Environment.OSVersion.Platform.ToString() + " - " + Environment.OSVersion.Version.ToString() + "\n" +
					"CurrentDir: " + Environment.CurrentDirectory  + "\n" +
					"SystemDir: " + Environment.SystemDirectory + "\n" +
					"CommandLine: " + Environment.CommandLine + "\n" +
					"CLR: " + Environment.Version.ToString() + "\n" +
					"EU2: " + (EU2.Install.FromRegistry() == null ? "(no install)" : EU2.Install.FromRegistry().RootPath) + "\n\n*** Error Info ***\n\n" +
					msg;
			}
			catch {
			}

			string name = String.Format( "error-{0}-{1}.log", System.Reflection.Assembly.GetCallingAssembly().GetName().Name, DateTime.Now.ToString("yyyyMMddHHmmss") );

			FileStream stream = null;
			try {
				if ( IsDebugBuild( Assembly.GetCallingAssembly() ) ) throw new Exception(); // to go to the catch statement
				stream = new FileStream( Path.Combine( Environment.CurrentDirectory, name ), FileMode.Create, FileAccess.Write, FileShare.None );
				StreamWriter writer = new StreamWriter( stream, System.Text.Encoding.UTF8 );
				writer.Write( msg );
				writer.Flush();
			}
			catch {
				Console.WriteLine( "An error occured.\nYou can read the error message below." );
				Console.WriteLine( msg );
				Console.WriteLine( "Please send this error info to eu2map@inferis.org for bugfixing." );
				return;
			}
			finally {
				if ( stream != null ) stream.Close();
			}

			Console.WriteLine( "An error occured.\nA log file (\"" + name + "\") has been generated, please send this file (or its contents) to eu2map@inferis.org for bugfixing." );
		}

        public static bool IsDebugBuild(Assembly assembly) {
            foreach (object att in assembly.GetCustomAttributes(false)) {
                if (att is DebuggableAttribute) {
                    return ((DebuggableAttribute)att).IsJITTrackingEnabled;
                }
            }

            return false;
        }

        public static ushort[] ScaleIDBuffer(ushort[,] buffer, int zoom) {
            int size=1<<zoom, width=buffer.GetLength(0), height=buffer.GetLength(1);
            if(width%size!=0 || height%size!=0) throw new ArgumentException("ID image dimensions must be divisible by the zoom factor.");
            ushort[] result=new ushort[(width/size)*(height/size)];
            int position=0;
            for(int y=0;y<height;y+=size) for(int x=0;x<width;x+=size) {
                System.Collections.Generic.Dictionary<ushort,int> counts=new System.Collections.Generic.Dictionary<ushort,int>();
                ushort best=0; int maximum=0;
                for(int by=0;by<size;by++) for(int bx=0;bx<size;bx++) {
                    ushort id=buffer[x+bx,y+by]; int count;
                    counts.TryGetValue(id,out count); count++; counts[id]=count;
                    if(count>maximum || count==maximum && id<best) { maximum=count; best=id; }
                }
                result[position++]=best;
            }
            return result;
        }

	}
}
