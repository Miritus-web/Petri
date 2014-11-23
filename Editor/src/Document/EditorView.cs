﻿using System;
using System.Collections.Generic;
using Gtk;
using Cairo;
using System.Linq;

namespace Petri
{
	public class EditorView : PetriView
	{
		public enum CurrentAction {
			None,
			MovingAction,
			MovingTransition,
			CreatingTransition,
			SelectionRect
		}

		public EditorView(Document doc) : base(doc) {
			currentAction = CurrentAction.None;
		}

		public override void FocusIn() {
			shiftDown = true;
			shiftDown = false;
			ctrlDown = false;
			currentAction = CurrentAction.None;
			base.FocusIn();
			hoveredItem = null;
		}

		public override void FocusOut() {
			shiftDown = false;
			ctrlDown = false;
			currentAction = CurrentAction.None;
			base.FocusOut();
		}

		protected override void ManageTwoButtonPress(Gdk.EventButton ev) {
			if(ev.Button == 1) {
				// Add new action
				if(this.selectedEntities.Count == 0) {
					document.PostAction(new AddStateAction(new Action(this.EditedPetriNet.Document, EditedPetriNet, false, new PointD(ev.X, ev.Y))/*, new List<Transition>()*/));
					hoveredItem = SelectedEntity;
				}
				else if(this.selectedEntities.Count == 1) {
					this.currentAction = CurrentAction.None;

					var selected = this.SelectedEntity as State;

					// Change type from Action to InnerPetriNet
					if(selected != null && selected is Action) {
						MessageDialog d = new MessageDialog(document.Window, DialogFlags.Modal, MessageType.Warning, ButtonsType.None, "Souhaitez-vous vraiment transformer l'action sélectionnée en macro ?");
						d.AddButton("Non", ResponseType.Cancel);
						d.AddButton("Oui", ResponseType.Accept);
						d.DefaultResponse = ResponseType.Accept;

						ResponseType result = (ResponseType)d.Run();

						if(result == ResponseType.Accept) {
							this.ResetSelection();
							var inner = new InnerPetriNet(this.EditedPetriNet.Document, this.EditedPetriNet, false, selected.Position);
							foreach(var t in selected.TransitionsAfter) {
								t.Before = inner;
							}
							foreach(var t in selected.TransitionsBefore) {
								t.After = inner;
							}
							selected.TransitionsAfter.Clear();
							selected.TransitionsBefore.Clear();
							EditedPetriNet.RemoveState(selected);
							EditedPetriNet.AddState(inner);
							selected = inner;
						}
						d.Destroy();
					}

					if(selected is InnerPetriNet) {
						this.EditedPetriNet = selected as InnerPetriNet;
					}
				}
			}
		}

		protected override void ManageOneButtonPress(Gdk.EventButton ev) {
			if(ev.Button == 1) {
				if(currentAction == CurrentAction.None) {
					deltaClick.X = ev.X;
					deltaClick.Y = ev.Y;

					hoveredItem = EditedPetriNet.StateAtPosition(deltaClick);

					if(hoveredItem == null) {
						hoveredItem = EditedPetriNet.TransitionAtPosition(deltaClick);
					}

					if(hoveredItem != null) {
						if(shiftDown || ctrlDown) {
							if(EntitySelected(hoveredItem))
								RemoveFromSelection(hoveredItem);
							else
								AddToSelection(hoveredItem);
						}
						else if(!EntitySelected(hoveredItem)) {
							this.SelectedEntity = hoveredItem;
						}

						motionReference = hoveredItem;
						originalPosition.X = motionReference.Position.X;
						originalPosition.Y = motionReference.Position.Y;

						if(motionReference is State) {
							currentAction = CurrentAction.MovingAction;
						}
						else if(motionReference is Transition) {
							currentAction = CurrentAction.MovingTransition;
						}
						deltaClick.X = ev.X - originalPosition.X;
						deltaClick.Y = ev.Y - originalPosition.Y;
					}
					else {
						if(!(ctrlDown || shiftDown))
							this.ResetSelection();
						else
							selectedFromRect = new HashSet<Entity>(selectedEntities);
						currentAction = CurrentAction.SelectionRect;
						originalPosition.X = ev.X;
						originalPosition.Y = ev.Y;
					}
				}
			}
			else if(ev.Button == 3) {
				if(currentAction == CurrentAction.None && hoveredItem != null && hoveredItem is State) {
					SelectedEntity = hoveredItem;
					currentAction = CurrentAction.CreatingTransition;
				}
			}
		}

