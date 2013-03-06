﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using Merthsoft.Tokens;
using Merthsoft.Tokens.DCSGUI;
using XmlValidator;
using System.Text;
using Merthsoft.CalcData;
using System.Xml.Schema;
using TokenIDE.Properties;
using TokenIDE.Project;
using System.Linq;
using Merthsoft.DynamicConfig;
using System.Drawing;

namespace TokenIDE {
	public partial class Tokens : Form {
		IEditWindow currWindow;
		//UserControl currWindow;

		List<TokensProject> projects;

		private int NumWindows {
			get {
				return EditWindows.TabPages.Count;
			}
		}

		TokenData _tokenData;
		TokenData TokenData {
			get { return _tokenData; }
			set {
				_tokenData = value;
				TokensTree.Nodes.Clear();
				commentBox.Clear();
				List<string> groups = _tokenData.GroupNames;
				groups.Sort();
				foreach (string group in groups) {
					TreeNode n = new TreeNode(group);
					TokensTree.Nodes.Add(n);
					List<Merthsoft.Tokens.TokenData.GroupEntry> inGroup = _tokenData.GetAllInGroup(group);
					inGroup.Sort();
					foreach (Merthsoft.Tokens.TokenData.GroupEntry token in inGroup) {
						TreeNode tNode = new TreeNode(token.ToString());
						tNode.Tag = token;
						n.Nodes.Add(tNode);
					}
				}
				if (TokensTree.Nodes.Count > 0) {
					TokensTree.SelectedNode = TokensTree.Nodes[0];
				}
			}
		}

		dynamic config;

		Font editorFont;

		public Tokens(string[] files) {
			InitializeComponent();
			//TokenData = new TokenData("Tokens.xml");
			//Environment.CurrentDirectory = Application.StartupPath;
			config = Config.ReadIni("TokenIDE.ini");

			OpenTokensFile(Path.Combine(Application.StartupPath, config.TokenIDE.file.ToString()));
			editorFont = new Font(config.Font.family, float.Parse(config.Font.size));

			AddNewTab();

			foreach (string file in files) {
				try {
					OpenFile(file);
				} catch (Exception e) {
					MessageBox.Show(string.Format("Could not open file {0}: {1}.", file, e.Message));
				}
			}

			projects = new List<TokensProject>();
		}

		private void openFileToolStripMenuItem_Click(object sender, EventArgs e) {
			//try {
			FileDialog fd = new OpenFileDialog();
			fd.ShowDialog();
			if (fd.FileName == string.Empty || fd.FileName == null)
				return;
			OpenFile(fd.FileName);
		}

