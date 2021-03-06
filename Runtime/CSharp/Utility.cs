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
    public enum ActionResult
    {
        OK,
        NOK
    }

    public class Utility
    {
        public static Int32 Pause(double delay)
        {
            return Interop.PetriUtils.PetriUtility_pause((UInt64)(delay * 1.0e6));
        }

        public static Int32 PrintAction(string name, UInt64 id)
        {
            return Interop.PetriUtils.PetriUtility_printAction(name, id);
        }

        public static Int32 DoNothing()
        {
            return Interop.PetriUtils.PetriUtility_doNothing();
        }

        bool ReturnTrue(Int32 res)
        {
            return true;
        }

        Int64 Random(Int64 lowerBound, Int64 upperBound)
        {
            return Interop.PetriUtils.PetriUtility_random(lowerBound, upperBound);
        }
    }
}

