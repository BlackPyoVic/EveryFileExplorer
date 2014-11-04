﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using LibEveryFileExplorer.Files;
using EveryFileExplorer.Plugins;
using EveryFileExplorer.Files;
using LibEveryFileExplorer;
using System.Runtime.InteropServices;
using System.Reflection;
using LibEveryFileExplorer.Compression;

namespace EveryFileExplorer
{
	public partial class Form1 : Form
	{
		private const int GWL_STYLE = -16;
		private const int GWL_EXSTYLE = -20;

		private const int WS_BORDER = 0x00800000;
		private const int WS_EX_CLIENTEDGE = 0x00000200;

		private const uint SWP_NOSIZE = 0x0001;
		private const uint SWP_NOMOVE = 0x0002;
		private const uint SWP_NOZORDER = 0x0004;
		private const uint SWP_NOREDRAW = 0x0008;
		private const uint SWP_NOACTIVATE = 0x0010;
		private const uint SWP_FRAMECHANGED = 0x0020;
		private const uint SWP_SHOWWINDOW = 0x0040;
		private const uint SWP_HIDEWINDOW = 0x0080;
		private const uint SWP_NOCOPYBITS = 0x0100;
		private const uint SWP_NOOWNERZORDER = 0x0200;
		private const uint SWP_NOSENDCHANGING = 0x0400;

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int SetWindowLong(IntPtr hWnd, int Index, int Value);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int GetWindowLong(IntPtr hWnd, int Index);

		[DllImport("user32.dll", ExactSpelling = true)]
		private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

		public Form1()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			for (int i = 0; i < this.Controls.Count; i++)
			{
				MdiClient mdiClient = this.Controls[i] as MdiClient;
				if (mdiClient != null)
				{
					SetMDIBorderStyle(mdiClient, BorderStyle.None);
				}
			}
			menuNew.MenuItems.Clear();
			foreach (Plugin p in Program.PluginManager.Plugins)
			{
				List<Type> creatables = new List<Type>();
				foreach (Type t in p.FileFormatTypes)
				{
					if (t.GetInterfaces().Contains(typeof(IEmptyCreatable))) creatables.Add(t);
				}
				if (creatables.Count > 0)
				{
					MenuItem m = menuNew.MenuItems.Add(p.Name);
					foreach (Type t in creatables)
					{
						MenuItem ii = m.MenuItems.Add(((dynamic)new StaticDynamic(t)).Identifier.GetFileDescription());
						ii.Click += new EventHandler(CreateNew_Click);
						ii.Tag = t;
					}
				}
			}
			menuFileNew.MenuItems.Clear();
			foreach (Plugin p in Program.PluginManager.Plugins)
			{
				List<Type> creatables = new List<Type>();
				foreach (Type t in p.FileFormatTypes)
				{
					if (t.GetInterfaces().Contains(typeof(IFileCreatable))) creatables.Add(t);
				}
				if (creatables.Count > 0)
				{
					MenuItem m = menuFileNew.MenuItems.Add(p.Name);
					foreach (Type t in creatables)
					{
						MenuItem ii = m.MenuItems.Add(((dynamic)new StaticDynamic(t)).Identifier.GetFileDescription());
						ii.Click += new EventHandler(CreateFileNew_Click);
						ii.Tag = t;
					}
				}
			}
			menuCompression.MenuItems.Clear();
			foreach (Plugin p in Program.PluginManager.Plugins)
			{
				if (p.CompressionTypes.Length != 0)
				{
					MenuItem m = menuCompression.MenuItems.Add(p.Name);
					foreach (Type t in p.CompressionTypes)
					{
						MenuItem ii = m.MenuItems.Add(((dynamic)new StaticDynamic(t)).Identifier.GetCompressionDescription());
						ii.MenuItems.Add("Decompress...");
						if (t.GetInterfaces().Contains(typeof(ICompressable))) ii.MenuItems.Add("Compress...");
						//ii.Click += new EventHandler(CreateFileNew_Click);
						//ii.Tag = t;
					}
				}
			}
		}