		private void OpenFile(string fileName) {
			foreach (TabPage tab in EditWindows.TabPages) {
				if (tab.Controls[0] is IEditWindow && ((IEditWindow)tab.Controls[0]).FileName == fileName) {
					EditWindows.SelectedTab = tab;
					return;
				}
			}

			FileInfo fi = new FileInfo(fileName);
			TabPage tp = new TabPage();
			Prog8xEditWindow ewProg;
			List8xEditWindow ewList;
			tp.Text = fi.Name;
			IEditWindow prevWindow = currWindow;
			string ext = fi.Extension.ToLower();
			switch (ext) {
				case ".8xp":
				case ".83p":
				case ".82p":
				case ".73p":
					TokenData tokenData = new Merthsoft.Tokens.TokenData((string)((IDictionary<string, object>)(config.Extensions))[ext.Substring(1)]);
					currWindow = ewProg = new Prog8xEditWindow(tokenData, fileName);
					using (FileStream pstream = new FileStream(fileName, FileMode.Open)) {
						using (BinaryReader preader = new BinaryReader(pstream)) {
							Prog8x newProg8x = new Prog8x(preader);
							if (newProg8x.IsAsm) {
								if (MessageBox.Show(
										string.Format("{0} is an assembly program, are you sure you want to open it?", fileName), 
										"ASM Program Detected", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation
									) == System.Windows.Forms.DialogResult.No) {
									return;
								}
							}
							ewProg.Program = newProg8x;
						}
					}
					ewProg.FullHighlightRefresh();
					tp.Controls.Add(ewProg);
					ewProg.ParentTabPage = tp;
					ewProg.ReadOnly = false;
					ewProg.ProgramTextBox.Font = editorFont;
					break;
				case ".txt":
					currWindow = ewProg = new Prog8xEditWindow(TokenData, fileName);
					ewProg.Program = new Prog8x(fi.Name.Split('.')[0]);
					using (StreamReader sr = new StreamReader(fileName)) {
						ewProg.ProgramText = sr.ReadToEnd();
					}
					ewProg.RefreshBytes();
					ewProg.FullHighlightRefresh();
					tp.Controls.Add(ewProg);
					ewProg.ParentTabPage = tp;
					ewProg.ProgramTextBox.Font = editorFont;
					break;
				case ".8xv":
					currWindow = ewProg = new Prog8xEditWindow(TokenData, fileName);
					using (FileStream pstream = new FileStream(fileName, FileMode.Open)) {
						using (BinaryReader preader = new BinaryReader(pstream)) {
							ewProg.Program = new AppVar8x(preader);
						}
					}
					ewProg.FullHighlightRefresh();
					tp.Controls.Add(ewProg);
					ewProg.ParentTabPage = tp;
					ewProg.ReadOnly = false;
					ewProg.ProgramTextBox.Font = editorFont;
					break;
				case ".8xl":
					currWindow = ewList = new List8xEditWindow(fileName);
					using (FileStream pstream = new FileStream(fileName, FileMode.Open)) {
						using (BinaryReader preader = new BinaryReader(pstream)) {
							ewList.List = new RealList8x(preader);
						}
					}
					tp.Controls.Add(ewList);
					ewList.ParentTabPage = tp;
					break;
				default:
					throw new Exception(string.Format("File type not supported: {0}.", fi.Extension));
			}
			tp.Text = currWindow.OnCalcName;
			EditWindows.TabPages.Add(tp);
			EditWindows.SelectedTab = tp;
			if (prevWindow.OnCalcName == "" && prevWindow.NumTokens == 0 &&
				prevWindow.ParentTabPage.Text == "new file" && prevWindow.FirstFileFlag) {
				EditWindows.TabPages.Remove(prevWindow.ParentTabPage);
			}

			//currWindow.DragEnter += TokenIDE_DragEnter;
			//currWindow.DragDrop += TokenIDE_DragDrop;
			//} catch (Exception ex) {
			//	MessageBox.Show(string.Format("Could not open file!\n{0}", ex.Message));
			//}
		}

		private void CompileToolStripMenuItem_Click(object sender, EventArgs e) {
			Var8x.VarType varType = Var8x.VarType.Program;
			Var8x.CalcType calcType = Var8x.CalcType.Calc8x;
			buildFile(varType, calcType);
		}

		private void buildFile(Var8x.VarType varType, Var8x.CalcType calcType) {
			string dir;
			byte[] data = setUpSave(out dir);
			if (data == null) {
				return;
			}
			try {
				build(varType, calcType, currWindow.OnCalcName, dir, new object[2] { currWindow.NumTokens.ToString(), data });
				statusLabel.Text = "Build succeeded";
			} catch (Exception ex) {
				statusLabel.Text = string.Concat("Build failed: ", ex.ToString());
			}
		}

		private void tokenize83pToolStripMenuItem_Click(object sender, EventArgs e) {
			Var8x.VarType varType = Var8x.VarType.Program;
			Var8x.CalcType calcType = Var8x.CalcType.Calc83;
			buildFile(varType, calcType);

		}

		public string getExtension(Var8x.VarType varType, Var8x.CalcType calcType) {
			string prefix = null;
			string suffix = null;
			switch (calcType) {
				case Var8x.CalcType.Calc8x:
					prefix = "8x";
					break;
				case Var8x.CalcType.Calc83:
					prefix = "83";
					break;
				case Var8x.CalcType.Calc82:
					prefix = "82";
					break;
				case Var8x.CalcType.Calc73:
					prefix = "73";
					break;
				default:
					break;
			}

			switch (varType) {
				case Var8x.VarType.AppVar:
					suffix = "v";
					break;
				case Var8x.VarType.Program:
				case Var8x.VarType.ProgramLocked:
					suffix = "p";
					break;
			}

			return "." + prefix + suffix;
		}

