/*
 * Copyright (c) 2016 Rémi Saurel
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

namespace Petri.Runtime
{
    /// <summary>
    /// A wrapper to a petri net variable.
    /// </summary>
    public class Atomic : MarshalByRefObject
    {
        internal Atomic(PetriNet pn, UInt32 id)
        {
            _pn = pn;
            _id = id;
        }

        /// <summary>
        /// Gets or sets the value of the variable
        /// </summary>
        /// <value>The value.</value>
        public Int64 Value {
            get {
                return Interop.PetriNet.PetriNet_getVariableValue(_pn.Handle, _id);
            }
            set {
                Interop.PetriNet.PetriNet_setVariableValue(_pn.Handle, _id, value);
            }
        }

        PetriNet _pn;
        UInt32 _id;
    }
}

