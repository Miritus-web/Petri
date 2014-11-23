﻿using System;
using System.Xml.Linq;
using System.Xml;
using Gtk;
using System.Collections.Generic;

namespace Petri
{
	public class Document {
		public Document(string path) {
			Window = new MainWindow(this);

			this.UndoManager = new UndoManager();
			this.Headers = new List<string>();
			CppActions = new List<Cpp.Function>();
			AllFunctions = new List<Cpp.Function>();
			CppConditions = new List<Cpp.Function>();

			EditorController = new EditorController(this);
			this.CurrentController = EditorController;

			this.Path = path;
			this.Blank = true;
			this.Restore();
			Window.PresentWindow();
		}

		public MainWindow Window {
			get;
			private set;
		}

		public EditorController EditorController {
			get;
			private set;
		}

		public Controller CurrentController {
			get;
			private set;
		}

		public string Path {
			get;
			set;
		}

		public bool Blank {
			get;
			set;
		}

		public void PostAction(GuiAction a) {
			Modified = true;
			UndoManager.PostAction(a);
			UpdateUndo();
			Window.PetriView.Redraw();
		}

		public void Undo() {
			UndoManager.Undo();
			var focus = UndoManager.NextRedo.Focus;
			CurrentController.ManageFocus(focus);

			UpdateUndo();
			Window.PetriView.Redraw();
		}

		public void Redo() {
			UndoManager.Redo();
			var focus = UndoManager.NextUndo.Focus;
			CurrentController.ManageFocus(focus);

			UpdateUndo();
			Window.PetriView.Redraw();
		}

		public List<string> Headers {
			get;
			private set;
		}

		public void AddHeader(string header) {
			if(header.Length == 0 || Headers.Contains(header))
				return;

			if(header.Length > 0) {
				var functions = Cpp.Parser.Parse(header);
				foreach(var func in functions) {
					if(func.ReturnType.Equals("ResultatAction")) {
						CppActions.Add(func);
					}
					else if(func.ReturnType.Equals("bool")) {
						CppConditions.Add(func);
					}
					AllFunctions.Add(func);
				}
			}

			Headers.Add(header);

			Modified = true;
		}

		// Performs the removal if possible
		public bool RemoveHeader(string header) {
			if(PetriNet.UsesHeader(header))
				return false;

			CppActions.RemoveAll(a => a.Header == header);
			CppConditions.RemoveAll(c => c.Header == header);
			Headers.Remove(header);

			Modified = true;

			return true;
		}

		public List<Cpp.Function> CppConditions {
			get;
			private set;
		}

		public List<Cpp.Function> AllFunctions {
			get;
			private set;
		}

		public List<Cpp.Function> CppActions {
			get;
			private set;
		}

		public RootPetriNet PetriNet {
			get;
			set;
		}

		public bool Modified {
			get {
				return modified;
			}
			set {
				modified = value;
				if(value == true)
					this.Blank = false;

				// We require the current undo stack to represent an unmodified state
				if(value == false) {
					guiActionToMatchSave = UndoManager.NextUndo;
				}
			}
		}

		public bool CloseAndConfirm() {
			if(Modified) {
				MessageDialog d = new MessageDialog(Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, "Souhaitez-vous enregistrer les modifications apportées au graphe ? Vos modifications seront perdues si vous ne les enregistrez pas.");
				d.AddButton("Ne pas enregistrer", ResponseType.No);
				d.AddButton("Annuler", ResponseType.Cancel);
				d.AddButton("Enregistrer", ResponseType.Yes).HasDefault = true;

				ResponseType result = (ResponseType)d.Run();

				if(result == ResponseType.Yes) {
					Save();
					d.Destroy();
				}
				else if(result == ResponseType.No) {
					d.Destroy();
				}
				else {
					d.Destroy();
					return false;
				}
			}

			MainClass.RemoveDocument(this);
			if(MainClass.Documents.Count == 0)
				MainClass.SaveAndQuit();
			return true;
		}

