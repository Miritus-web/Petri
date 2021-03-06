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

namespace Petri.Editor.GUI
{
    public class MacrosManager
    {
        public MacrosManager(Document doc)
        {
            _document = doc;
            _window = new Window(WindowType.Toplevel);
            _window.Title = Configuration.GetLocalized("Macros associated with") + " " + doc.Window.Title;

            _window.DefaultWidth = 400;
            _window.DefaultHeight = 300;

            _window.SetPosition(WindowPosition.Center);
            int x, y;
            _window.GetPosition(out x, out y);
            _window.Move(x, 2 * y / 3);
            _window.BorderWidth = 15;

            var vbox = new VBox(false, 5);

            _window.Add(vbox);

            _table = new TreeView();

            {
                TreeViewColumn c = new TreeViewColumn();
                c.Title = Configuration.GetLocalized("Name");
                var nameCell = new Gtk.CellRendererText();
                c.PackStart(nameCell, true);
                c.AddAttribute(nameCell, "text", 0);
                _table.AppendColumn(c);
            }

            {
                TreeViewColumn c = new TreeViewColumn();
                c.Title = Configuration.GetLocalized("Value");
                var valueCell = new Gtk.CellRendererText();
                valueCell.Editable = true;
                valueCell.Edited += (object o, EditedArgs args) => {
                    TreeIter iter;
                    _dataStore.GetIterFromString(out iter, args.Path);
                    _document.CommitGuiAction(new ChangeMacroAction(_document, _dataStore.GetValue(iter, 0) as string, args.NewText));
                    this.BuildList();
                };
                c.PackStart(valueCell, true);
                c.AddAttribute(valueCell, "text", 1);
                _table.AppendColumn(c);
            }

            _dataStore = new Gtk.ListStore(typeof(string), typeof(string));
            _table.Model = _dataStore;

            vbox.PackStart(_table, true, true, 0);

            var hbox = new HBox(false, 5);
            var plus = new Button(new Label("+"));
            var minus = new Button(new Label("-"));
            plus.Clicked += OnAdd;
            minus.Clicked += OnRemove;
            hbox.PackStart(plus, false, false, 0);
            hbox.PackStart(minus, false, false, 0);
            vbox.PackStart(hbox, false, false, 0);

            var OK = new Button(new Label("OK"));
            hbox.PackEnd(OK, false, false, 0);
            OK.Clicked += (sender, e) => _window.Hide();

            _window.DeleteEvent += OnDeleteEvent;
        }

        public void Show()
        {
            this.BuildList();
            _window.ShowAll();
            _window.Present();
            _document.AssociatedWindows.Add(_window);
        }

        public void Hide()
        {
            _document.AssociatedWindows.Remove(_window);
            _window.Hide();
        }

        private void BuildList()
        {
            _dataStore.Clear();
            foreach(var m in _document.PreprocessorMacros) {
                _dataStore.AppendValues(m.Key, m.Value);
            }

            _window.ShowAll();
        }

        protected void OnRemove(object sender, EventArgs e)
        {
            TreeIter iter;
            TreePath[] treePath = _table.Selection.GetSelectedRows();

            for(int i = treePath.Length; i > 0; i--) {
                _dataStore.GetIter(out iter, treePath[(i - 1)]);
                MessageDialog d = new MessageDialog(_window,
                                                    DialogFlags.Modal,
                                                    MessageType.Error,
                                                    ButtonsType.None,
                                                    Configuration.GetLocalized("Removing a macro used in the document will make it inconsistent. Be careful!"));
                d.AddButton(Configuration.GetLocalized("Remove"), ResponseType.Accept);
                d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
                if(d.Run() == (int)ResponseType.Accept) {
                    var key = _dataStore.GetValue(iter, 0) as string;
                    _document.CommitGuiAction(new RemoveMacroAction(_document, key));
                }
                d.Destroy();
            }

            this.BuildList();
        }


        protected void OnDeleteEvent(object sender, DeleteEventArgs a)
        {
            _window.Hide();
            // We do not close the window so that there is no need to recreate it upon reopening
            a.RetVal = true;
        }

        private void OnAdd(object sender, EventArgs e)
        {
            MessageDialog d = new MessageDialog(_window,
                                                DialogFlags.Modal,
                                                MessageType.Question,
                                                ButtonsType.None,
                                                Configuration.GetLocalized("Please write down the macro name (cannot be changed later):"));
            d.AddButton(Configuration.GetLocalized("Add the macro"), ResponseType.Accept);
            d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
            Entry entry = new Entry(Configuration.GetLocalized("Name"));
            d.VBox.PackEnd(entry, true, true, 0);
            d.ShowAll();
            if(d.Run() == (int)ResponseType.Accept) {
                _document.CommitGuiAction(new ChangeMacroAction(_document, entry.Text, Configuration.GetLocalized("Value")));
                this.BuildList();
            }
            d.Destroy();
        }

        Document _document;
        Window _window;
        TreeView _table;
        ListStore _dataStore;
    }
}

