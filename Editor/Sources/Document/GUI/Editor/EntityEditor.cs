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
using System.Collections.Generic;
using Gtk;
using System.Linq;
using Petri.Editor.Code;

namespace Petri.Editor.GUI.Editor
{
    public abstract class EntityEditor : PaneEditor
    {
        public static EntityEditor GetEditor(Entity e, Document doc)
        {
            EntityEditor editor;

            if(e is Action) {
                editor = new ActionEditor(e as Action, doc);
            }
            else if(e is Transition) {
                editor = new TransitionEditor(e as Transition, doc);
            }
            else if(e is Comment) {
                editor = new CommentEditor(e as Comment, doc);
            }
            else if(e is InnerPetriNet) {
                editor = new InnerPetriNetEditor(e as InnerPetriNet, doc);
            }
            else if(e is ExitPoint) {
                editor = new ExitPointEditor(e as ExitPoint, doc);
            }
            else if(doc.Window.EditorGui.View.MultipleSelection) {
                editor = new MultipleEditor(doc);
            }
            else {
                editor = new EmptyEditor(doc);
            }

            editor.FormatAndShow();

            return editor;
        }

        public Entity Entity {
            get;
            private set;
        }

        protected EntityEditor(Entity e, Document doc) : base(doc, doc.Window.EditorGui.Editor)
        {
            Entity = e;

            if(!_document.Window.EditorGui.View.MultipleSelection && e != null) {
                var label = CreateLabel(0,
                                        Configuration.GetLocalized("Entity's ID:") + " " + e.ID.ToString());
                label.Markup = "<span color=\"grey\">" + label.Text + "</span>";
            }
        }

        /// <summary>
        /// Displays the entity's compilation error if any
        /// </summary>
        protected void AddErrorIfAny()
        {
            if(_document.Conflicts(Entity)) {
                CreateLabel(0, Configuration.GetLocalized("Compilation errors:"));

                var buf = new TextBuffer(new TextTagTable());

                if(Entity is PetriNet) {
                    buf.Text = Configuration.GetLocalized("Some entities contained in this petri net are causing compilation errors.");
                }
                else {
                    buf.Text = _document.Conflicting[Entity];
                }
                var error = CreateWidget<TextView>(true, 0, buf);
                error.SetSizeRequest(200, 100);
                error.WrapMode = WrapMode.Word;
                error.Editable = false;
            }
        }
    }

    public class ActionEditor : EntityEditor
    {
        enum ActionType
        {
            Nothing,
            Print,
            Pause,
            Manual,
            Invocation
        }

