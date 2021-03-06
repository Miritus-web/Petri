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
using System.Resources;
using System.Xml;
using System.Xml.Linq;

namespace Petri.Editor
{
    public sealed class ExitPoint : State
    {
        public ExitPoint(HeadlessDocument doc, PetriNet parent, Cairo.PointD pos) : base(doc, parent, false, 0, pos)
        {
            this.Radius = 25;
        }

        public ExitPoint(HeadlessDocument doc, PetriNet parent, XElement descriptor) : base(doc, parent, descriptor)
        {
			
        }

        public override XElement GetXML()
        {
            var elem = new XElement("Exit");
            this.Serialize(elem);
            return elem;
        }

        /// <summary>
        /// Forces the value to <c>false</c> as the instance can never be active.
        /// </summary>
        /// <value><c>true</c> if active; otherwise, <c>false</c>.</value>
        public override bool Active {
            get {
                return false;
            }
            set {
                base.Active = false;
            }
        }

        /// <summary>
        /// Always require as much token as there are transitions leading to <c>this</c>, so that every state is sync'ed before exiting.
        /// </summary>
        /// <value>The required tokens.</value>
        public override int RequiredTokens {
            get {
                return this.TransitionsBefore.Count;
            }
            set {
				
            }
        }

        /// <summary>
        /// Gets or sets the name of the entity. This is a no-op here.
        /// </summary>
        /// <value>The name.</value>
        public override string Name {
            get {
                return "End";
            }
            set {
                base.Name = "End";
            }
        }

        /// <summary>
        /// The entity doesn't use any function.
        /// </summary>
        /// <returns><c>true</c>, if function was used, <c>false</c> otherwise.</returns>
        /// <param name="f">F.</param>
        public override bool UsesFunction(Code.Function f)
        {
            return false;
        }
    }
}