		protected override bool OnButtonReleaseEvent(Gdk.EventButton ev) {
			if(currentAction == CurrentAction.MovingAction || currentAction == CurrentAction.MovingTransition) {
				if(shouldUnselect) {
					SelectedEntity = hoveredItem;
				}
				else {
					var backToPrevious = new PointD(originalPosition.X - motionReference.Position.X, originalPosition.Y - motionReference.Position.Y);
					if(backToPrevious.X != 0 || backToPrevious.Y != 0) {
						var actions = new List<GuiAction>();
						foreach(var e in selectedEntities) {
							e.Position = new PointD(e.Position.X + backToPrevious.X, e.Position.Y + backToPrevious.Y);
							actions.Add(new MoveAction(e, new PointD(-backToPrevious.X, -backToPrevious.Y)));
						}
						document.PostAction(new GuiActionList(actions, actions.Count > 1 ? "Déplacer les entités" : "Déplacer l'entité"));
					}
				}
				currentAction = CurrentAction.None;
			}
			else if(currentAction == CurrentAction.CreatingTransition && ev.Button == 1) {
				currentAction = CurrentAction.None;
				if(hoveredItem != null && hoveredItem is State) {
					document.PostAction(new AddTransitionAction(new Transition(EditedPetriNet.Document, EditedPetriNet, SelectedEntity as State, hoveredItem as State), true));
				}

				this.Redraw();
			}
			else if(currentAction == CurrentAction.SelectionRect) {
				currentAction = CurrentAction.None;

				this.ResetSelection();
				foreach(var e in selectedFromRect)
					selectedEntities.Add(e);
				document.EditorController.UpdateSelection();

				selectedFromRect.Clear();
			}

			return base.OnButtonReleaseEvent(ev);
		}

		protected override bool OnMotionNotifyEvent(Gdk.EventMotion ev)
		{
			shouldUnselect = false;

			if(currentAction == CurrentAction.MovingAction || currentAction == CurrentAction.MovingTransition) {
				if(currentAction == CurrentAction.MovingAction) {
					selectedEntities.RemoveWhere(item => item is Transition);
					document.EditorController.UpdateSelection();
				}
				else {
					SelectedEntity = motionReference;
				}
				var delta = new PointD(ev.X - deltaClick.X - motionReference.Position.X, ev.Y - deltaClick.Y - motionReference.Position.Y);
				foreach(var e in selectedEntities) {
					e.Position = new PointD(e.Position.X + delta.X, e.Position.Y + delta.Y);
				}
				this.Redraw();
			}
			else if(currentAction == CurrentAction.SelectionRect) {
				deltaClick.X = ev.X;
				deltaClick.Y = ev.Y;

				var oldSet = new HashSet<Entity>(selectedEntities);
				selectedFromRect = new HashSet<Entity>();

				double xm = Math.Min(deltaClick.X, originalPosition.X);
				double ym = Math.Min(deltaClick.Y, originalPosition.Y);
				double xM = Math.Max(deltaClick.X, originalPosition.X);
				double yM = Math.Max(deltaClick.Y, originalPosition.Y);

				foreach(State s in EditedPetriNet.States) {
					if(xm < s.Position.X + s.Radius && xM > s.Position.X - s.Radius && ym < s.Position.Y + s.Radius && yM > s.Position.Y - s.Radius)
						selectedFromRect.Add(s);
				}

				foreach(Transition t in EditedPetriNet.Transitions) {
					if(xm < t.Position.X + t.Width / 2 && xM > t.Position.X - t.Width / 2 && ym < t.Position.Y + t.Width / 2 && yM > t.Position.Y - t.Width / 2)
						selectedFromRect.Add(t);
				}

				selectedFromRect.SymmetricExceptWith(oldSet);

				this.Redraw();
			}
			else {
				deltaClick.X = ev.X;
				deltaClick.Y = ev.Y;

				hoveredItem = EditedPetriNet.StateAtPosition(deltaClick);

				if(hoveredItem == null) {
					hoveredItem = EditedPetriNet.TransitionAtPosition(deltaClick);
				}

				this.Redraw();
			}

			return base.OnMotionNotifyEvent(ev);
		}

