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
//  ThreadMemoryPool.cpp
//  Pétri
//
//  Created by Rémi on 06/07/2014.
//

#include "ManagedMemoryHeap.h"

namespace Petri {

    ManagedMemoryHeap::ManagedHeaps ManagedMemoryHeap::_managedHeaps;
}

using namespace Petri;

void *operator new(size_t bytes) {
    void *p = nullptr;

    ManagedMemoryHeap *currentHeap = ManagedMemoryHeap::_managedHeaps.getHeap(std::this_thread::get_id());

    if(!currentHeap) {
        p = std::malloc(bytes);
    } else {
        p = std::malloc(bytes);
        currentHeap->_allocatedObjects.insert(ManagedMemoryHeap::SetPair(p, bytes));
    }

    return p;
}

void operator delete(void *p) noexcept {
    ManagedMemoryHeap *currentPool = ManagedMemoryHeap::_managedHeaps.getHeap(std::this_thread::get_id());

    auto classicDelete = [](void *p) { std::free(p); };

    if(currentPool) {
        auto it = currentPool->_allocatedObjects.find(ManagedMemoryHeap::SetPair(p, 0));
        if(it == currentPool->_allocatedObjects.end()) {
            classicDelete(p);
        } else {
            std::free(p);
            currentPool->_allocatedObjects.erase(it);
        }
    } else {
        classicDelete(p);
    }
}