        public ActionEditor(Action a, Document doc) : base(a, doc)
        {
            CreateLabel(0, Configuration.GetLocalized("State's name:"));
            var name = CreateWidget<Entry>(true, 0, a.Name);
            Application.RegisterValidation(name, true, (obj, p) => {
                _document.CommitGuiAction(new ChangeNameAction(a, (obj as Entry).Text));
            });

            var active = CreateWidget<CheckButton>(false,
                                                   0,
                                                   Configuration.GetLocalized("Active on t=0:"));
            active.Active = a.Active;
            active.Toggled += (sender, e) => {
                _document.CommitGuiAction(new ToggleActiveAction(a));
            };

            if(a.TransitionsBefore.Count > 0) {
                CreateLabel(0, Configuration.GetLocalized("Required tokens to enter the state:"));
                var list = new List<string>();
                for(int i = 0; i < a.TransitionsBefore.Count; ++i) {
                    list.Add((i + 1).ToString());
                }

                ComboBox tokensChoice = ComboHelper(list[a.RequiredTokens - 1], list);
                this.AddWidget(tokensChoice, false, 0);

                tokensChoice.Changed += (object sender, EventArgs e) => {
                    ComboBox combo = sender as ComboBox;

                    TreeIter iter;

                    if(combo.GetActiveIter(out iter)) {
                        var val = combo.Model.GetValue(iter, 0) as string;
                        int nbTok = int.Parse(val);
                        _document.CommitGuiAction(new ChangeRequiredTokensAction(a, nbTok));
                    }
                };
            }

            // Manage code invocation
            {
                CreateLabel(0, Configuration.GetLocalized("Associated action:"));

                var editorFields = new List<Widget>();

                var list = new List<string>();
                string nothingFunction = Configuration.GetLocalized("Do Nothing");
                string printFunction = Configuration.GetLocalized("Show state's ID and Name");
                string pauseFunction = Configuration.GetLocalized("Sleep");
                string manual = Configuration.GetLocalized("Manual…");
                list.Add(nothingFunction);
                list.Add(printFunction);
                list.Add(pauseFunction);
                list.Add(manual);
                foreach(var func in _document.CodeActions) {
                    if(func.Signature != RuntimeFunctions.DoNothingFunction(a.Document).Signature && func.Signature != RuntimeFunctions.PrintFunction(a.Document).Signature && func.Signature != RuntimeFunctions.PauseFunction(a.Document).Signature && func.ReturnType.Equals(_document.Settings.Enum.Type))
                        list.Add(func.Signature);
                }

                ActionType actionType = a.Function.Function.Signature == RuntimeFunctions.DoNothingFunction(a.Document).Signature ? ActionType.Nothing : a.Function.Function.Signature == RuntimeFunctions.PauseFunction(a.Document).Signature ? ActionType.Pause : a.Function.Function.Signature == RuntimeFunctions.PrintFunction(a.Document).Signature ? ActionType.Print : !(a.Function is WrapperFunctionInvocation) && !a.Function.NeedsExpansion && list.Contains(a.Function.Function.Signature) ? ActionType.Invocation : ActionType.Manual;
                string activeFunction = manual;
                if(actionType == ActionType.Nothing) {
                    activeFunction = nothingFunction;
                }
                else if(actionType == ActionType.Print) {
                    activeFunction = printFunction;
                }
                else if(actionType == ActionType.Pause) {
                    activeFunction = pauseFunction;
                }
                else if(actionType == ActionType.Invocation) {
                    activeFunction = a.Function.Function.Signature;
                }
					
                ComboBox funcList = ComboHelper(activeFunction, list);
                this.AddWidget(funcList, true, 0);
                funcList.Changed += (object sender, EventArgs e) => {
                    ComboBox combo = sender as ComboBox;

                    TreeIter iter;

                    if(combo.GetActiveIter(out iter)) {
                        var val = combo.Model.GetValue(iter, 0) as string;
                        if(val == nothingFunction) {
                            _document.CommitGuiAction(new InvocationChangeAction(a,
                                                                                 new FunctionInvocation(a.Document.Settings.Language,
                                                                                                        RuntimeFunctions.DoNothingFunction(a.Document))));
                            actionType = ActionType.Nothing;
                        }
                        else if(val == printFunction) {
                            _document.CommitGuiAction(new InvocationChangeAction(a,
                                                                                 a.PrintAction()));
                            actionType = ActionType.Print;
                        }
                        else if(val == pauseFunction) {
                            _document.CommitGuiAction(new InvocationChangeAction(a,
                                                                                 new FunctionInvocation(a.Document.Settings.Language,
                                                                                                        RuntimeFunctions.PauseFunction(a.Document),
                                                                                                        LiteralExpression.CreateFromString("1s",
                                                                                                                                           a.Document.Settings.Language))));
                            actionType = ActionType.Pause;
                        }
                        else if(val == manual) {
                            actionType = ActionType.Manual;
                        }
                        else {
                            actionType = ActionType.Invocation;

                            var f = _document.CodeActions.FirstOrDefault(delegate(Code.Function ff) {
                                return ff.Signature == val;
                            });

                            if(a.Function.Function != f) {
                                var pp = new List<Expression>();
                                for(int i = 0; i < f.Parameters.Count; ++i) {
                                    pp.Add(new EmptyExpression(_document.Settings.Language, true));
                                }
                                FunctionInvocation invocation;
                                if(f is Method) {
                                    invocation = new MethodInvocation(a.Document.Settings.Language,
                                                                      f as Method,
                                                                      new EmptyExpression(a.Document.Settings.Language,
                                                                                          true),
                                                                      false,
                                                                      pp.ToArray());
                                }
                                else {
                                    invocation = new FunctionInvocation(a.Document.Settings.Language,
                                                                        f,
                                                                        pp.ToArray());
                                }
                                _document.CommitGuiAction(new InvocationChangeAction(a, invocation));
                            }
                        }
                        EditInvocation(a, actionType, editorFields);
                    }
                };

                EditInvocation(a, actionType, editorFields);
            }

            AddErrorIfAny();
        }