		public void SaveAs() {
			var fc = new Gtk.FileChooserDialog("Enregistrer le graphe sous…", Window,
				FileChooserAction.Save,
				new object[]{"Annuler",ResponseType.Cancel,
					"Enregistrer",ResponseType.Accept});

			if(Configuration.SavePath.Length > 0) {
				fc.SetCurrentFolder(System.IO.Directory.GetParent(Configuration.SavePath).FullName);
				fc.CurrentName = System.IO.Path.GetFileName(Configuration.SavePath);
			}

			fc.DoOverwriteConfirmation = true;

			if(fc.Run() == (int)ResponseType.Accept) {
				this.Path = fc.Filename;
				if(!this.Path.EndsWith(".petri"))
					this.Path += ".petri";
				Window.Title = System.IO.Path.GetFileName(this.Path).Split(new string[]{".petri"}, StringSplitOptions.None)[0];
				fc.Destroy();
			}
			else {
				fc.Destroy();
				return;
			}

			this.Save();
		}

		public void Save()
		{
			string tempFileName = "";
			try {
				if(Path == "") {
					this.SaveAs();
					return;
				}

				var doc = new XDocument();
				var root = new XElement("Document");

				root.Add(settings.GetXml());

				var winConf = new XElement("Window");
				{
					int w, h, x, y;
					Window.GetSize(out w, out h);
					Window.GetPosition(out x, out y);
					winConf.SetAttributeValue("X", x.ToString());
					winConf.SetAttributeValue("Y", y.ToString());
					winConf.SetAttributeValue("W", w.ToString());
					winConf.SetAttributeValue("H", h.ToString());
				}

				var headers = new XElement("Headers");
				foreach(var h in Headers) {
					var hh = new XElement("Header");
					hh.SetAttributeValue("File", h);
					headers.Add(hh);
				}
				doc.Add(root);
				root.Add(winConf);
				root.Add(headers);
				root.Add(PetriNet.GetXml());

				// Write to a temporary file to avoid corrupting the existing document on error
				tempFileName = System.IO.Path.GetTempFileName();
				XmlWriterSettings xsettings = new XmlWriterSettings();
				xsettings.Indent = true;
				XmlWriter writer = XmlWriter.Create(tempFileName, xsettings);

				doc.Save(writer);
				writer.Flush();
				writer.Close();

				if(System.IO.File.Exists(this.Path))
					System.IO.File.Delete(this.Path);
				System.IO.File.Move(tempFileName, this.Path);
				tempFileName = "";

				Modified = false;
			}
			catch(Exception e) {
				if(tempFileName.Length > 0)
					System.IO.File.Delete(tempFileName);

				MessageDialog d = new MessageDialog(Window, DialogFlags.Modal, MessageType.Error, ButtonsType.None, "Une erreur est survenue lors de l'enregistrement : " + e.ToString());
				d.AddButton("OK", ResponseType.Cancel);
				d.Run();
				d.Destroy();
			}
		}

		public void Restore()
		{
			if(Modified) {
				MessageDialog d = new MessageDialog(Window, DialogFlags.Modal, MessageType.Warning, ButtonsType.None, "Souhaitez-vous revenir à la dernière version enregistrée du graphe ? Vos modifications seront perdues.");
				d.AddButton("Annuler", ResponseType.Cancel);
				d.AddButton("Revenir", ResponseType.Accept);

				ResponseType result = (ResponseType)d.Run();

				d.Destroy();
				if(result != ResponseType.Accept) {
					return;
				}
			}

			Window.PetriView.EditedPetriNet = null;
			EditorController.EditedObject = null;

			var oldPetriNet = PetriNet;

			this.ResetID();
			settings = null;

			try {
				if(Path == "") {
					PetriNet = new RootPetriNet(this);
					int docID = 1;
					string prefix = "Sans titre ";
					foreach(var d in MainClass.Documents) {
						if(d.Window.Title.StartsWith(prefix)) {
							int id = 0;
							if(int.TryParse(d.Window.Title.Substring(prefix.Length), out id)) {
								docID = id + 1;
							}
						}
					}
					Window.Title = prefix + docID.ToString();
					Modified = false;
					Blank = true;
				}
				else {
					var document = XDocument.Load(Path);

					var elem = document.FirstNode as XElement;

					settings = DocumentSettings.CreateSettings(this, elem.Element("Settings"));

					var winConf = elem.Element("Window");
					Window.Move(int.Parse(winConf.Attribute("X").Value), int.Parse(winConf.Attribute("Y").Value));
					Window.Resize(int.Parse(winConf.Attribute("W").Value), int.Parse(winConf.Attribute("H").Value));

					var node = elem.Element("Headers");
					foreach(var e in node.Elements()) {
						this.AddHeader(e.Attribute("File").Value);
					}

					PetriNet = new RootPetriNet(this, elem.Element("PetriNet"));
					PetriNet.Canonize();
					Window.Title = System.IO.Path.GetFileName(this.Path).Split(new string[]{".petri"}, StringSplitOptions.None)[0];
					this.Blank = false;
					Modified = false;
				}
			}
			catch(Exception e) {
				MessageDialog d = new MessageDialog(Window, DialogFlags.Modal, MessageType.Error, ButtonsType.None, "Une erreur est survenue lors de du chargement du document : " + e.ToString());
				d.AddButton("OK", ResponseType.Cancel);
				d.Run();
				d.Destroy();
				Console.WriteLine("Error during Petri net loading: {0}", e.Message);

				if(oldPetriNet != null) {
					// The document was already open and the user-requested restore failed for some reason. What could we do?
					// At least the modified document is preserved so that the user has a chance to save his work.
				}
				else {
					// If it is a fresh opening, just get back to an empty state.
					PetriNet = new RootPetriNet(this);
					Modified = false;
					this.Blank = true;
				}
			}
			if(settings == null) {
				settings = DocumentSettings.GetDefaultSettings(this);
			}
			Window.PetriView.EditedPetriNet = PetriNet;

			Window.PetriView.Redraw();
		}

