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
//  PetriDynamicLib.h
//  Petri
//
//  Created by Rémi on 02/07/2015.
//

#ifndef CPetriDynamicLib_c
#define CPetriDynamicLib_c

#define DECLARE_STRUCT(x)  DECLARE_STRUCT_EVAL(x)
#define DECLARE_STRUCT_EVAL(prefix, class_name, port) struct class_name ## _1 {};

#include "PetriNet.h"
#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

	typedef struct PetriDynamicLib PetriDynamicLib;

	/**
	 * Creates a dynamic library handle to the specified name, prefix and port.
	 * @return The newly created handle.
	 */
	PetriDynamicLib *PetriDynamicLib_create(char const *name, char const *prefix, uint16_t port);

	/**
	 * Destroys the specified dynamic library handle.
	 * @param lib The dynamic library handle to destroy.
	 */
	void PetriDynamicLib_destroy(PetriDynamicLib *lib);

	/**
	 * Creates the PetriNet as contained in the dynamic library.
	 * @param lib The dynamic library handle to extract the PetriNet from.
	 * @return The newly created PetriNet, or NULL if the lib is not load()ed.
	 */
	PetriNet *PetriDynamicLib_createPetriNet(PetriDynamicLib *lib);

	/**
	 * Creates the PetriNet as contained in the dynamic library, along with debugging facilities.
	 * @param lib The dynamic library handle to extract the PetriNet from.
	 * @return The newly created PetriNet, or NULL if the lib is not load()ed.
	 */
	PetriNet *PetriDynamicLib_createDebugPetriNet(PetriDynamicLib *lib);

	/**
	 * Returns the SHA-1 hash string that identifies the PetriNet contained in the library.
	 * @param lib The dynamic library handle containing the PetriNet.
	 * @return The PetriNet's SHA-1 hash string.
	 */
	char const *PetriDynamicLib_getHash(PetriDynamicLib *lib);

	/**
	 * Returns the name of the PetriNet contained in the library.
	 * @param lib The dynamic library handle containing the PetriNet.
	 * @return The PetriNet's name.
	 */
	char const *PetriDynamicLib_getName(PetriDynamicLib *lib);

	/**
	 * Returns the TCP port on which the debugger will try to attach to.
	 * @param lib The dynamic library handle containing the PetriNet.
	 * @return The PetriNet's debugging TCP port.
	 */
	uint16_t PetriDynamicLib_getPort(PetriDynamicLib *lib);

	/**
	 * Loads the symbols contained in the dynamic library, resulting in an error if they are not available
	 * @param lib The dynamic library handle containing the PetriNet.
	 * @return The dynamic library's relative path.
	 */
	bool PetriDynamicLib_load(PetriDynamicLib *lib);

	/**
	 * Returns the path of the dynamic library, relative to the main executable.
	 * @param lib The dynamic library handle containing the PetriNet.
	 * @return The dynamic library's relative path.
	 */
	char const *PetriDynamicLib_getPath(PetriDynamicLib *lib);

	/**
	 * Returns the dylib's prefix.
	 * @param lib The dynamic library handle containing the PetriNet.
	 * @return The dynamic library's prefix.
	 */
	char const *PetriDynamicLib_getPrefix(PetriDynamicLib *lib);

	

#ifdef __cplusplus
}
#endif

#endif /* PetriDynamicLib_c */