        private void EditInvocation(Action a, ActionType type, List<Widget> editorFields)
        {
            foreach(var e in editorFields) {
                _objectList.RemoveAll(((Tuple<Widget, int, bool> obj) => obj.Item1 == e));
            }
            editorFields.Clear();

            if(type == ActionType.Manual) {
                var label = CreateLabel(0, Configuration.GetLocalized("State's invocation:"));
                editorFields.Add(label);
                string userReadable;
                if(a.Function.NeedsExpansion) {
                    userReadable = a.Function.Unexpanded;
                }
                else {
                    userReadable = a.Function.MakeUserReadable();
                }

                var invocation = CreateWidget<Entry>(true, 0, userReadable);
                editorFields.Add(invocation);
                Application.RegisterValidation(invocation, false, (obj, p) => {
                    Expression cppExpr = null;
                    FunctionInvocation funcInvocation = null;
                    try {
                        cppExpr = Expression.CreateFromStringAndEntity<Expression>((obj as Entry).Text,
                                                                                   a);
                        if(cppExpr is FunctionInvocation) {
                            funcInvocation = (FunctionInvocation)cppExpr;
                            if(!funcInvocation.Function.ReturnType.Equals(Code.Type.UnknownType(_document.Settings.Language)) && !funcInvocation.Function.ReturnType.Equals(_document.Settings.Enum.Type)) {
                                throw new Exception(Configuration.GetLocalized("Incorrect return type for the function: {0} expected, {1} found.",
                                                                               _document.Settings.Enum.Name,
                                                                               funcInvocation.Function.ReturnType.ToString()));
                            }
                        }
                        else {
                            funcInvocation = new WrapperFunctionInvocation(_document.Settings.Language,
                                                                           _document.Settings.Enum.Type,
                                                                           cppExpr);
                        }
                        _document.CommitGuiAction(new InvocationChangeAction(a, funcInvocation));
                    }
                    catch(Exception ex) {
                        MessageDialog d = new MessageDialog(_document.Window,
                                                            DialogFlags.Modal,
                                                            MessageType.Question,
                                                            ButtonsType.None,
                                                            Application.SafeMarkupFromString(Configuration.GetLocalized("The specified expression is invalid ({0}).",
                                                                                                                        ex.Message)));
                        d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
                        d.Run();
                        d.Destroy();

                        (obj as Entry).Text = userReadable;
                    }
                });
            }
            else if(type == ActionType.Invocation) {
                if(a.Function.Function is Method) {
                    var method = a.Function as MethodInvocation;
                    var editorHeader = CreateLabel(20,
                                                   Configuration.GetLocalized("*this object of type {0}:",
                                                                              method.Function.Enclosing.ToString()));
                    editorFields.Add(editorHeader);

                    var valueEditor = CreateWidget<Entry>(true, 20, method.This.MakeUserReadable());
                    editorFields.Add(valueEditor);
                    Application.RegisterValidation(valueEditor, false, (obj, p) => {
                        try {
                            var args = new List<Expression>();
                            for(int j = 2; j < editorFields.Count; ++j) {
                                Widget w = editorFields[j];
                                if(w.GetType() == typeof(Entry)) {
                                    args.Add(Expression.CreateFromStringAndEntity<Expression>((w as Entry).Text,
                                                                                              a,
                                                                                              false));
                                }
                            }
                            _document.CommitGuiAction(new InvocationChangeAction(a,
                                                                                 new MethodInvocation(_document.Settings.Language,
                                                                                                      method.Function as Method,
                                                                                                      Expression.CreateFromStringAndEntity<Expression>((editorFields[1] as Entry).Text,
                                                                                                                                                       a),
                                                                                                      false,
                                                                                                      args.ToArray())));
                        }
                        catch(Exception ex) {
                            MessageDialog d = new MessageDialog(_document.Window,
                                                                DialogFlags.Modal,
                                                                MessageType.Question,
                                                                ButtonsType.None,
                                                                Application.SafeMarkupFromString(Configuration.GetLocalized("The specified expression is invalid ({0}).",
                                                                                                                            ex.Message)));
                            d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
                            d.Run();
                            d.Destroy();

                            (obj as Entry).Text = method.This.MakeUserReadable();
                        }
                    });
                }
                for(int i = 0; i < a.Function.Function.Parameters.Count; ++i) {
                    EditParameter(a, i, editorFields);
                }
            }
            else if(type == ActionType.Pause) {
                EditParameter(a, 0, editorFields);
            }

            this.FormatAndShow();
        }

