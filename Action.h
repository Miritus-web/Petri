//
//  Action.h
//  Pétri
//
//  Created by Rémi on 22/11/2014.
//

#ifndef Petri_Action_h
#define Petri_Action_h

#include "Callable.h"
#include "Condition.h"
#include <queue>
#include <list>
#include <unordered_map>
#include "ThreadPool.h"
#include <atomic>
#include <mutex>
#include <thread>
#include <deque>
#include "Transition.h"

namespace Petri {

	using namespace std::chrono_literals;

	/**
	 * A state composing a PetriNet.
	 */
	template<typename _ActionResult>
	class Action : public CallableTimeout<uint64_t> {
	public:
		/**
		 * Creates an empty action, associated to a null CallablePtr.
		 */
		Action() : CallableTimeout(0), _action(nullptr), _requiredTokens(1) {}

		/**
		 * Creates an empty action, associated to a copy ofthe specified Callable.
		 * @param action The Callable which will be copied
		 */
		Action(CallableBase<_ActionResult> const &action) : CallableTimeout(0), _action(action.copy_ptr()), _requiredTokens(1) {}

		/**
		 * Adds a Transition to the Action.
		 * @param transition the transition to be added
		 */
		void addTransition(std::shared_ptr<Transition<_ActionResult>> &transition) {
			_transitions.push_back(transition);
		}

		/**
		 * Returns the Callable asociated to the action. An Action with a null Callable must not invoke this method!
		 * @return The Callable of the Action
		 */
		CallableBase<_ActionResult> &action() {
			return *_action;
		}

		/**
		 * Changes the Callable associated to the Action
		 * @param action The Callable which will be copied and put in the Action
		 */
		void setAction(CallableBase<_ActionResult> const &action) {
			_action = action.copy_ptr();
		}

		/**
		 * Changes the Callable associated to the Action
		 * @param action The Callable which will be put in the Action
		 */
		void setAction(std::shared_ptr<CallableBase<_ActionResult>> const &action) {
			_action = action;
		}

		/**
		 * Returns the required tokens of the Action to be activated, i.e. the count of Actions which must lead to *this and terminate for *this to activate.
		 * @return The required tokens of the Action
		 */
		std::size_t requiredTokens() const {
			return _requiredTokens;
		}

		/**
		 * Changes the required tokens of the Action to be activated.
		 * @return The required tokens of the Action
		 */
		void setRequiredTokens(std::size_t requiredTokens) {
			_requiredTokens = requiredTokens;
		}

		/**
		 * Gets the current tokens count given to the Action by its preceding Actions.
		 * @return The current tokens count of the Action
		 */
		std::size_t &currentTokens() {
			return _currentTokens;
		}

		std::mutex &tokensMutex() {
			return _tokensMutex;
		}

		/**
		 * Returns the name of the Action.
		 * @return The name of the Action
		 */
		std::string const &name() const {
			return _name;
		}

		/**
		 * Sets the name of the Action
		 * @param name The name of the Action
		 */
		void setName(std::string const &name) {
			_name = name;
		}

		/**
		 * Returns the transitions exiting the Action.
		 * @param name The exiting transitions of the Action
		 */
		std::list<std::shared_ptr<Transition<_ActionResult>>> const &transitions() const {
			return _transitions;
		}

	private:
		std::list<std::shared_ptr<Transition<_ActionResult>>> _transitions;
		std::shared_ptr<CallableBase<_ActionResult>> _action;
		std::string _name;
		std::size_t _requiredTokens;
		
		std::size_t _currentTokens;
		std::mutex _tokensMutex;
	};

}

#endif