		public void build(Var8x.VarType varType, Var8x.CalcType calcType, string name, string dir, object[] data) {
			Var8x var = null;
			string ext = getExtension(varType, calcType);
			if (varType == Var8x.VarType.AppVar) {
				var = new AppVar8x(name, calcType);
			} else {
				var = new Prog8x(name, calcType);
			}
			var.SetData(data);

			using (StreamWriter s = new StreamWriter(Path.Combine(dir, name + ext))) {
				var.Save(new BinaryWriter(s.BaseStream));
			}
		}

		private void hexSpriteEditorToolStripMenuItem_Click(object sender, EventArgs e) {
			if (!(currWindow is Prog8xEditWindow)) { return; }
			Prog8xEditWindow ew = (Prog8xEditWindow)currWindow;

			try {
				HexSprite s;
				if (ew.SelectedText != "") {
					s = new HexSprite(ew.SelectedText.Trim().Replace("\"", "").Replace("(", "").Replace(")", "").Replace(",", ""));
				} else {
					s = new HexSprite();
				}
				s.ShowDialog();
				if (s.outString != "") {
					ew.SelectedText = s.outString;
				}
			} catch (Exception ex) {
				MessageBox.Show(ex.ToString());
			}
		}

		private void dCSGuiDesignerToolStripMenuItem_Click(object sender, EventArgs e) {
			if (!(currWindow is Prog8xEditWindow)) { return; }
			Prog8xEditWindow ew = (Prog8xEditWindow)currWindow;

			DCSGUIDesigner d = new DCSGUIDesigner();
			d.ShowDialog();
			if (d.outControls == null)
				return;
			string s = "OpenGUIStack(\n";
			foreach (GUIItem dgi in d.outControls) {
				s += dgi.GetOutString() + "\n";
			}
			s += "RenderGUI(";
			ew.SelectedText = s;
		}

		private void imageEditosToolStripMenuItem_Click(object sender, EventArgs e) {
			if (!(currWindow is Prog8xEditWindow)) { return; }
			Prog8xEditWindow ew = (Prog8xEditWindow)currWindow;

			ImageEditor i = new ImageEditor();
			i.ShowDialog();
			if (i.OutStrings != null) {
				string s = "";
				foreach (string str in i.OutStrings) {
					s += str + "\n";
				}
				ew.SelectedText = s;
			}
		}

		private void aboutToolStripMenuItem_Click(object sender, EventArgs e) {
			AboutBox1 a = new AboutBox1();
			a.ShowDialog();
		}

		private void SchemaStringValidationHandler(object sender, ValidationEventArgs args) {
			throw new Exception("Shaun, what the fuck did you do?!");
		}

		private void changeTokenFileToolStripMenuItem_Click(object sender, EventArgs e) {
			OpenFileDialog f = new OpenFileDialog();
			f.AddFilter("Token Files", "*.xml");
			if (f.ShowDialog() == System.Windows.Forms.DialogResult.Cancel) { return; }
			string xmlFileName = f.FileName;
			OpenTokensFile(xmlFileName);
		}

		private void OpenTokensFile(string xmlFileName) {
			if (string.IsNullOrEmpty(xmlFileName) || !File.Exists(xmlFileName)) {
				return;
			}

			XmlValidator.XmlValidator validator = new XmlValidator.XmlValidator();
			XmlSchema xmlSchema = XmlSchema.Read(new StringReader(Resources.Tokens_xsd), SchemaStringValidationHandler);
			if (!validator.Validate(xmlSchema, xmlFileName)) {
				using (StreamWriter sw = new StreamWriter("xml.err", false)) {
					sw.WriteLine("Line:Column Message");
					foreach (XmlValidator.ValidationError error in validator.Errors) {
						sw.WriteLine(string.Format("{0}:{1} {2}", error.Line, error.Column, error.Message));
					}
				}
				MessageBox.Show("Error: The XML file you attempted to load did not match the schema. Please select a different file. See xml.err for a detailed list of errors.");
			} else {
				TokenData oldTokenData = TokenData;
				TokenData = new TokenData(xmlFileName);
				if (currWindow != null && currWindow is Prog8xEditWindow) {
					int a;
					Prog8xEditWindow ew = (Prog8xEditWindow)currWindow;
					ew.ProgramText = TokenData.Detokenize(oldTokenData.Tokenize(ew.ProgramText, out a));
					ew.TokenData = TokenData;
					ew.Dirty = false;
				}
			}
		}

