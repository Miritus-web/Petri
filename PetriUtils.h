//
//  StateChartUtils.h
//  Pétri
//
//  Created by Rémi on 12/11/2014.
//

#ifndef Petri_PetriUtils_h
#define Petri_PetriUtils_h

#include <functional>
#include <memory>
#include "Condition.h"
#include "PetriNet.h"
#include "PetriDebug.h"

namespace Petri {

	using namespace std::chrono_literals;

	enum class ActionResult {
		OK,
		NOK
	};

	namespace PetriUtils {
		struct indirect {
			template <class _Tp>
			inline constexpr auto operator()(_Tp&& x) const {
				return *std::forward<_Tp>(x);
			}
		};
		struct adressof {
			template <class _Tp>
			inline constexpr auto operator()(_Tp&& x) const {
				return &std::forward<_Tp>(x);
			}
		};
		struct preincr {
			template <class _Tp>
			inline constexpr auto &operator()(_Tp&& x) const {
				return ++std::forward<_Tp>(x);
			}
		};
		struct predecr {
			template <class _Tp>
			inline constexpr auto &operator()(_Tp&& x) const {
				return --std::forward<_Tp>(x);
			}
		};
		struct postincr {
			template <class _Tp>
			inline constexpr auto operator()(_Tp&& x) const {
				return std::forward<_Tp>(x)++;
			}
		};
		struct postdecr {
			template <class _Tp>
			inline constexpr auto operator()(_Tp&& x) const {
				return std::forward<_Tp>(x)--;
			}
		};

		struct shift_left {
			template <class _T1, class _T2>
			inline constexpr auto operator()(_T1&& t, _T2&& u) const {
				return std::forward<_T1>(t) << std::forward<_T2>(u);
			}
		};

		struct shift_right {
			template <class _T1, class _T2>
			inline constexpr auto operator()(_T1&& t, _T2&& u) const {
				return std::forward<_T1>(t) >> std::forward<_T2>(u);
			}
		};
	}

	namespace PetriUtils {
		template<typename _ActionResult>
		inline _ActionResult defaultAction(Action<_ActionResult> *a) {
			std::cout << "Action " << a->name() << ", ID " << std::to_string(a->ID()) << " exécutée." << std::endl;
			return _ActionResult{};
		}

		template<typename _ActionResult>
		inline _ActionResult doNothing(_ActionResult result) {
			return result;
		}
	}

}

#endif
