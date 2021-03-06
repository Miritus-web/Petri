/*
 * Copyright (c) 2015 Rémi Saurel
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using Gtk;
using System.Text.RegularExpressions;
using System.Linq;

namespace Petri.Editor.GUI
{
    public class DocumentSettingsEditor
    {
        public DocumentSettingsEditor(Document doc)
        {
            _document = doc;

            _window = new Window(WindowType.Toplevel);
            _window.Title = Configuration.GetLocalized("Document's settings:") + " " + doc.Window.Title;

            _window.DefaultWidth = 400;
            _window.DefaultHeight = 600;
            _window.SetSizeRequest(300, 600);

            _window.SetPosition(WindowPosition.Center);
            int x, y;
            _window.GetPosition(out x, out y);
            _window.Move(x, 2 * y / 3);
            _window.BorderWidth = 15;

            var vbox = new VBox(false, 5);
            ScrolledWindow scrolledWindow = new ScrolledWindow();
            scrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);

            Viewport viewport = new Viewport();

            viewport.Add(vbox);

            scrolledWindow.Add(viewport);
            _window.Add(scrolledWindow);

            {
                _languageCombo = ComboBox.NewText();

                foreach(Code.Language l in Enum.GetValues(typeof(Code.Language))) {
                    _languageCombo.AppendText(DocumentSettings.LanguageName(l));
                }

                _languageCombo.Changed += (object sender, EventArgs e) => {
                    if(_updating) {
                        return;
                    }

                    TreeIter it;

                    if(_languageCombo.GetActiveIter(out it)) {
                        var newSettings = _document.Settings.Clone();
                        newSettings.Language = (Code.Language)int.Parse(_languageCombo.Model.GetStringFromIter(it));
                        _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                        UpdateGUIForLanguage();
                    }
                };
                _runInEditor = new CheckButton(Configuration.GetLocalized("Run the Petri net in the editor"));
                _runInEditor.Toggled += (sender, e) => {
                    if(_updating) {
                        return;
                    }

                    var newSettings = _document.Settings.Clone();
                    newSettings.RunInEditor = _runInEditor.Active;
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                };

                _labelName = new Label(Configuration.GetLocalized("<language> name of the Petri net:",
                                                                  _document.Settings.LanguageName()));

                _nameEntry = new Entry();
                Application.RegisterValidation(_nameEntry, false, (obj, p) => {
                    if(_updating) {
                        return;
                    }

                    Regex name = new Regex(Code.Parser.GetNamePattern(true));
                    Match nameMatch = name.Match((obj as Entry).Text);

                    if(!nameMatch.Success || nameMatch.Value != (obj as Entry).Text) {
                        MessageDialog d = new MessageDialog(_window,
                                                            DialogFlags.Modal,
                                                            MessageType.Error,
                                                            ButtonsType.None,
                                                            Configuration.GetLocalized("The Petri net's name is not a valid <language> identifier.",
                                                                                       _document.Settings.LanguageName()));
                        d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
                        d.Run();
                        d.Destroy();

                        (obj as Entry).Text = _document.Settings.Name;
                    }
                    else {
                        var newSettings = _document.Settings.Clone();
                        newSettings.Name = (obj as Entry).Text;
                        _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                    }
                });

                vbox.PackStart(_languageCombo, false, false, 0);
                vbox.PackStart(_runInEditor, false, false, 0);
                var hbox = new HBox(false, 5);
                hbox.PackStart(_labelName, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);
                vbox.PackStart(_nameEntry, false, false, 0);

                Label labelEnum = new Label(Configuration.GetLocalized("Enum \"Action Result\":"));
                _customEnumEditor = new Entry("");

                Application.RegisterValidation(_customEnumEditor, false, (obj, p) => {
                    if(_updating) {
                        return;
                    }

                    try {
                        Code.Enum e = new Code.Enum(_document.Settings.Language,
                                                    (obj as Entry).Text);
                        var newSettings = _document.Settings.Clone();
                        newSettings.Enum = e;
                        _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                    }
                    catch(Exception) {
                        MessageDialog d = new MessageDialog(_window,
                                                            DialogFlags.Modal,
                                                            MessageType.Error,
                                                            ButtonsType.None,
                                                            Configuration.GetLocalized("Invalid name for the enum or one of its values."));
                        d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
                        d.Run();
                        d.Destroy();

                        (obj as Entry).Text = _document.Settings.Enum.ToString();
                        if(_document.Settings.Enum.Equals(_document.Settings.DefaultEnum)) {
                            _defaultEnum.Active = true;
                            _customEnum.Active = false;
                            ((Entry)obj).Sensitive = false;
                        }
                    }
                });

                var radioVBox = new VBox(true, 2);
                _defaultEnum = new RadioButton(Configuration.GetLocalized("Use the default enum (ActionResult)"));
                _defaultEnum.Toggled += (object sender, EventArgs e) => {
                    if(_updating) {
                        return;
                    }

                    if((sender as RadioButton).Active) {
                        _customEnumEditor.Sensitive = false;
                        _customEnumEditor.Text = "";
                        var newSettings = _document.Settings.Clone();
                        newSettings.Enum = _document.Settings.DefaultEnum;
                        _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                    }
                };
                _customEnum = new RadioButton(_defaultEnum,
                                              Configuration.GetLocalized("Use the following enum (name, value1, value2…):"));
                _customEnum.Toggled += (object sender, EventArgs e) => {
                    if(_updating) {
                        return;
                    }

                    if((sender as RadioButton).Active) {
                        _customEnumEditor.Sensitive = true;
                        _customEnumEditor.Text = _document.Settings.Enum.ToString();
                    }
                };
                radioVBox.PackStart(_defaultEnum, true, true, 2);
                radioVBox.PackStart(_customEnum, true, true, 2);

                hbox = new HBox(false, 5);
                hbox.PackStart(labelEnum, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);
                vbox.PackStart(radioVBox, false, false, 0);
                vbox.PackStart(_customEnumEditor, false, false, 0);

                _labelCompiler = new Label(Configuration.GetLocalized("Path to the <language> compiler:",
                                                                      _document.Settings.LanguageName()));

                _compilerEntry = new Entry();
                Application.RegisterValidation(_compilerEntry, false, (obj, p) => {
                    if(_updating) {
                        return;
                    }

                    var newSettings = _document.Settings.Clone();
                    newSettings.Compiler = (obj as Entry).Text;
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                });

                hbox = new HBox(false, 5);
                hbox.PackStart(_labelCompiler, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);
                vbox.PackStart(_compilerEntry, false, false, 0);

                _labelFlags = new Label(Configuration.GetLocalized("Flags forwarded to the <language> compiler:",
                                                                   _document.Settings.LanguageName()));

                _compilerFlags = new Entry();
                Application.RegisterValidation(_compilerFlags, false, (obj, p) => {
                    if(_updating) {
                        return;
                    }

                    var newSettings = _document.Settings.Clone();
                    newSettings.CompilerFlags.Clear();
                    newSettings.CompilerFlags.AddRange((obj as Entry).Text.Split(new char[]{ ' ' },
                                                                                 StringSplitOptions.RemoveEmptyEntries));
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                });

                hbox = new HBox(false, 5);
                hbox.PackStart(_labelFlags, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);
                vbox.PackStart(_compilerFlags, false, false, 0);

                var outputLabel = new Label(Configuration.GetLocalized("Output path for the generated code (relative to the document):"));
                _sourceOutputPath = new Entry();
                Application.RegisterValidation(_sourceOutputPath, false, (obj, p) => {
                    if(_updating) {
                        return;
                    }

                    var newSettings = _document.Settings.Clone();
                    newSettings.RelativeSourceOutputPath = (obj as Entry).Text;
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                });

                hbox = new HBox(false, 5);
                hbox.PackStart(outputLabel, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);

                _selectSourceOutputPath = new Button("…");
                _selectSourceOutputPath.Clicked += OnAdd;

                hbox = new HBox(false, 5);
                hbox.PackStart(_sourceOutputPath, true, true, 0);
                hbox.PackStart(_selectSourceOutputPath, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);

                outputLabel = new Label(Configuration.GetLocalized("Output path for the dynamic library (relative to the document):"));
                _libOutputPath = new Entry();
                Application.RegisterValidation(_libOutputPath, false, (obj, p) => {
                    if(_updating) {
                        return;
                    }

                    var newSettings = _document.Settings.Clone();
                    newSettings.RelativeLibOutputPath = (obj as Entry).Text;
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                });

                hbox = new HBox(false, 5);
                hbox.PackStart(outputLabel, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);

                _selectLibOutputPath = new Button("…");
                _selectLibOutputPath.Clicked += OnAdd;

                hbox = new HBox(false, 5);
                hbox.PackStart(_libOutputPath, true, true, 0);
                hbox.PackStart(_selectLibOutputPath, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);

                var label = new Label(Configuration.GetLocalized("Host name for the debugger:"));
                _hostnameEntry = new Entry(_document.Settings.Hostname);
                Application.RegisterValidation(_hostnameEntry, false, (obj, p) => {
                    if(_updating) {
                        return;
                    }

                    var newSettings = _document.Settings.Clone();
                    newSettings.Hostname = (obj as Entry).Text;
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                });

                hbox = new HBox(false, 5);
                hbox.PackStart(label, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);
                vbox.PackStart(_hostnameEntry, false, false, 0);

                label = new Label(Configuration.GetLocalized("TCP Port for the debugger communication:"));
                _portEntry = new Entry(_document.Settings.Port.ToString());
                Application.RegisterValidation(_portEntry, false, (obj, p) => {
                    if(_updating) {
                        return;
                    }

                    try {
                        var newSettings = _document.Settings.Clone();
                        newSettings.Port = UInt16.Parse((obj as Entry).Text);
                        _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                    }
                    catch(Exception) {
                        (obj as Entry).Text = _document.Settings.Port.ToString();
                    }
                });

                hbox = new HBox(false, 5);
                hbox.PackStart(label, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);
                vbox.PackStart(_portEntry, false, false, 0);
            }

            {
                _headersSearchPathBox = new VBox();
                var hbox = new HBox(false, 5);
                Label label = new Label(Configuration.GetLocalized("Headers search paths:"));
                hbox.PackStart(label, false, false, 0);
                _headersSearchPathBox.PackStart(hbox, false, false, 0);

                _headersSearchPath = new TreeView();
                TreeViewColumn c = new TreeViewColumn();
                c.Title = Configuration.GetLocalized("Path");
                var pathCell = new Gtk.CellRendererText();
                pathCell.Editable = true;
                pathCell.Edited += (object o, EditedArgs args) => {
                    if(_updating) {
                        return;
                    }

                    var tup = _document.Settings.IncludePaths[int.Parse(args.Path)];

                    var newSettings = _document.Settings.Clone();
                    newSettings.IncludePaths[int.Parse(args.Path)] = Tuple.Create(args.NewText,
                                                                                  tup.Item2);
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));

                    this.BuildHeadersSearchPath();
                };

                c.PackStart(pathCell, true);
                c.AddAttribute(pathCell, "text", 0);
                _headersSearchPath.AppendColumn(c);

                c = new TreeViewColumn();
                c.Title = "Recursive";
                var recursivityCell = new Gtk.CellRendererToggle();
                recursivityCell.Toggled += (object o, ToggledArgs args) => {
                    if(_updating) {
                        return;
                    }

                    var tup = _document.Settings.IncludePaths[int.Parse(args.Path)];

                    var newSettings = _document.Settings.Clone();
                    newSettings.IncludePaths[int.Parse(args.Path)] = Tuple.Create(tup.Item1,
                                                                                  !tup.Item2);
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));

                    this.BuildHeadersSearchPath();
                };
                c.PackStart(recursivityCell, true);
                c.AddAttribute(recursivityCell, "active", 1);
                _headersSearchPath.AppendColumn(c);

                _headersSearchPathStore = new Gtk.ListStore(typeof(string), typeof(bool));
                _headersSearchPath.Model = _headersSearchPathStore;

                _headersSearchPathBox.PackStart(_headersSearchPath, true, true, 0);

                hbox = new HBox(false, 5);
                _addHeaderSearchPath = new Button(new Label("+"));
                _removeHeaderSearchPath = new Button(new Label("-"));
                _addHeaderSearchPath.Clicked += OnAdd;
                _removeHeaderSearchPath.Clicked += OnRemove;
                hbox.PackStart(_addHeaderSearchPath, false, false, 0);
                hbox.PackStart(_removeHeaderSearchPath, false, false, 0);
                _headersSearchPathBox.PackStart(hbox, false, false, 0);
                vbox.PackStart(_headersSearchPathBox, false, false, 0);
            }

            {
                var hbox = new HBox(false, 5);
                Label label = new Label(Configuration.GetLocalized("Libraries search paths:"));
                hbox.PackStart(label, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);

                _libsSearchPath = new TreeView();
                TreeViewColumn c = new TreeViewColumn();
                c.Title = Configuration.GetLocalized("Path");
                var pathCell = new Gtk.CellRendererText();
                pathCell.Editable = true;
                pathCell.Edited += (object o, EditedArgs args) => {
                    if(_updating) {
                        return;
                    }

                    var tup = _document.Settings.LibPaths[int.Parse(args.Path)];

                    var newSettings = _document.Settings.Clone();
                    newSettings.LibPaths[int.Parse(args.Path)] = Tuple.Create(args.NewText,
                                                                              tup.Item2);
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));

                    this.BuildLibsSearchPath();
                };
                c.PackStart(pathCell, true);
                c.AddAttribute(pathCell, "text", 0);
                _libsSearchPath.AppendColumn(c);

                c = new TreeViewColumn();
                c.Title = Configuration.GetLocalized("Recursive");
                var recursivityCell = new Gtk.CellRendererToggle();
                recursivityCell.Toggled += (object o, ToggledArgs args) => {
                    if(_updating) {
                        return;
                    }

                    var tup = _document.Settings.LibPaths[int.Parse(args.Path)];

                    var newSettings = _document.Settings.Clone();
                    newSettings.LibPaths[int.Parse(args.Path)] = Tuple.Create(tup.Item1,
                                                                              !tup.Item2);
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));

                    this.BuildLibsSearchPath();
                };
                c.PackStart(recursivityCell, true);
                c.AddAttribute(recursivityCell, "active", 1);
                _libsSearchPath.AppendColumn(c);

                _libsSearchPathStore = new Gtk.ListStore(typeof(string), typeof(bool));
                _libsSearchPath.Model = _libsSearchPathStore;

                vbox.PackStart(_libsSearchPath, true, true, 0);

                hbox = new HBox(false, 5);
                _addLibSearchPath = new Button(new Label("+"));
                _removeLibSearchPath = new Button(new Label("-"));
                _addLibSearchPath.Clicked += OnAdd;
                _removeLibSearchPath.Clicked += OnRemove;
                hbox.PackStart(_addLibSearchPath, false, false, 0);
                hbox.PackStart(_removeLibSearchPath, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);
            }

            {
                var hbox = new HBox(false, 5);
                Label label = new Label(Configuration.GetLocalized("Libraries used by the document:"));
                hbox.PackStart(label, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);

                _libs = new TreeView();
                TreeViewColumn c = new TreeViewColumn();
                c.Title = Configuration.GetLocalized("Path");
                var pathCell = new Gtk.CellRendererText();
                pathCell.Editable = true;
                pathCell.Edited += (object o, EditedArgs args) => {
                    if(_updating) {
                        return;
                    }

                    var newSettings = _document.Settings.Clone();
                    newSettings.Libs[int.Parse(args.Path)] = args.NewText;
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));

                    this.BuildLibs();
                };
                c.PackStart(pathCell, true);
                c.AddAttribute(pathCell, "text", 0);
                _libs.AppendColumn(c);

                _libsStore = new Gtk.ListStore(typeof(string));
                _libs.Model = _libsStore;

                vbox.PackStart(_libs, true, true, 0);

                hbox = new HBox(false, 5);
                _addLib = new Button(new Label("+"));
                _removeLib = new Button(new Label("-"));
                _addLib.Clicked += OnAdd;
                _removeLib.Clicked += OnRemove;
                hbox.PackStart(_addLib, false, false, 0);
                hbox.PackStart(_removeLib, false, false, 0);
                vbox.PackStart(hbox, false, false, 0);
            }

            _window.DeleteEvent += this.OnDeleteEvent;
        }

        public void Show()
        {
            UpdateUI();

            _window.Present();
            _document.AssociatedWindows.Add(_window);
        }

        public void UpdateUI()
        {
            _updating = true;

            TreeIter iter;
            _languageCombo.Model.GetIterFirst(out iter);
            _languageCombo.Model.GetIterFirst(out iter);
            do {
                GLib.Value thisRow = new GLib.Value();
                _languageCombo.Model.GetValue(iter, 0, ref thisRow);
                if((thisRow.Val as string).Equals(_document.Settings.LanguageName())) {
                    _languageCombo.SetActiveIter(iter);
                    break;
                }
            } while(_languageCombo.Model.IterNext(ref iter));

            _runInEditor.Active = _document.Settings.RunInEditor;

            _nameEntry.Text = _document.Settings.Name;

            if(_document.Settings.Enum.Equals(_document.Settings.DefaultEnum)) {
                _defaultEnum.Active = true;
                _customEnum.Active = false;
                _customEnumEditor.Sensitive = false;
            }
            else {
                _defaultEnum.Active = false;
                _customEnum.Active = true;
                _customEnumEditor.Sensitive = true;
                _customEnumEditor.Text = _document.Settings.Enum.ToString();
            }

            _compilerEntry.Text = _document.Settings.Compiler;
            _compilerFlags.Text = String.Join(" ", _document.Settings.CompilerFlags);

            _sourceOutputPath.Text = _document.Settings.RelativeSourceOutputPath;
            _libOutputPath.Text = _document.Settings.RelativeLibOutputPath;

            _hostnameEntry.Text = _document.Settings.Hostname;
            _portEntry.Text = _document.Settings.Port.ToString();

            this.BuildHeadersSearchPath();
            this.BuildLibsSearchPath();
            this.BuildLibs();

            UpdateGUIForLanguage();

            _updating = false;
        }

        public void Hide()
        {
            _document.AssociatedWindows.Remove(_window);
            _window.Hide();
        }

        void UpdateGUIForLanguage()
        {
            _labelName.Text = Configuration.GetLocalized("<language> name of the Petri net:",
                                                         _document.Settings.LanguageName());
            _labelCompiler.Text = Configuration.GetLocalized("Path to the <language> compiler:",
                                                             _document.Settings.LanguageName());
            _labelFlags.Text = Configuration.GetLocalized("Flags forwarded to the <language> compiler:",
                                                          _document.Settings.LanguageName());
            
            _window.ShowAll();

            if(_document.Settings.Language == Code.Language.CSharp) {
                _headersSearchPathBox.Hide();
            }

            _document.Window.EditorGui.UpdateGUIForLanguage();
        }

        protected void OnDeleteEvent(object sender, DeleteEventArgs a)
        {
            _window.Hide();
            // We do not close the window so that there is no need to recreate it upon reponing
            a.RetVal = true;
        }

        private void OnAdd(object sender, EventArgs e)
        {
            string title = "";
            FileChooserAction action = FileChooserAction.Open;

            FileFilter filter = null;

            if(sender == _addHeaderSearchPath) {
                title = Configuration.GetLocalized("Select the directory where to search for the headers…");
                action = FileChooserAction.SelectFolder;
            }
            else if(sender == _addLibSearchPath) {
                title = Configuration.GetLocalized("Select the directory where to search for the libraries…");
                action = FileChooserAction.SelectFolder;
            }
            else if(sender == _addLib) {
                title = Configuration.GetLocalized("Select the library…");
                action = FileChooserAction.Open;
                filter = new FileFilter();
                filter.Name = Configuration.GetLocalized("Library");

                filter.AddPattern("*.a");
                filter.AddPattern("*.lib");
                filter.AddPattern("*.so");
                filter.AddPattern("*.dylib");
                filter.AddPattern("*.dll");
            }
            else if(sender == _selectSourceOutputPath) {
                title = Configuration.GetLocalized("Select the directory where to generate the <language> source code…",
                                                   _document.Settings.LanguageName());
                action = FileChooserAction.SelectFolder;
            }
            else if(sender == _selectLibOutputPath) {
                title = Configuration.GetLocalized("Select the directory where to generate the library…");
                action = FileChooserAction.SelectFolder;
            }

            var fc = new Gtk.FileChooserDialog(title, _window,
                                               action,
                                               new object[] {Configuration.GetLocalized("Cancel"), ResponseType.Cancel,
                Configuration.GetLocalized("Open"), ResponseType.Accept
            });
            if(filter != null) {
                fc.AddFilter(filter);
            }

            if(fc.Run() == (int)ResponseType.Accept) {
                string relativePath = _document.GetRelativeToDoc(fc.Filename);
                var newSettings = _document.Settings.Clone();

                if(sender == _addHeaderSearchPath) {
                    newSettings.IncludePaths.Add(Tuple.Create(relativePath, false));
                    this.BuildHeadersSearchPath();
                }
                else if(sender == _addLibSearchPath) {
                    newSettings.LibPaths.Add(Tuple.Create(relativePath, false));
                    this.BuildLibsSearchPath();
                }
                else if(sender == _addLib) {
                    string filename = System.IO.Path.GetFileName(fc.Filename);
                    if(filename.StartsWith("lib")) {
                        filename = filename.Substring(3);
                    }
                    filename = System.IO.Path.GetFileNameWithoutExtension(filename);
                    newSettings.Libs.Add(filename);
                    this.BuildLibs();
                }
                else if(sender == _selectSourceOutputPath) {
                    string filename = _document.GetRelativeToDoc(fc.Filename);
                    _sourceOutputPath.Text = filename;
                    newSettings.RelativeSourceOutputPath = filename;
                }
                else if(sender == _selectLibOutputPath) {
                    string filename = _document.GetRelativeToDoc(fc.Filename);
                    _libOutputPath.Text = filename;
                    newSettings.RelativeLibOutputPath = filename;
                }

                _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
            }
            fc.Destroy();
        }

        protected void OnRemove(object sender, EventArgs e)
        {
            if(sender == _removeHeaderSearchPath) {
                TreeIter iter;
                TreePath[] treePath = _headersSearchPath.Selection.GetSelectedRows();

                for(int i = treePath.Length; i > 0; i--) {
                    _headersSearchPathStore.GetIter(out iter, treePath[(i - 1)]);

                    var newSettings = _document.Settings.Clone();
                    newSettings.IncludePaths.Remove(Tuple.Create(_headersSearchPathStore.GetValue(iter,
                                                                                                  0) as string,
                                                                 (bool)(_headersSearchPathStore.GetValue(iter,
                                                                                                         1))));
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                }

                this.BuildHeadersSearchPath();
            }
            else if(sender == _removeLibSearchPath) {
                TreeIter iter;
                TreePath[] treePath = _libsSearchPath.Selection.GetSelectedRows();

                for(int i = treePath.Length; i > 0; i--) {
                    _libsSearchPathStore.GetIter(out iter, treePath[(i - 1)]);

                    var newSettings = _document.Settings.Clone();
                    newSettings.LibPaths.Remove(Tuple.Create(_libsSearchPathStore.GetValue(iter, 0) as string,
                                                             (bool)(_libsSearchPathStore.GetValue(iter,
                                                                                                  1))));
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                }

                this.BuildLibsSearchPath();
            }
            else if(sender == _removeLib) {
                TreeIter iter;
                TreePath[] treePath = _libs.Selection.GetSelectedRows();

                for(int i = treePath.Length; i > 0; i--) {
                    _libsStore.GetIter(out iter, treePath[(i - 1)]);

                    var newSettings = _document.Settings.Clone();
                    newSettings.Libs.Remove(_libsStore.GetValue(iter, 0) as string);
                    _document.CommitGuiAction(new ChangeSettingsAction(_document, newSettings));
                }

                this.BuildLibs();
            }
        }

        protected void BuildHeadersSearchPath()
        {
            _headersSearchPathStore.Clear();
            foreach(var p in _document.Settings.IncludePaths) {
                _headersSearchPathStore.AppendValues(p.Item1, p.Item2);
            }
            _window.ShowAll();
        }

        protected void BuildLibsSearchPath()
        {
            _libsSearchPathStore.Clear();
            foreach(var p in _document.Settings.LibPaths) {
                _libsSearchPathStore.AppendValues(p.Item1, p.Item2);
            }
        }

        protected void BuildLibs()
        {
            _libsStore.Clear();
            foreach(var p in _document.Settings.Libs) {
                _libsStore.AppendValues(p);
            }
        }

        Window _window;
        Document _document;

        CheckButton _runInEditor;

        RadioButton _defaultEnum, _customEnum;
        Entry _customEnumEditor;

        Entry _libOutputPath, _sourceOutputPath;
        Button _selectLibOutputPath, _selectSourceOutputPath;

        VBox _headersSearchPathBox;
        TreeView _headersSearchPath;
        ListStore _headersSearchPathStore;
        Button _addHeaderSearchPath;
        Button _removeHeaderSearchPath;

        TreeView _libsSearchPath;
        ListStore _libsSearchPathStore;
        Button _addLibSearchPath;
        Button _removeLibSearchPath;

        TreeView _libs;
        ListStore _libsStore;
        Button _addLib;
        Button _removeLib;

        ComboBox _languageCombo;
        Entry _nameEntry;
        Entry _compilerEntry;
        Entry _compilerFlags;
        Entry _hostnameEntry;
        Entry _portEntry;
        Label _labelName;
        Label _labelCompiler;
        Label _labelFlags;

        bool _updating;
    }
}