		private void saveToolStripMenuItem_Click(object sender, EventArgs e) {
			if (EditWindows.SelectedTab.Controls[0] is Prog8xEditWindow) {
				SaveTextFile(false);
			}
		}

		private void saveAsToolStripMenuItem_Click(object sender, EventArgs e) {
			if (EditWindows.SelectedTab.Controls[0] is Prog8xEditWindow) {
				SaveTextFile(true);
			}
		}

		private void SaveTextFile(bool saveAs) {
			if (currWindow.New || saveAs) {
				//string progName = InputBox.ShowInputBox("Program Name");
				SaveFileDialog sfd = new SaveFileDialog();
				sfd.AddExtension = true;
				sfd.FileName = currWindow.OnCalcName + ".txt";
				sfd.DefaultExt = ".txt";
				sfd.AddFilter("Text File", ".txt");
				//sfd.CheckFileExists = true;
				if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.Cancel) { return; }
				if (!string.IsNullOrWhiteSpace(sfd.FileName)) {
					//currWindow.ProgramName = new FileInfo(sfd.FileName).Name;
					currWindow.FileName = sfd.FileName;
				} else {
					statusLabel.Text = "Save failed";
					return;
				}
				currWindow.New = false;
			}
			//if (String.IsNullOrWhiteSpace(currWindow.OnCalcName)) {
			//    statusLabel.Text = "Save failed";
			//    return;
			//}
			//string programName = currWindow.ProgramName;
			//SaveProgram(currWindow.SaveDirectory, programName, !currWindow.HasSaved);
			try {
				currWindow.Save();
				statusLabel.Text = "Save succeeded.";
			} catch (Exception ex) {
				statusLabel.Text = "Save failed: " + ex.Message;
			}
			//currWindow.HasSaved = true;
			return;
		}

		//private bool SaveProgram(string dir, string programName, bool checkExists) {
		//    programName = programName + ".txt";
		//    if (checkExists && File.Exists(programName)) {
		//        var res = MessageBox.Show("Are you sure you want to overwrite " + programName, "File Already Exists", MessageBoxButtons.YesNo);
		//        if (res != System.Windows.Forms.DialogResult.Yes) {
		//            statusLabel.Text = "Save failed";
		//            return false;
		//        }
		//    }
		//    try {
		//        using (StreamWriter sw = new StreamWriter(programName, false)) {
		//            sw.Write(currWindow.ProgramText.Replace("\n", Environment.NewLine));
		//        }
		//        statusLabel.Text = "Save succeeded";
		//    } catch (Exception ex) {
		//        statusLabel.Text = string.Concat("Save failed: ", ex.ToString());
		//        return false;
		//    }
		//    currWindow.Dirty = false;
		//    return true;
		//}

		private void EditWindows_SelectedIndexChanged(object sender, EventArgs e) {
			if (EditWindows.TabPages.Count > 0) {
				currWindow = (IEditWindow)EditWindows.SelectedTab.Controls[0];
			} else {
				currWindow = null;
			}
		}