        private void EditParameter(Action a, int i, List<Widget> editorFields)
        {
            var p = a.Function.Function.Parameters[i];
            var editorHeader = CreateLabel(20,
                                           Configuration.GetLocalized("Parameter {0} {1} :",
                                                                      p.Type,
                                                                      p.Name));
            editorFields.Add(editorHeader);

            var valueEditor = CreateWidget<Entry>(true,
                                                  20,
                                                  a.Function.Arguments[i].MakeUserReadable());
            editorFields.Add(valueEditor);
            Application.RegisterValidation(valueEditor, false, (obj, ii) => {
                try {
                    var args = new List<Expression>();
                    for(int j = (a.Function.Function is Method) ? 2 : 0; j < editorFields.Count; ++j) {
                        Widget w = editorFields[j];
                        if(w.GetType() == typeof(Entry)) {
                            args.Add(Expression.CreateFromStringAndEntity<Expression>((w as Entry).Text,
                                                                                      a));
                        }
                    }
                    FunctionInvocation invocation;
                    if(a.Function.Function is Method) {
                        invocation = new MethodInvocation(_document.Settings.Language,
                                                          a.Function.Function as Method,
                                                          Expression.CreateFromStringAndEntity<Expression>((editorFields[1] as Entry).Text,
                                                                                                           a),
                                                          false,
                                                          args.ToArray());
                    }
                    else {
                        invocation = new FunctionInvocation(_document.Settings.Language,
                                                            a.Function.Function,
                                                            args.ToArray());
                    }
                    _document.CommitGuiAction(new InvocationChangeAction(a, invocation));
                }
                catch(Exception ex) {
                    MessageDialog d = new MessageDialog(_document.Window,
                                                        DialogFlags.Modal,
                                                        MessageType.Question,
                                                        ButtonsType.None,
                                                        Application.SafeMarkupFromString(Configuration.GetLocalized("The specified expression is invalid ({0}).",
                                                                                                                    ex.Message)));
                    d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
                    d.Run();
                    d.Destroy();
                    (obj as Entry).Text = a.Function.Arguments[(int)(ii[0])].MakeUserReadable();
                }
            }, new object[]{ i });
        }
    }

    public class CommentEditor : EntityEditor
    {
        public CommentEditor(Comment c, Document doc) : base(c, doc)
        {
            CreateLabel(0, Configuration.GetLocalized("Color:"));
            _colors = new List<Cairo.Color>();
            _colors.Add(new Cairo.Color(1, 1, 0.7));
            _colors.Add(new Cairo.Color(1, 0.7, 0.7));
            _colors.Add(new Cairo.Color(0.7, 1, 0.7));
            _colors.Add(new Cairo.Color(0.7, 0.7, 1));
            _colors.Add(new Cairo.Color(1, 0.7, 1));
            _colorNames = new List<String>();
            _colorNames.Add(Configuration.GetLocalized("Yellow"));
            _colorNames.Add(Configuration.GetLocalized("Red"));
            _colorNames.Add(Configuration.GetLocalized("Green"));
            _colorNames.Add(Configuration.GetLocalized("Blue"));
            _colorNames.Add(Configuration.GetLocalized("Pink"));
            _colorNames.Add(Configuration.GetLocalized("Manual…"));

            int colorIndex = _colors.FindIndex(((Cairo.Color obj) => {
                return obj.R == c.Color.R && obj.G == c.Color.G && obj.B == c.Color.B;
            }));
            if(colorIndex == -1) {
                colorIndex = _colorNames.Count - 1;
            }

            var colorList = ComboHelper(_colorNames[colorIndex], _colorNames);
            colorList.Changed += (object sender, EventArgs e) => {
                ComboBox combo = sender as ComboBox;

                TreeIter iter;

                if(combo.GetActiveIter(out iter)) {
                    var val = combo.Model.GetValue(iter, 0) as string;
                    this.EditColor(c, val, true);
                }
            };
            this.AddWidget(colorList, false, 0);

            _button = new ColorButton();
            _button.ColorSet += (object sender, EventArgs e) => {
                var newColor = (sender as ColorButton).Color;
                _document.CommitGuiAction(new ChangeCommentColorAction(c,
                                                                       new Cairo.Color(newColor.Red / 65535.0,
                                                                                       newColor.Green / 65535.0,
                                                                                       newColor.Blue / 65535.0)));
            };

            this.EditColor(c, _colorNames[colorIndex], false);

            CreateLabel(0, Configuration.GetLocalized("Comment:"));

            var buf = new TextBuffer(new TextTagTable());
            buf.Text = c.Name;
            var comment = CreateWidget<TextView>(true, 0, buf);
            comment.SetSizeRequest(200, 400);
            comment.WrapMode = WrapMode.Word;

            comment.FocusOutEvent += (obj, eventInfo) => {
                _document.CommitGuiAction(new ChangeNameAction(c, (obj as TextView).Buffer.Text));
            };
        }

