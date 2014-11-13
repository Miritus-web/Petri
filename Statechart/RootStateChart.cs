﻿using System;
using System.Xml;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Statechart
{
	public class RootStateChart : StateChart
	{
		public RootStateChart(Document doc) : base(doc, null, true, new Cairo.PointD(0, 0))
		{
			Document.Controller.Modified = false;
		}

		public RootStateChart(Document doc, XElement descriptor) : base(doc, null, descriptor)
		{
			Document.Controller.Modified = false;
		}

		public override bool Active {
			get {
				return true;
			}
			set {
				base.Active = true;
			}
		}

		public override int RequiredTokens {
			get {
				return 0;
			}
			set {
			}
		}

		public override string Name {
			get {
				return "Root";
			}
			set {
				base.Name = "Root";
			}
		}

		public override Document Document {
			get {
				return document;
			}
			set {
				document = value;
			}
		}

		public Cpp.Generator GenerateCpp()
		{
			var source = new Cpp.Generator();

			this.GenerateCpp(source, new IDManager(Document.LastEntityID));

			return source;
		}

		public override void GenerateCpp(Cpp.Generator source, IDManager lastID) {
			source.AddHeader("\"StateChartUtils.h\"");
			foreach(var s in Document.Controller.Headers) {
				source.AddHeader("\"" + s + "\"");
			}

			source += "namespace MyStateChart {";

			source += "ResultatAction defaultAction(Action *a) {\nlogInfo(\"Action \" + a->name() + \", ID \" + std::to_string(a->ID()) + \" exécutée.\");\nreturn ResultatAction::ActionReussie;\n}\n";

			source += "inline std::unique_ptr<StateChart> create() {";
			source += "auto stateChart = std::make_unique<StateChart>();";
			source += "\n";

			base.GenerateCpp(source, lastID);

			source += "";

			source += "return stateChart;";

			source += "}"; // create()

			source += "}"; // namespace <State Chart Name>
		}

		// Use this to scale down the IDs of Actions (resp. Transitions) to 0...N, with N = number of Actions (resp. Transitions)
		public void Canonize()
		{
			var states = this.BuildActionsList();
			states.Add(this);
			states.AddRange(this.Transitions);

			states.Sort(delegate(Entity o1, Entity o2) {
				return o1.ID.CompareTo(o2.ID);
			});

			Document.LastEntityID = 0;
			foreach(Entity o in states) {
				o.ID = Document.LastEntityID;
				++Document.LastEntityID;
			}

			Document.Controller.Modified = true;
		}

		private Document document;
	}
}

