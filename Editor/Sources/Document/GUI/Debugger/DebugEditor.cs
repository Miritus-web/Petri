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

namespace Petri.Editor.GUI.Debugger
{
    public class DebugEditor : PaneEditor
    {
        public DebugEditor(Document doc, Entity selected) : base(doc, doc.Window.DebugGui.Editor)
        {
            if(selected != null) {
                var label = CreateLabel(0,
                                        Configuration.GetLocalized("Entity's ID:") + " " + selected.ID.ToString());
                label.Markup = "<span color=\"grey\">" + label.Text + "</span>";
            }
            if(selected is Transition) {
                CreateLabel(0, Configuration.GetLocalized("Transition's condition:"));
                Entry e = CreateWidget<Entry>(true,
                                              0,
                                              ((Transition)selected).Condition.MakeUserReadable());
                e.IsEditable = false;
            }
            else if(selected is Action) {
                CreateLabel(0, Configuration.GetLocalized("State's action:"));
                Entry ee = CreateWidget<Entry>(true,
                                               0,
                                               ((Action)selected).Function.MakeUserReadable());
                ee.IsEditable = false;

                var active = CreateWidget<CheckButton>(false,
                                                       0,
                                                       Configuration.GetLocalized("Breakpoint on the state"));
                active.Active = _document.DebugController.Breakpoints.Contains((Action)selected);
                active.Toggled += (sender, e) => {
                    if(_document.DebugController.Breakpoints.Contains((Action)selected)) {
                        _document.DebugController.RemoveBreakpoint((Action)selected);
                    }
                    else {
                        _document.DebugController.AddBreakpoint((Action)selected);
                    }

                    _document.Window.DebugGui.View.Redraw();
                };
            }

            CreateLabel(0, Configuration.GetLocalized("Evaluate expression:"));
            string[] lastEvaluations = (_document.DebugController != null) ? _document.DebugController.LastEvaluations.ToArray() : new string[]{};
            var combo = CreateWidget<ComboBoxEntry>(true, 0, new object[]{ lastEvaluations });
            if(_document.DebugController != null && _document.DebugController.LastEvaluations.Count > 0) {
                combo.Entry.Text = _document.DebugController.LastEvaluations[0];
            }

            if(_document.Settings?.Language == Code.Language.C) {
                CreateLabel(0, Configuration.GetLocalized("With printf format:"));
                _formatEntry = CreateWidget<Entry>(true, 0, "%d");
            }
            Evaluate = CreateWidget<Button>(false, 0, Configuration.GetLocalized("Evaluate"));
            Evaluate.Sensitive = _document.DebugController != null
                && (_document.DebugController.Client.CurrentSessionState == DebugClient.SessionState.Started)
                && (_document.DebugController.Client.CurrentPetriState == DebugClient.PetriState.Stopped || _document.DebugController.Client.CurrentPetriState == DebugClient.PetriState.Paused);
           
            CreateLabel(0, Configuration.GetLocalized("Result:"));

            _buf = new TextBuffer(new TextTagTable());
            _buf.Text = "";
            var result = CreateWidget<TextView>(true, 0, _buf);
            result.Editable = false;
            result.WrapMode = WrapMode.Word;

            Evaluate.Clicked += (sender, ev) => {
                if((_document.DebugController.Client.CurrentSessionState == DebugClient.SessionState.Started)
                   && (_document.DebugController.Client.CurrentPetriState == DebugClient.PetriState.Stopped || _document.DebugController.Client.CurrentPetriState == DebugClient.PetriState.Paused)) {
                    string str = combo.Entry.Text;

                    int pos = _document.DebugController.LastEvaluations.IndexOf(str);
                    if(pos != -1) {
                        _document.DebugController.LastEvaluations.RemoveAt(pos);
                        combo.RemoveText(pos);
                    }
                    _document.DebugController.LastEvaluations.Insert(0, str);
                    combo.PrependText(str);

                    try {
                        Code.Expression expr = Code.Expression.CreateFromStringAndEntity<Code.Expression>(str,
                                                                                                          _document.PetriNet);

                        object[] userData = null;
                        if(_document.Settings.Language == Code.Language.C) {
                            userData = new object[] { _formatEntry.Text };
                        }
                        _document.DebugController.Client.Evaluate(expr, userData);
                    }
                    catch(Exception e) {
                        _buf.Text = e.Message;
                    }
                }
            };

            this.FormatAndShow();
        }

        public void OnEvaluate(string result)
        {
            _buf.Text = result;
        }

        public Button Evaluate {
            get;
            private set;
        }

        TextBuffer _buf;
        Entry _formatEntry;
    }
}

