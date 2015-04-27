//
//  Transition.h
//  IA Pétri
//
//  Created by Rémi on 22/11/2014.
//

#ifndef IA_Pe_tri_Transition_h
#define IA_Pe_tri_Transition_h

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

template<typename _ActionResult>
class Action;

/**
 * A transition linking 2 Action, composing a PetriNet.
 */
template<typename _ActionResult>
class Transition : public CallableTimeout<uint64_t> {
public:
	Transition(Transition const &t) : CallableTimeout<uint64_t>(this->ID()), _previous(t._previous), _next(t._next), _name(t._name), _delayBetweenEvaluation(t._delayBetweenEvaluation) {
		this->setCondition(t._test);
	}

	/**
	 * Creates an Transition object, containing a nullptr test, allowing the end of execution of Action 'previous' to provoke
	 * the execution of Action 'next', if the test is fulfilled.
	 * @param previous The starting point of the Transition
	 * @param next The arrival point of the Transition
	 */
	Transition(Action<_ActionResult> &previous, Action<_ActionResult> &next) : CallableTimeout(0), _previous(previous), _next(next) {}

	/**
	 * Checks whether the Transition can be crossed
	 * @param actionResult The result of the Action 'previous'. This is useful when the Transition's test uses this value.
	 * @return The result of the test, true meaning that the Transition can be crossed to enable the action 'next'
	 */
	bool isFulfilled(_ActionResult actionResult) const {
		return _test->isFulfilled(actionResult);
	}

	/**
	 * Invoked just before the execution of Action 'previous'.
	 */
	void actionStarted() {
		_test->willTest();
	}

	/**
	 * Invoked just after the execution of Action 'previous'.
	 */
	void actionEnded() {
		_test->didTest();
	}

	/**
	 * Returns the condition associated to the Transition
	 * @return The condition associated to the Transition
	 */
	ConditionBase<_ActionResult> const &condition() const {
		return *_test;
	}

	/**
	 * Changes the condition associated to the Transition
	 * @param test The new condition to associate to the Transition
	 */
	void setCondition(ConditionBase<_ActionResult> const &test) {
		_test = std::static_pointer_cast<ConditionBase<_ActionResult>>(test.copy_ptr());
	}

	/**
	 * Changes the condition associated to the Transition
	 * @param test The new condition to associate to the Transition
	 */
	void setCondition(std::shared_ptr<ConditionBase<_ActionResult>> const &test) {
		_test = test;
	}

	/**
	 * Gets the Action 'previous', the starting point of the Transition.
	 * @return The Action 'previous', the starting point of the Transition.
	 */
	Action<_ActionResult> &previous() {
		return _previous;
	}

	/**
	 * Gets the Action 'next', the arrival point of the Transition.
	 * @return The Action 'next', the arrival point of the Transition.
	 */
	Action<_ActionResult> &next() {
		return _next;
	}

	/**
	 * Gets the name of the Transition.
	 * @return The name of the Transition.
	 */
	std::string const &name() const {
		return _name;
	}

	/**
	 * Changes the name of the Transition.
	 * @param The new name of the Transition.
	 */
	void setName(std::string const &name) {
		_name = name;
	}

	/**
	 * The delay between successive evaluations of the Transition. The runtime will not try to evaluate
	 * the Transition with a delay smaller than this delay after a previous evaluation, but only for one execution of Action 'previous'
	 * @return The minimal delay between two evaluations of the Transition.
	 */
	std::chrono::nanoseconds delayBetweenEvaluation() const {
		return _delayBetweenEvaluation;
	}

	/**
	 * Changes the delay between successive evaluations of the Transition.
	 * @param delay The new minimal delay between two evaluations of the Transition.
	 */
	void setDelayBetweenEvaluation(std::chrono::nanoseconds delay) {
		_delayBetweenEvaluation = delay;
	}

private:
	std::shared_ptr<ConditionBase<_ActionResult>> _test;
	Action<_ActionResult> &_previous;
	Action<_ActionResult> &_next;
	std::string _name;

	// Default delay between evaluation
	std::chrono::nanoseconds _delayBetweenEvaluation = 10ms;
};

#endif
