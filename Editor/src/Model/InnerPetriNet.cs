﻿using System;
using System.Xml;
using System.Xml.Linq;

namespace Petri
{
	public sealed class InnerPetriNet : PetriNet
	{
		public InnerPetriNet(HeadlessDocument doc, PetriNet parent, bool active, Cairo.PointD pos) : base(doc, parent, active, pos)
		{
			_exitPoint = new ExitPoint(doc, this, new Cairo.PointD(300, 100));
			this.AddState(this.ExitPoint);
			this.EntryPointID = Document.LastEntityID++;
		}

		public InnerPetriNet(HeadlessDocument doc, PetriNet parent, XElement descriptor) : base(doc, parent, descriptor) {
			EntryPointID = UInt64.Parse(descriptor.Attribute("EntryPointID").Value);

			foreach(var s in this.States) {
				if(s.GetType() == typeof(ExitPoint)) {
					_exitPoint = s as ExitPoint;
					break;
				}
			}

			if(_exitPoint == null)
				throw new Exception("No Exit node found in the saved Petri net!");
		}

		public override void Serialize(XElement elem) {
			elem.SetAttributeValue("EntryPointID", this.EntryPointID);
			base.Serialize(elem);
		}

		public ExitPoint ExitPoint {
			get {
				return _exitPoint;
			}
		}

		public UInt64 EntryPointID {
			get;
			set;
		}

		public string EntryPointName {
			get {
				return this.CppName + "_Entry";
			}
		}

		public override string GenerateCpp(Cpp.Generator source, IDManager lastID) {
			string name = this.EntryPointName;

			// Adding an entry point
			source += "auto " + name + " = std::make_shared<Action<" + Document.Settings.Enum.Name + ">>();";
			source += name + "->setAction(" + "make_callable_ptr([](){ return " + Document.Settings.Enum.Name + "(); })" + ");";
			source += name + "->setRequiredTokens(" + this.RequiredTokens.ToString() + ");";

			source += name + "->setName(\"" + this.Name + "_Entry" + "\");";
			source += name + "->setID(" + EntryPointID + ");";

			source += "petriNet.addAction(" + name + ", " + (Active ? "true" : "false") + ");";

			base.GenerateCpp(source, lastID);

			// Adding a transition from the entry point to all of the initially active states
			foreach(State s in States) {
				if(s.Active) {
					var newID = lastID.Consume();
					string tName = name + "_" + newID.ToString();

					source += "auto " + tName + " = std::make_shared<Transition<" + Document.Settings.Enum.Name + ">>(*" + name + ", *" + s.CppName + ");";
					source += tName + "->setCondition([](" + Document.Settings.Enum.Name + "){ return true; });";

					source += tName + "->setName(\"" + tName + "\");";
					source += tName + "->setID(" + newID.ToString() + ");";
					source += name + "->addTransition(" + tName + ");";
				}
			}

			return "";
		}

		ExitPoint _exitPoint;
	}
}

