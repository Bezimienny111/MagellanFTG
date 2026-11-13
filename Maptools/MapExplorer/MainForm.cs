using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace MapExplorer {
    public class MainForm : Form {
        private readonly TreeView tvContents = new TreeView();
        private readonly Panel dcExplorer = new Panel();
        private readonly OpenFileDialog ofdSelectFile = new OpenFileDialog();

        public MainForm() {
            Text = "Magellan Map Explorer";
            ClientSize = new Size(1000, 700);
            IsMdiContainer = true;
            dcExplorer.Dock = DockStyle.Right;
            dcExplorer.Width = 250;
            tvContents.Dock = DockStyle.Fill;
            dcExplorer.Controls.Add(tvContents);
            Controls.Add(dcExplorer);
            MainMenu menu = new MainMenu();
            MenuItem file = new MenuItem("&File");
            file.MenuItems.Add(new MenuItem("&New", mbiFileNew_Activate));
            file.MenuItems.Add(new MenuItem("&Open...", mbiFileOpen_Activate));
            file.MenuItems.Add(new MenuItem("&Save", SaveCurrent));
            file.MenuItems.Add(new MenuItem("Save &as...", SaveAs));
            file.MenuItems.Add(new MenuItem("&Close", mbiFileClose_Activate));
            file.MenuItems.Add(new MenuItem("E&xit", mbiFileExit_Activate));
            menu.MenuItems.Add(file);
            menu.MenuItems.Add(new MenuItem("&Refresh", delegate { BuildExplorer(Workset.Current); }));
            Menu = menu;
            Load += MainForm_Load;
            FormClosing += delegate(object sender, FormClosingEventArgs e) { e.Cancel = !GetRidOfOldWorkset(); };
        }

        protected override void Dispose(bool disposing) {
            if (disposing) ofdSelectFile.Dispose();
            base.Dispose(disposing);
        }

        private void SaveCurrent(object sender, EventArgs e) {
            try {
                if (Workset.Current.File == null) return;
                if (!Workset.Current.SaveFile()) SaveAs(sender, e);
            } catch (Exception error) { HandleError(error); }
        }

        private void SaveAs(object sender, EventArgs e) {
            if (Workset.Current.File == null) return;
            using (SaveFileDialog dialog = new SaveFileDialog()) {
                dialog.Filter = "EU2 map (*.eu2map)|*.eu2map";
                dialog.DefaultExt = "eu2map";
                dialog.AddExtension = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try {
                    Workset.Current.SaveFileAs(dialog.FileName);
                    BuildExplorer(Workset.Current);
                } catch (Exception error) { HandleError(error); }
            }
        }

        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
		public void HandleError( Exception e ) {
			MessageBox.Show( this, "An error occured.\n\n" + e.ToString() );
		}

		private bool GetRidOfOldWorkset() {
			if ( Workset.Current.FileModified ) {
				DialogResult choice = MessageBox.Show( this,
					"You have unsaved changes to this map. Do you want to save the changes?",
					"Map still open",
					MessageBoxButtons.YesNoCancel,
					MessageBoxIcon.Question );

				if ( choice == DialogResult.Cancel ) return false;
				if (choice == DialogResult.Yes) {
                    SaveCurrent(this, EventArgs.Empty);
                    if (Workset.Current.FileModified) return false;
                }

				return true;
			}

			// Close all windows related to this workset
			if ( dcExplorer != null ) {
				BuildExplorer( null );
			}

			return true;
		}

		private void OpenWorkset() {
			// Build explorer tree
			BuildExplorer( Workset.Current );

			// open map window
//			MapForm map = new MapForm( );
//			map.MdiParent = this;
//			map.Show();
//			map.Source = Workset.Current;

			// open map window
			AdjacentiesForm adj = new AdjacentiesForm( Workset.Current );
			adj.MdiParent = this;
			adj.Show();
		}

		private void BuildExplorer( Workset workset ) {
			tvContents.Nodes.Clear();
			if ( workset == null || workset.File == null ) return;

			// -- add project node
			TreeNode root = new TreeNode( System.IO.Path.GetFileName( workset.FilePath ), 0, 0 );
			if ( root.Text.Length == 0 ) root.Text = "<new map>";
			tvContents.Nodes.Add( root );

			// -- add provinces node
			root = new TreeNode( "Provinces" );
			tvContents.Nodes[0].Nodes.Add( root );

			if ( workset.File.Provinces != null ) root.Nodes.Add( "Provinces" );

			// -- add map
			root = new TreeNode( "Maps" );
			tvContents.Nodes[0].Nodes.Add( root );

			if ( workset.File.Lightmap1 != null ) root.Nodes.Add( "Lightmap 1" );
			if ( workset.File.Lightmap2 != null ) root.Nodes.Add( "Lightmap 2" );
			if ( workset.File.Lightmap3 != null ) root.Nodes.Add( "Lightmap 3" );
			if ( workset.File.ColorScales != null ) root.Nodes.Add( "Colorscales" );

			root = new TreeNode( "Map Support" );
			tvContents.Nodes[0].Nodes.Add( root );

			if ( workset.File.IDMap != null ) root.Nodes.Add( "ID Map" );
			if ( workset.File.AdjacencyTable != null ) root.Nodes.Add( "Adjacency Table" );
			if ( workset.File.BoundBoxes != null ) root.Nodes.Add( "Boundboxes" );
			if ( workset.File.IncognitaGrid != null ) root.Nodes.Add( "Incognita Grid" );
			if ( workset.File.IDGrid != null ) root.Nodes.Add( "ID Grid" );
		}

		private void MainForm_Load(object sender, System.EventArgs e) {
			Text = Text + " (v" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + ")";
			Workset.Current.LastOpenDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		}

		private void mbiFileNew_Activate(object sender, System.EventArgs e) {
			if ( !GetRidOfOldWorkset() ) return;

			Workset.Current.CreateFile();
			OpenWorkset();
		}

		private void mbiFileOpen_Activate(object sender, System.EventArgs e) {
			if ( !GetRidOfOldWorkset() ) return;

			ofdSelectFile.AddExtension = true;
			ofdSelectFile.Filter = "EU2Map Files (*.eu2map)|*.eu2map|All Files (*.*)|*.*";
			ofdSelectFile.Title = "Open Map File";
			ofdSelectFile.Multiselect = false;
			ofdSelectFile.ShowReadOnly = true;
			ofdSelectFile.InitialDirectory = Workset.Current.LastOpenDirectory;

			if ( ofdSelectFile.ShowDialog( this ).Equals( DialogResult.Cancel ) ) return;

			Application.DoEvents();
			string file = ofdSelectFile.FileName;
			Workset.Current.LastOpenDirectory = System.IO.Path.GetDirectoryName( file );
			try {
				Cursor = Cursors.WaitCursor;
				Application.DoEvents();
				Workset.Current.OpenFile( file );
				Cursor = Cursors.Default;
				Application.DoEvents();
			}
			catch ( Exception ex ) {
				HandleError( ex );
				return;
			}
			finally {
				Cursor = Cursors.Default;
			}

			if ( Workset.Current.File == null ) {
				MessageBox.Show( this, "Could not open file." );
				return;
			}

			OpenWorkset();
		}

		private void mbiFileClose_Activate(object sender, System.EventArgs e) {
			if ( !GetRidOfOldWorkset() ) return;
			Workset.Current.CloseFile();
			BuildExplorer(null);
		}

		private void mbiFileExit_Activate(object sender, System.EventArgs e) {
			Application.Exit();
		}
	}
}