		public void SaveCpp()
		{
			var fc = new Gtk.FileChooserDialog("Enregistrer le code généré sous…", Window,
				FileChooserAction.SelectFolder,
				new object[]{"Annuler",ResponseType.Cancel,
					"Enregistrer",ResponseType.Accept});
			
			if(this.settings.OutputPath.Length > 0) {
				fc.SetCurrentFolder(this.settings.OutputPath);
			}

			fc.DoOverwriteConfirmation = true;

			if(fc.Run() == (int)ResponseType.Accept) {
				var cppGen = PetriNet.GenerateCpp();
				cppGen.Item1.AddHeader("\"" + System.IO.Path.Combine(fc.Filename, settings.Name) + ".h\"");
				cppGen.Item1.Write(System.IO.Path.Combine(fc.Filename, settings.Name) + ".cpp");

				var generator = new Cpp.Generator();
				generator.AddHeader("\"PetriUtils.h\"");

				generator += "#ifndef PETRI_" + cppGen.Item2 + "_H";
				generator += "#define PETRI_" + cppGen.Item2 + "_H\n";

				generator += "#define CLASS_NAME MyPetriNet";
				generator += "#define PREFIX \"" + settings.Name + "\"";
				generator += "#define LIB_PATH \"" + System.IO.Path.Combine(fc.Filename, settings.Name) + ".so" + "\"\n";

				generator += "#include \"PetriDynamicLib.h\"\n";

				generator += "#endif"; // ifndef header guard

				System.IO.File.WriteAllText(System.IO.Path.Combine(fc.Filename, settings.Name) + ".h", generator.Value);
			
				string old = settings.OutputPath;
				this.settings.OutputPath = fc.Filename;
				Modified = old != settings.OutputPath;
			}

			fc.Destroy();
		}

		public void Compile() {
			var c = new CppCompiler(this);
			var o = c.Compile();
			if(o != "") {
				// TODO: manage errors output
			}
		}

		public void ManageHeaders() {
			if(headersManager == null) {
				headersManager = new HeadersManager(this);
			}

			headersManager.Show();
		}

		public UInt64 LastEntityID {
			get;
			set;
		}

		public void ResetID() {
			this.LastEntityID = 0;
		}

		public DocumentSettings Settings {
			get {
				return settings;
			}
		}

		public UndoManager UndoManager {
			get;
			private set;
		}

		private void UpdateUndo() {
			// If we fall back to the state we consider unmodified, let it be considered so
			Modified = UndoManager.NextUndo != this.guiActionToMatchSave;

			Window.RevertItem.Sensitive = this.Modified;

			this.UpdateMenuItems();

			(Window.UndoItem.Child as Label).Text = "Annuler" + (UndoManager.NextUndo != null ? " " + UndoManager.NextUndoDescription : "");
			(Window.RedoItem.Child as Label).Text = "Rétablir" + (UndoManager.NextRedo != null ? " " + UndoManager.NextRedoDescription : "");
		}

		public void UpdateMenuItems() {
			CurrentController.UpdateMenuItems();
			Window.UndoItem.Sensitive = UndoManager.NextUndo != null;
			Window.RedoItem.Sensitive = UndoManager.NextRedo != null;
		}

		GuiAction guiActionToMatchSave = null;
		HeadersManager headersManager;
		DocumentSettings settings;
		bool modified;
	}
}

