using System;
using Gtk;

namespace Petri.Editor.GUI
{
    public class CompilationErrorPresenter
    {
        public CompilationErrorPresenter(Document doc, string error)
        {
            _document = doc;

            _window = new Window(WindowType.Toplevel);
            _window.Title = Configuration.GetLocalized("{0} compilation output", doc.Window.Title);

            _window.DefaultWidth = 600;
            _window.DefaultHeight = 400;

            _window.SetPosition(WindowPosition.Center);
            int x, y;
            _window.GetPosition(out x, out y);
            _window.Move(x, 2 * y / 3);
            _window.BorderWidth = 15;
            _window.AllowShrink = true;

            ScrolledWindow scrolledWindow = new ScrolledWindow();
            scrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);

            Viewport viewport = new Viewport();

            var tagTable = new TextTagTable();
            var tag = new TextTag("mytag");
            tagTable.Add(tag);
            var buf = new TextBuffer(tagTable);
            tag.Family = "Monospace";


            TextView view = new TextView(buf);
            view.Editable = false;
            view.Buffer.Text = error;
            buf.ApplyTag("mytag", buf.StartIter, buf.EndIter);

            viewport.Add(view);

            view.SizeRequested += (o, args) => {
                viewport.WidthRequest = viewport.Child.Requisition.Width;
                viewport.HeightRequest = viewport.Child.Requisition.Height;
            };

            scrolledWindow.Add(viewport);

            var hbox = new HBox(false, 0);
            hbox.PackStart(scrolledWindow, true, true, 0);
            _window.Add(hbox);

            _window.DeleteEvent += (o, args) => this.Hide();
        }

        public void Show()
        {
            _window.ShowAll();
            _window.Present();
            _document.AssociatedWindows.Add(_window);
        }

        public void Hide()
        {
            _document.AssociatedWindows.Remove(_window);
            _window.Hide();
        }

        Window _window;
        Document _document;
    }
}

