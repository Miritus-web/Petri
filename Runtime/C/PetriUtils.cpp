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

//
//  PetriUtils.c
//  Petri
//
//  Created by Rémi on 01/07/2015.
//

#include "PetriUtils.h"
#include "../PetriUtils.h"

Petri_actionResult_t PetriUtility_pause(uint64_t usdelay) {
	return Petri::Utility::pause(std::chrono::microseconds(usdelay));
}

Petri_actionResult_t PetriUtility_printAction(char const *name, uint64_t id) {
	return Petri::Utility::printAction(name, id);
}

Petri_actionResult_t PetriUtility_doNothing() {
	return Petri::Utility::doNothing();
}

bool PetriUtility_returnTrue() {
	return true;
}

Petri_actionResult_t PetriUtility_returnDefault(Petri_actionResult_t res) {
	return 0;
}