		private void SetMDIBorderStyle(MdiClient mdiClient, BorderStyle value)
		{
			// Get styles using Win32 calls
			int style = GetWindowLong(mdiClient.Handle, GWL_STYLE);
			int exStyle = GetWindowLong(mdiClient.Handle, GWL_EXSTYLE);

			// Add or remove style flags as necessary.
			switch (value)
			{
				case BorderStyle.Fixed3D:
					exStyle |= WS_EX_CLIENTEDGE;
					style &= ~WS_BORDER;
					break;

				case BorderStyle.FixedSingle:
					exStyle &= ~WS_EX_CLIENTEDGE;
					style |= WS_BORDER;
					break;

				case BorderStyle.None:
					style &= ~WS_BORDER;
					exStyle &= ~WS_EX_CLIENTEDGE;
					break;
			}

			// Set the styles using Win32 calls
			SetWindowLong(mdiClient.Handle, GWL_STYLE, style);
			SetWindowLong(mdiClient.Handle, GWL_EXSTYLE, exStyle);

			// Update the non-client area.
			SetWindowPos(mdiClient.Handle, IntPtr.Zero, 0, 0, 0, 0,
				SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER |
				SWP_NOOWNERZORDER | SWP_FRAMECHANGED);
		}

		public void BringMDIWindowToFront(Form Dialog)
		{
			ActivateMdiChild(Dialog);
			Dialog.BringToFront();
		}
		bool opening = false;
		private void OpenFile(object sender, EventArgs e)
		{
			if (opening) return;
			opening = true;
			String Filter = "";
			List<String> Filters = new List<string>();
			Filters.Add("all files (*.*)|*.*");
			List<String> Extenstions = new List<string>();
			String AllSupportedFilesA = "All Supported Files (";
			String AllSupportedFilesB = "|";
			foreach (Plugin p in Program.PluginManager.Plugins)
			{
				foreach (Type t in p.FileFormatTypes)
				{
					if (t.GetInterfaces().Contains(typeof(IViewable)))
					{
						dynamic d = new StaticDynamic(t);
						String FilterString;
						try
						{
							FilterString = d.Identifier.GetFileFilter();
						}
						catch (NotImplementedException)
						{
							continue;
						}
						if (FilterString == null || FilterString.Length == 0) continue;
						if (!Filters.Contains(FilterString.ToLower()))
						{
							string[] strArray = FilterString.Split(new char[] { '|' });
							if ((strArray == null) || ((strArray.Length % 2) != 0)) continue;
							string[] q = FilterString.Split('|');
							for (int i = 1; i < q.Length; i += 2)
							{
								foreach (string f in q[i].Split(';'))
								{
									if (!Extenstions.Contains(f.ToLower()))
									{
										Extenstions.Add(f.ToLower());
										if (!AllSupportedFilesA.EndsWith("(")) AllSupportedFilesA += ", ";
										AllSupportedFilesA += f.ToLower();
										if (!AllSupportedFilesB.EndsWith("|")) AllSupportedFilesB += ";";
										AllSupportedFilesB += f.ToLower();
									}
								}
							}
							Filters.Add(FilterString.ToLower());
							if (Filter != "") Filter += "|";
							Filter += FilterString;
						}
					}
				}
			}
			if (Filter != "")
			{
				Filter = AllSupportedFilesA + ")" + AllSupportedFilesB + "|" + Filter;
				Filter += "|";
			}
			Filter += "All Files (*.*)|*.*";
			openFileDialog1.Filter = Filter;
			if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK
				&& openFileDialog1.FileName.Length > 0)
			{
				Program.FileManager.OpenFile(new EFEDiskFile(openFileDialog1.FileName));
			}
			opening = false;
		}

		private void menuTile_Click(object sender, EventArgs e)
		{
			LayoutMdi(MdiLayout.TileVertical);
		}

		private void menuCascade_Click(object sender, EventArgs e)
		{
			LayoutMdi(MdiLayout.Cascade);
		}

		private void Form1_MdiChildActivate(object sender, EventArgs e)
		{
			if (ActiveMdiChild != null)
			{
				var v = Program.FileManager.GetViewableFileFromWindow(ActiveMdiChild);
				dynamic FileFormat = v.FileFormat;
				menuSave.Enabled = buttonSave.Enabled = FileFormat is IWriteable && (v.File.CompressionFormat == null || v.File.CompressionFormat is ICompressable);
				menuSaveAs.Enabled = FileFormat is IWriteable | FileFormat is IConvertable;
				menuClose.Enabled = true;
			}
			else
			{
				menuClose.Enabled = false;
				menuSaveAs.Enabled = menuSave.Enabled = buttonSave.Enabled = false;
			}
		}

		private void menuExit_Click(object sender, EventArgs e)
		{
			Application.Exit();
		}

		private void SaveFile(object sender, EventArgs e)
		{
			ViewableFile file = Program.FileManager.GetViewableFileFromWindow(ActiveMdiChild);
			if (!(file.FileFormat is IWriteable))
			{
				MessageBox.Show("This format is not saveable!");
				return;
			}
			if (file.File is EFEDiskFile && ((EFEDiskFile)file.File).Path == null)
			{
				saveFileDialog1.Filter = file.FileFormat.GetSaveDefaultFileFilter();
				saveFileDialog1.Title = "Save";
				saveFileDialog1.FileName = file.File.Name;
				if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK
				&& saveFileDialog1.FileName.Length > 0)
				{
					((EFEDiskFile)file.File).Path = saveFileDialog1.FileName;
					file.File.Name = System.IO.Path.GetFileName(saveFileDialog1.FileName);
					file.Dialog.Text = file.File.Name;
				}
				else return;
			}
			try
			{
				byte[] data = file.FileFormat.Write();
				file.File.Data = data;
				file.File.Save();
			}
			catch
			{
				MessageBox.Show("An error occurred while trying to save!");
			}
		}

		private void menuClose_Click(object sender, EventArgs e)
		{
			ActiveMdiChild.Close();
		}

		private void menuSaveAs_Click(object sender, EventArgs e)
		{
			String Filter = "";
			ViewableFile file = Program.FileManager.GetViewableFileFromWindow(ActiveMdiChild);
			if (file.FileFormat is IWriteable) Filter += file.FileFormat.GetSaveDefaultFileFilter();
			if (file.FileFormat is IConvertable)
			{
				if (Filter.Length > 0) Filter += "|";
				Filter += file.FileFormat.GetConversionFileFilters();
			}
			saveFileDialog1.Filter = Filter;
			saveFileDialog1.Title = "Save As";
			saveFileDialog1.FileName = file.File.Name;
			if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK
				&& saveFileDialog1.FileName.Length > 0)
			{
				if (file.FileFormat is IWriteable && saveFileDialog1.FilterIndex == 1)
				{
					try
					{
						byte[] data = file.FileFormat.Write();
						if (data != null)
						{
							System.IO.File.Create(saveFileDialog1.FileName).Close();
							System.IO.File.WriteAllBytes(saveFileDialog1.FileName, data);
						}
					}
					catch { }
				}
				else
				{
					if (!file.FileFormat.Convert(saveFileDialog1.FilterIndex - (file.FileFormat is IWriteable ? 2 : 1), saveFileDialog1.FileName))
					{
						MessageBox.Show("An error occured while trying to convert!");
					}
				}
			}
		}

		private void Form1_DragDrop(object sender, DragEventArgs e)
		{
			string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
			foreach (string file in files) Program.FileManager.OpenFile(new EFEDiskFile(file));
		}

		private void Form1_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
		}

		void CreateNew_Click(object sender, EventArgs e)
		{
			Program.FileManager.CreateEmptyFileWithType((Type)(((MenuItem)sender).Tag));
		}

		void CreateFileNew_Click(object sender, EventArgs e)
		{
			Program.FileManager.CreateFileFromFileWithType((Type)(((MenuItem)sender).Tag));
		}
	}
}