		[GLib.ConnectBefore()]
		protected override bool OnKeyPressEvent(Gdk.EventKey ev)
		{
			if(ev.Key == Gdk.Key.Escape) {
				if(currentAction == CurrentAction.CreatingTransition) {
					currentAction = CurrentAction.None;
					this.Redraw();
				}
				else if(currentAction == CurrentAction.None) {
					if(selectedEntities.Count > 0) {
						this.ResetSelection();
					}
					else if(this.EditedPetriNet.Parent != null) {
						this.EditedPetriNet = this.EditedPetriNet.Parent;
					}
					this.Redraw();
				}
				else if(currentAction == CurrentAction.SelectionRect) {
					currentAction = CurrentAction.None;
					this.Redraw();
				}
			}
			else if(selectedEntities.Count > 0 && currentAction == CurrentAction.None && (ev.Key == Gdk.Key.Delete || ev.Key == Gdk.Key.BackSpace)) {
				document.PostAction(document.EditorController.RemoveSelection());
			}
			else if(ev.Key == Gdk.Key.Shift_L || ev.Key == Gdk.Key.Shift_R) {
				shiftDown = true;
			}
			else if(((Configuration.RunningPlatform == Platform.Mac) && (ev.Key == Gdk.Key.Meta_L || ev.Key == Gdk.Key.Meta_R)) || ((Configuration.RunningPlatform != Platform.Mac) && (ev.Key == Gdk.Key.Control_L || ev.Key == Gdk.Key.Control_L))) {
				ctrlDown = true;
			}

			return base.OnKeyPressEvent(ev);
		}

		[GLib.ConnectBefore()]
		protected override bool OnKeyReleaseEvent(Gdk.EventKey ev) {
			if(ev.Key == Gdk.Key.Shift_L || ev.Key == Gdk.Key.Shift_R) {
				shiftDown = false;
			}
			else if(((Configuration.RunningPlatform == Platform.Mac) && (ev.Key == Gdk.Key.Meta_L || ev.Key == Gdk.Key.Meta_R)) || ((Configuration.RunningPlatform != Platform.Mac) && (ev.Key == Gdk.Key.Control_L || ev.Key == Gdk.Key.Control_L))) {
				ctrlDown = false;
			}

			return base.OnKeyReleaseEvent(ev);
		}
		protected override void UpdateContextToEntity(Cairo.Context context, Entity e, ref double arrowScale) {
			if(e is Transition) {
				Color c = new Color(0.1, 0.6, 1, 1);
				double lineWidth = 2;

				if(EntitySelected(e)) {
					c.R = 0.3;
					c.G = 0.8;
					lineWidth += 2;
					arrowScale = 18;
				}
				context.SetSourceRGBA(c.R, c.G, c.B, c.A);
				context.LineWidth = lineWidth;
			}
			else if(e is State) {
				Color color = new Color(0, 0, 0, 1);
				double lineWidth = 3;

				if(EntitySelected(e)) {
					color.R = 1;
				}
				context.LineWidth = lineWidth;
				context.SetSourceRGBA(color.R, color.G, color.B, color.A);

				context.Save();

				if(e == hoveredItem && currentAction == CurrentAction.CreatingTransition) {
					lineWidth += 2;
				}

				context.LineWidth = lineWidth;
			}
		}