		private void TokensTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e) {
			TokensTree.SelectedNode = e.Node;
			if (TokenData.Comments.ContainsKey(e.Node.Text)) {
				string comment = TokenData.Comments[e.Node.Text];
				if (comment != null) {
					commentBox.Text = comment.Replace("\n", Environment.NewLine);//.Replace("\\n", "\n");
				} else {
					commentBox.Text = "No data available for this command.";
				}
			} else {
				commentBox.Text = "";
			}
		}

		private void TokensTree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e) {
			if (!(currWindow is Prog8xEditWindow)) { return; }
			Prog8xEditWindow ew = (Prog8xEditWindow)currWindow;

			if (e.Node.Nodes.Count == 0 && e.Node.IsSelected) {
				ew.SelectedText = ((Merthsoft.Tokens.TokenData.GroupEntry)e.Node.Tag).Main;
				ew.ProgramTextBox.Focus();
			}
		}

		private void navigateToDocsToolStripMenuItem_Click(object sender, EventArgs e) {
			TreeNode n = TokensTree.SelectedNode;
			while (!TokenData.Sites.ContainsKey(n.Text) && n.Parent != null) {
				n = n.Parent;
			}
			if (!TokenData.Sites.ContainsKey(n.Text)) {
				MessageBox.Show(string.Format("No online documentation available for {0}.", TokensTree.SelectedNode.Text));
				return;
			}
			System.Diagnostics.Process.Start(TokenData.Sites[n.Text]);
		}

		private void fileToolStripMenuItem2_Click(object sender, EventArgs e) {
			AddNewTab();
		}

		private void TokensTree_AfterSelect(object sender, TreeViewEventArgs e) {
			if (e.Node != null) {
				if (TokenData.Comments.ContainsKey(e.Node.Text)) {
					string comment = TokenData.Comments[e.Node.Text];
					if (comment != null) {
						commentBox.Text = comment.Replace("\n", Environment.NewLine);//.Replace("\\n", "\n");
					} else {
						commentBox.Text = "No data available for this command.";
					}

					TreeNode n = e.Node;
					while (!TokenData.Sites.ContainsKey(n.Text) && n.Parent != null) {
						n = n.Parent;
					}
					if (!TokenData.Sites.ContainsKey(n.Text)) {
						docLinkLabel.Text = "";
					} else {
						docLinkLabel.Text = TokenData.Sites[n.Text];
					}
					mainToolTip.SetToolTip(docLinkLabel, docLinkLabel.Text);
				} else {
					commentBox.Text = "";
				}
			}
		}

		private void closeToolStripMenuItem_Click(object sender, EventArgs e) {
			CloseTab();
		}
		
		private void CloseTab() {
			if (currWindow.Dirty) {
				if (MessageBox.Show("File has not been saved, are you sure you want to exit?", "Exit?",
					MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No) {
					return;
				}
			}
			EditWindows.TabPages.Remove(currWindow.ParentTabPage);
			if (currWindow == null) {
				AddNewTab();
			}
		}

		private void AddNewTab() {
			TabPage tp = new TabPage("new file");
			Prog8xEditWindow ew = new Prog8xEditWindow(TokenData, "new file");
			ew.FirstFileFlag = true;
			ew.Program = new Prog8x("");
			ew.ParentTabPage = tp;
			tp.Controls.Add(ew);
			EditWindows.TabPages.Add(tp);
			currWindow = ew;

			ew.ProgramTextBox.Font = editorFont;
			ew.DragEnter += TokenIDE_DragEnter;
			ew.DragDrop += TokenIDE_DragDrop;
		}

		private void TokenIDE_FormClosing(object sender, FormClosingEventArgs e) {
			bool dirty = false;
			foreach (TabPage tp in EditWindows.TabPages) {
				if (((IEditWindow)tp.Controls[0]).Dirty) {
					dirty = true;
				}
			}
			if (dirty) {
				if (MessageBox.Show("File has not been saved, are you sure you want to exit?", "Exit?",
						MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No) {
					e.Cancel = true;
				}
			}
		}

		private byte[] setUpSave(out string directory) {
			directory = "";
			if (currWindow == null)
				return null;
			if (string.IsNullOrWhiteSpace(currWindow.OnCalcName)) {
				currWindow.OnCalcName = InputBox.ShowInputBox("Program Name");
				//currWindow.FileName = currWindow.ProgramName;
			}
			if (string.IsNullOrWhiteSpace(currWindow.OnCalcName)) {
				statusLabel.Text = "Build failed";
				return null;
			}
			byte[] data = currWindow.ByteData;
			if (data == null) {
				statusLabel.Text = "Build failed";
				return null;
			}

			if (string.IsNullOrWhiteSpace(currWindow.SaveDirectory)) {
				FolderBrowserDialog fbd = new FolderBrowserDialog();
				fbd.SelectedPath = Environment.CurrentDirectory;
				if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.Cancel) {
					return null;
				}

				directory = fbd.SelectedPath;
				currWindow.SaveDirectory = directory;
			} else {
				directory = currWindow.SaveDirectory;
			}

			return data;
		}

		private void tokenize8xvToolStripMenuItem_Click(object sender, EventArgs e) {
			//string dir;
			//byte[] data = setUpSave(out dir);
			//if (data == null)
			//    return;
			//try {
			//    currWindow.CalcVar.ID = Var8x.VarType.AppVar;
			//    currWindow.CalcVar.SetData(new object[2] { currWindow.NumTokens.ToString(), data });
			//    using (StreamWriter s = new StreamWriter(Path.Combine(dir, currWindow.OnCalcName + ".8xv"))) {
			//        currWindow.CalcVar.Save(new BinaryWriter(s.BaseStream));
			//    }
			//    statusLabel.Text = "Build succeeded";
			//} catch (Exception ex) {
			//    statusLabel.Text = string.Concat("Build failed: ", ex.ToString());
			//}
			buildFile(Var8x.VarType.AppVar, Var8x.CalcType.Calc8x);
		}

		private void TokenIDE_DragDrop(object sender, DragEventArgs e) {
			// Extract the data from the DataObject-Container into a string list
			string[] fileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);

			foreach (string file in fileList) {
				OpenFile(file);
			}
		}

		private void TokenIDE_DragEnter(object sender, DragEventArgs e) {
			// Check if the Dataformat of the data can be accepted
			// (we only accept file drops from Explorer, etc.)
			e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
		}

		private void changeSaveDirectoryToolStripMenuItem_Click(object sender, EventArgs e) {
			FolderBrowserDialog fbd = new FolderBrowserDialog();
			fbd.SelectedPath = Environment.CurrentDirectory;
			if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.Cancel) {
				return;
			}

			currWindow.SaveDirectory = fbd.SelectedPath;
		}

		private void findToolStripMenuItem_Click(object sender, EventArgs e) {
			//Find f = new Find(currWindow);
			//f.Show();
			if (currWindow is Prog8xEditWindow) {
				var ew = (Prog8xEditWindow)currWindow;
				FastColoredTextBoxNS.FindForm f = new FastColoredTextBoxNS.FindForm(ew.ProgramTextBox);
				f.Show();
			}
		}

		private void projectToolStripMenuItem_Click(object sender, EventArgs e) {
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.AddFilter("Tokens Project", "*.xml");
			ofd.CheckFileExists = true;
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK || ofd.FileName == "") { return; }

			TokensProject proj = new TokensProject(ofd.FileName);
			AddProject(proj);
		}

		private void AddProject(TokensProject proj) {
			projects.Add(proj);

			TreeNode projectNode = new TreeNode(proj.Name);
			projectNode.ImageKey = "icon_project.png";

			TreeNode programsNode = new TreeNode("Programs");
			programsNode.ImageKey = programsNode.SelectedImageKey = "icon_files.png";
			foreach (var projItem in proj.Programs) {
				TreeNode progNode = new TreeNode(projItem.ToString());
				progNode.Tag = projItem;
				progNode.ImageKey = progNode.SelectedImageKey = "icon_prog.png";
				programsNode.Nodes.Add(progNode);
			}
			projectNode.Nodes.Add(programsNode);
			
			TreeNode appVarNodes = new TreeNode("AppVars");
			appVarNodes.ImageKey = appVarNodes.SelectedImageKey = "icon_files.png";
			foreach (var appVarItem in proj.AppVars) {
				TreeNode progNode = new TreeNode(appVarItem.ToString());
				progNode.Tag = appVarItem;
				progNode.ImageKey = progNode.SelectedImageKey = "icon_appvar.png";
				appVarNodes.Nodes.Add(progNode);
			}
			projectNode.Nodes.Add(appVarNodes);

			projectTree.Nodes.Add(projectNode);
			projectNode.Expand();
		}

		private void projectTree_DoubleClick(object sender, EventArgs e) {
			TreeNode selectedNode = projectTree.SelectedNode;
			if (selectedNode == null) { return; }

			if (selectedNode.Tag != null && selectedNode.Parent.Text == "Programs" || selectedNode.Parent.Text == "AppVars") {
				OpenFile(((ProjectItem)selectedNode.Tag).File);
			}
		}

		private void buildAllToolStripMenuItem_Click(object sender, EventArgs e) {

		}

		private void projectTree_AfterExpand(object sender, TreeViewEventArgs e) {
			if (e.Node.Nodes.Count == 1) { e.Node.Nodes[0].Expand(); }
		}

		private void EditWindows_MouseClick(object sender, MouseEventArgs e) {
			//if (e.Button == System.Windows.Forms.MouseButtons.Middle) {
			//        CloseTab(EditWindows.Tab);
			//}			
		}

		private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e) {

		}

		private void blockCountsToolStripMenuItem_Click(object sender, EventArgs e) {
			Prog8xEditWindow editWindow = currWindow as Prog8xEditWindow;
			if (currWindow == null) { return; }

			GroupStats gs = new GroupStats(editWindow);
			gs.Show();
		}

		private void Tokens_Load(object sender, EventArgs e) {

		}

		private void optionsToolStripMenuItem_Click(object sender, EventArgs e) {
			Options o = new Options();
			o.SelectedFont = editorFont;
			o.TokenFile = config.TokenIDE.file;
			o.TokenData = TokenData;
			DialogResult result = o.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK) {
				config.TokenIDE.file = o.TokenFile;
				editorFont = o.SelectedFont;
				config.Font.family = editorFont.FontFamily.Name;
				config.Font.size = editorFont.SizeInPoints;
				Config.WriteIni(config, "TokenIDE.ini");
				foreach (TabPage window in EditWindows.Controls) {
					Prog8xEditWindow editWindow = window.Controls[0] as Prog8xEditWindow;
					if (editWindow == null) { continue; }
					editWindow.ProgramTextBox.Font = editorFont;
					editWindow.Invalidate();
				}
			}
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
			if (docLinkLabel.Text == "") { return; }
			System.Diagnostics.Process.Start(docLinkLabel.Text);
		}

		private void tokenize82pToolStripMenuItem_Click(object sender, EventArgs e) {
			buildFile(Var8x.VarType.Program, Var8x.CalcType.Calc82);
		}

		private void tokenize73pToolStripMenuItem_Click(object sender, EventArgs e) {
			buildFile(Var8x.VarType.Program, Var8x.CalcType.Calc73);
		}

		private void hexViewToolStripMenuItem_Click(object sender, EventArgs e) {
			Prog8xEditWindow window = currWindow as Prog8xEditWindow;
			if (window == null) { return; }
			List<List<TokenData.TokenDictionaryEntry>> tokens;
			window.GenerateByteData(false, false, out tokens);
			StringBuilder sb = new StringBuilder();
			foreach (var line in tokens) {
				foreach (var token in line) {
					sb.AppendFormat("[{0}:{1}] ", token.Name, token.Bytes);
				}
				sb.AppendLine();
			}

			TextBox tb = new TextBox();
			tb.Dock = DockStyle.Fill;
			tb.Multiline = true;
			tb.ScrollBars = ScrollBars.Both;
			tb.Font = editorFont;
			tb.WordWrap = false;
			tb.Text = sb.ToString();

			Form f = new Form();
			f.Text = "Hex View";
			f.Controls.Add(tb);

			f.Show();
		}

		public void dumpTokens(Dictionary<byte, TokenData.TokenDictionaryEntry> data, StringBuilder sb) {
			foreach (var token in data) {
				if (token.Value.Name != null) {
					sb.AppendFormat(string.Format("{0} - {1}", token.Value.Bytes, token.Value.Name.Replace("{", "{{").Replace("}", "}}").Replace("\\", "\\\\")));
				}
				sb.AppendLine();
				dumpTokens(token.Value.SubTokens, sb);
			}
		}

		private void dumpTokensToolStripMenuItem_Click(object sender, EventArgs e) {
			Prog8xEditWindow window = currWindow as Prog8xEditWindow;
			if (window == null) { return; }
			TokenData data = window.TokenData;
			StringBuilder sb = new StringBuilder();
			dumpTokens(data.Tokens, sb);

			window.ProgramText = sb.ToString();
		}
	}
}