        protected void EditColor(Comment comment, string color, bool changed)
        {
            if(color == Configuration.GetLocalized("Manual…")) {
                int index = _objectList.FindIndex(obj => {
                    return obj.Item1 is Label && (obj.Item1 as Label).Text == Configuration.GetLocalized("Color:");
                });
                _objectList.Insert(index + 2, Tuple.Create(_button as Widget, 20, false));
                _button.Color = new Gdk.Color((byte)(comment.Color.R * 255),
                                              (byte)(comment.Color.G * 255),
                                              (byte)(comment.Color.B * 255));
            }
            else {
                int index = _objectList.FindIndex(obj => {
                    return obj.Item1 == _button;
                });
                if(index != -1) {
                    _objectList.RemoveAt(index);
                }

                if(changed) {
                    _document.CommitGuiAction(new ChangeCommentColorAction(comment,
                                                                           _colors[_colorNames.IndexOf(color)]));
                }
            }
            _document.Window.EditorGui.View.Redraw();
            this.FormatAndShow();
        }

        List<string> _colorNames;
        List<Cairo.Color> _colors;
        ColorButton _button;
    }

    public class TransitionEditor : EntityEditor
    {
        public TransitionEditor(Transition t, Document doc) : base(t, doc)
        {
            CreateLabel(0, Configuration.GetLocalized("Transition's name:"));
            var name = CreateWidget<Entry>(true, 0, t.Name);
            Application.RegisterValidation(name, true, (obj, p) => {
                _document.CommitGuiAction(new ChangeNameAction(t, (obj as Entry).Text));
            });

            CreateLabel(0, Configuration.GetLocalized("Transition's condition:"));
            string userReadable;
            if(t.Condition.NeedsExpansion) {
                userReadable = t.Condition.Unexpanded;
            }
            else {
                userReadable = t.Condition.MakeUserReadable();
            }
            var condition = CreateWidget<Entry>(true, 0, userReadable);
            Application.RegisterValidation(condition, false, (obj, p) => {
                try {
                    var cond = new ConditionChangeAction(t,
                                                         Expression.CreateFromStringAndEntity<Expression>((obj as Entry).Text,
                                                                                                          t));
                    _document.CommitGuiAction(cond);
                }
                catch(Exception e) {
                    MessageDialog d = new MessageDialog(_document.Window,
                                                        DialogFlags.Modal,
                                                        MessageType.Question,
                                                        ButtonsType.None,
                                                        Application.SafeMarkupFromString(Configuration.GetLocalized("The specified condition is invalid ({0}).",
                                                                                                                    e.Message)));
                    d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
                    d.Run();
                    d.Destroy();

                    (obj as Entry).Text = userReadable;
                }
            });

            AddErrorIfAny();
        }
    }

    public class InnerPetriNetEditor : EntityEditor
    {
        public InnerPetriNetEditor(InnerPetriNet i, Document doc) : base(i, doc)
        {
            CreateLabel(0, Configuration.GetLocalized("Graph's name:"));
            var name = CreateWidget<Entry>(true, 0, i.Name);
            Application.RegisterValidation(name, true, (obj, p) => {
                _document.CommitGuiAction(new ChangeNameAction(i, (obj as Entry).Text));
            });

            var active = CreateWidget<CheckButton>(false, 0, "Active on t=0:");
            active.Active = i.Active;
            active.Toggled += (sender, e) => {
                _document.CommitGuiAction(new ToggleActiveAction(i));
            };

            AddErrorIfAny();
        }
    }

    public class ExitPointEditor : EntityEditor
    {
        public ExitPointEditor(ExitPoint e, Document doc) : base(e, doc)
        {
            CreateLabel(0, Configuration.GetLocalized("Exit point of the graph"));
        }
    }

    public class MultipleEditor : EntityEditor
    {
        public MultipleEditor(Document doc) : base(null, doc)
        {
            CreateLabel(0, Configuration.GetLocalized("Select only one object"));
        }
    }

    public class EmptyEditor : EntityEditor
    {
        public EmptyEditor(Document doc) : base(null, doc)
        {
            CreateLabel(0, Configuration.GetLocalized("Select an object to edit"));
        }
    }

}

