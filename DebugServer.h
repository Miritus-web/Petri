//
//  DebugServer.h
//  Pétri
//
//  Created by Rémi on 23/11/2014.
//

#ifndef Petri_DebugServer_h
#define Petri_DebugServer_h

#include <string>
#include "PetriNet.h"
#include "jsoncpp/include/json.h"
#include <set>
#include "Socket.h"

namespace Petri {
	
	using namespace std::string_literals;
	using namespace std::chrono_literals;

	namespace DebugServer {
		extern std::string getVersion();
		extern std::chrono::system_clock::time_point getAPIdate();
		extern std::chrono::system_clock::time_point getDateFromTimestamp(char const *timestamp);
	}

	class PetriDynamicLibCommon;

	class PetriDebug;

	class DebugSession {
	public:
		DebugSession(PetriDynamicLibCommon &petri);

		~DebugSession();

		DebugSession(DebugSession const &) = delete;
		DebugSession &operator=(DebugSession const &) = delete;

		void start();
		void stop();
		bool running() const;

		void addActiveState(Action &a);
		void removeActiveState(Action &a);
		void notifyStop();

	protected:
		void serverCommunication();
		void heartBeat();

		void clearPetri();

		void setPause(bool pause);

		void updateBreakpoints(Json::Value const &breakpoints);

		Json::Value receiveObject();
		void sendObject(Json::Value const &o);

		Json::Value json(std::string const &type, Json::Value const &payload);
		Json::Value error(std::string const &error);

		std::map<Action *, std::size_t> _activeStates;
		bool _stateChange = false;
		std::condition_variable _stateChangeCondition;
		std::mutex _stateChangeMutex;

		std::thread _receptionThread;
		std::thread _heartBeat;
		Petri::Socket _socket;
		Petri::Socket _client;
		std::atomic_bool _running = {false};

		PetriDynamicLibCommon &_petriNetFactory;
		std::unique_ptr<PetriDebug> _petri;
		std::mutex _sendMutex;
		std::mutex _breakpointsMutex;
		std::set<Action *> _breakpoints;
	};

}

#include "DebugServer.hpp"

#endif