		protected override void SpecializedDrawing(Cairo.Context context) {
				if(currentAction == CurrentAction.CreatingTransition) {
					Color color = new Color(1, 0, 0, 1);
					double lineWidth = 2;

					if(hoveredItem != null && hoveredItem is State) {
						color.R = 0;
						color.G = 1;
					}

					PointD direction = new PointD(deltaClick.X - SelectedEntity.Position.X, deltaClick.Y - SelectedEntity.Position.Y);
					if(PetriView.Norm(direction) > (SelectedEntity as State).Radius) {
						direction = PetriView.Normalized(direction);

						PointD origin = new PointD(SelectedEntity.Position.X + direction.X * (SelectedEntity as State).Radius, SelectedEntity.Position.Y + direction.Y * (SelectedEntity as State).Radius);
						PointD destination = deltaClick;

						context.LineWidth = lineWidth;
						context.SetSourceRGBA(color.R, color.G, color.B, color.A);

						double arrowLength = 12;

						context.MoveTo(origin);
						context.LineTo(new PointD(destination.X - 0.99 * direction.X * arrowLength, destination.Y - 0.99 * direction.Y * arrowLength));
						context.Stroke();
						PetriView.DrawArrow(context, direction, destination, arrowLength);
					}
				}
				else if(currentAction == CurrentAction.SelectionRect) {
					double xm = Math.Min(deltaClick.X, originalPosition.X);
					double ym = Math.Min(deltaClick.Y, originalPosition.Y);
					double xM = Math.Max(deltaClick.X, originalPosition.X);
					double yM = Math.Max(deltaClick.Y, originalPosition.Y);

					context.LineWidth = 1;
					context.MoveTo(xm, ym);
					context.SetSourceRGBA(0.4, 0.4, 0.4, 0.6);
					context.Rectangle(xm, ym, xM - xm, yM - ym);
					context.StrokePreserve();
					context.SetSourceRGBA(0.8, 0.8, 0.8, 0.3);
					context.Fill();
				}
		}

		public override PetriNet EditedPetriNet {
			get {
				return base.EditedPetriNet;
			}
			set {
				this.ResetSelection();
				base.EditedPetriNet = value;
			}
		}

		public Entity SelectedEntity {
			get {
				if(selectedEntities.Count == 1) {
					foreach(Entity e in selectedEntities) // Just to compensate the strange absence of an Any() method which would return an object in the set
						return e;
					return null;
				}
				else
					return null;
			}
			set {
				if(value != null && EditedPetriNet != value.Parent) {
					if(value is RootPetriNet)
						this.ResetSelection();
					else {
						EditedPetriNet = value.Parent;
						SelectedEntity = value;
					}
				}
				else {
					selectedEntities.Clear();
					if(value != null)
						selectedEntities.Add(value);
					document.EditorController.UpdateSelection();
				}
			}
		}

		public HashSet<Entity> SelectedEntities {
			get {
				return selectedEntities;
			}
		}

		public bool MultipleSelection {
			get {
				return selectedEntities.Count > 1;
			}
		}

		bool EntitySelected(Entity e) {
			if(currentAction == CurrentAction.SelectionRect) {
				return selectedFromRect.Contains(e);
			}
			return selectedEntities.Contains(e);
		}

		void AddToSelection(Entity e) {
			selectedEntities.Add(e);
			document.EditorController.UpdateSelection();
		}

		void RemoveFromSelection(Entity e) {
			selectedEntities.Remove(e);
			document.EditorController.UpdateSelection();
		}

		public void ResetSelection() {
			SelectedEntity = null;
			hoveredItem = null;
			selectedEntities.Clear();
		}

		CurrentAction currentAction;
		bool shouldUnselect = false;
		Entity motionReference;
		HashSet<Entity> selectedEntities = new HashSet<Entity>();
		HashSet<Entity> selectedFromRect = new HashSet<Entity>();
		Entity hoveredItem;
		bool shiftDown;
		bool ctrlDown;
	}
}

