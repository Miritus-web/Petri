using System;
using System.Collections.Generic;
using System.Threading;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;
using System.Linq;
using Gtk;

namespace Petri
{
	public class DebugServer {
		public DebugServer(Document doc) {
			_document = doc;
			_sessionRunning = false;
			_petriRunning = false;
			_pause = false;
		}

		~DebugServer() {
			if(_petriRunning || _sessionRunning) {
				throw new Exception("Debugger still running!");
			}
		}

		public bool SessionRunning {
			get {
				return _sessionRunning;
			}
		}

		public bool PetriRunning {
			get {
				return _petriRunning;
			}
		}

		public bool Pause {
			get {
				return _pause;
			}
			set {
				if(PetriRunning) {
					try {
						if(value) {
							this.sendObject(new JObject(new JProperty("type", "pause")));
						}
						else {
							this.sendObject(new JObject(new JProperty("type", "resume")));
						}
					}
					catch(Exception e) {
						GLib.Timeout.Add(0, () => {
							MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString("Une erreur est survenue dans le débuggueur : " + e.Message));
							d.AddButton("Annuler", ResponseType.Cancel);
							d.Run();
							d.Destroy();

							return false;
						});

						this.StopSession();
					}
				}
				else {
					_pause = false;
					_document.Window.DebugGui.UpdateToolbar();
				}
			}
		}

		public string Version {
			get {
				return "0.1";
			}
		}

		public void StartSession() {
			_sessionRunning = true;
			_receiverThread = new Thread(this.receiver);
			_pause = false;
			_receiverThread.Start();
			DateTime time = DateTime.Now.AddSeconds(1);
			while(_socket == null && DateTime.Now.CompareTo(time) < 0);
		}

		public void StopSession() {
			_pause = false;
			if(_sessionRunning) {
				if(PetriRunning) {
					if(Pause) {
						this.Pause = false;
					}
					StopPetri();
					_petriRunning = false;
				}

				try {
					this.sendObject(new JObject(new JProperty("type", "exit")));
				}
				catch(Exception) {}

				if(_receiverThread != null && !_receiverThread.Equals(Thread.CurrentThread)) {
					_receiverThread.Join();
				}
				_sessionRunning = false;
			}

			lock(_document.DebugController.ActiveStates) {
				_document.DebugController.ActiveStates.Clear();
			}
		}

		public void StartPetri() {
			_pause = false;
			try {
				if(!_petriRunning)
					this.sendObject(new JObject(new JProperty("type", "start"), new JProperty("payload", new JObject(new JProperty("hash", _document.GetHash())))));
			}
			catch(Exception e) {
				GLib.Timeout.Add(0, () => {
					MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString("Une erreur est survenue dans le débuggueur : " + e.Message));
					d.AddButton("Annuler", ResponseType.Cancel);
					d.Run();
					d.Destroy();

					return false;
				});

				this.StopSession();
			}
		}

		public void StopPetri() {
			_pause = false;
			try {
				if(_petriRunning)
					this.sendObject(new JObject(new JProperty("type", "stop")));
			}
			catch(Exception e) {
				GLib.Timeout.Add(0, () => {
					MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString("Une erreur est survenue dans le débuggueur : " + e.Message));
					d.AddButton("Annuler", ResponseType.Cancel);
					d.Run();
					d.Destroy();

					return false;
				});

				this.StopSession();
			}
		}

		public void ReloadPetri() {
			this.StopPetri();
			_document.SaveCppDontAsk();
			if(!_document.Compile()) {

			}
			else {
				try {
					this.sendObject(new JObject(new JProperty("type", "reload")));
				}
				catch(Exception e) {
					GLib.Timeout.Add(0, () => {
						MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString("Une erreur est survenue dans le débuggueur : " + e.Message));
						d.AddButton("Annuler", ResponseType.Cancel);
						d.Run();
						d.Destroy();

						return false;
					});
					this.StopSession();
				}
			}
		}

		public void UpdateBreakpoints() {
			var breakpoints = new JArray();
			foreach(var p in _document.DebugController.Breakpoints) {
				breakpoints.Add(new JValue(p.ID));
			}
			this.sendObject(new JObject(new JProperty("type", "breakpoints"), new JProperty("payload", breakpoints)));
		}

		private void Hello() {
			try {
				this.sendObject(new JObject(new JProperty("type", "hello"), new JProperty("payload", new JObject(new JProperty("version", Version)))));
		
				var ehlo = this.receiveObject();
				if(ehlo != null && ehlo["type"].ToString() == "ehlo") {
					_document.Window.DebugGui.UpdateToolbar();
					return;
				}
				else if(ehlo != null && ehlo["type"].ToString() == "error") {
					throw new Exception("An error was returned by the debugger: " + ehlo["error"]);
				}
				throw new Exception("Invalid message received from debugger (expected ehlo)");
			}
			catch(Exception e) {
				GLib.Timeout.Add(0, () => {
					MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString("Une erreur est survenue dans le débuggueur : " + e.Message));
					d.AddButton("Annuler", ResponseType.Cancel);
					d.Run();
					d.Destroy();

					return false;
				});
				this.StopSession();
				_document.Window.DebugGui.UpdateToolbar();
			}
		}

		private void receiver() {
			try {
				_socket = new TcpClient(_document.Settings.Hostname, _document.Settings.Port);
			}
			catch(Exception e) {
				this.StopSession();

				GLib.Timeout.Add(0, () => {
					MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString("Impossible de se connecter au serveur : " + e.Message));
					d.AddButton("Annuler", ResponseType.Cancel);
					d.Run();
					d.Destroy();

					return false;
				});
				return;
			}
			this.Hello();

			try {
				while(_sessionRunning && _socket.Connected) {
					JObject msg = this.receiveObject();
					if(msg == null)
						break;

					if(msg["type"].ToString() == "ack") {
						if(msg["payload"].ToString() == "start") {
							_petriRunning = true;
							_document.Window.DebugGui.UpdateToolbar();
						}
						else if(msg["payload"].ToString() == "stop") {
							_petriRunning = false;
							_document.Window.DebugGui.UpdateToolbar();
							lock(_document.DebugController.ActiveStates) {
								_document.DebugController.ActiveStates.Clear();
							}
							_document.Window.DebugGui.View.Redraw();
						}
						else if(msg["payload"].ToString() == "reload") {
							_document.Window.DebugGui.UpdateToolbar();
						}
						else if(msg["payload"].ToString() == "pause") {
							_pause = true;
							_document.Window.DebugGui.UpdateToolbar();
						}
						else if(msg["payload"].ToString() == "resume") {
							_pause = false;
							_document.Window.DebugGui.UpdateToolbar();
						}
					}
					else if(msg["type"].ToString() == "error") {
						GLib.Timeout.Add(0, () => {
							MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString("Une erreur est survenue dans le débuggueur : " + msg["payload"].ToString()));
							d.AddButton("Annuler", ResponseType.Cancel);
							d.Run();
							d.Destroy();

							return false;
						});

						if(_petriRunning) {
							this.StopPetri();
						}
					}
					else if(msg["type"].ToString() == "exit") {
						if(msg["payload"].ToString() == "kbye") {
							_sessionRunning = false;
							_petriRunning = false;
							_document.Window.DebugGui.UpdateToolbar();
						}
						else {
							_sessionRunning = false;
							_petriRunning = false;

							throw new Exception("Remote debugger requested a session termination for reason: " + msg["payload"].ToString());
						}
					}
					else if(msg["type"].ToString() == "states") {
						var states = msg["payload"].Select(t => t).ToList();

						lock(_document.DebugController.ActiveStates) {
							_document.DebugController.ActiveStates.Clear();
							foreach(var s in states) {
								var id = UInt64.Parse(s["id"].ToString());
								var e = _document.EntityFromID(id);
								if(e == null || !(e is State)) {
									throw new Exception("Entity sent from runtime doesn't exist on our side! (id: " + id + ")");
								}
								_document.DebugController.ActiveStates[e as State] = int.Parse(s["count"].ToString());
							}
						}

						_document.Window.DebugGui.View.Redraw();
					}
				}
				if(_sessionRunning) {
					throw new Exception("Socket unexpectedly disconnected");
				}
			}
			catch(Exception e) {
				GLib.Timeout.Add(0, () => {
					MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString("Une erreur est survenue dans le débuggueur : " + e.Message));
					d.AddButton("Annuler", ResponseType.Cancel);
					d.Run();
					d.Destroy();

					return false;
				});
				this.StopSession();
			}

			try {
				_socket.Close();
			}
			catch(Exception) {}
			_document.Window.DebugGui.UpdateToolbar();
		}

		private JObject receiveObject() {
			int count = 0;
			while(_sessionRunning) {
				string val = this.receiveString();
				if(val.Length > 0)
					return JObject.Parse(val);

				if(++count > 5) {
					throw new Exception("Remote debugger isn't available anymore!");
				}
				Thread.Sleep(1);
			}

			return null;
		}

		private void sendObject(JObject o) {
			this.sendString(o.ToString());
		}

		private string receiveString() {
			byte[] msg;

			lock(_downLock) {
				byte[] countBytes = new byte[4];

				int len =  _socket.GetStream().Read(countBytes, 0, 4);
				if(len != 4)
					return "";

				UInt32 count = (UInt32)countBytes[0] | ((UInt32)countBytes[1] << 8) | ((UInt32)countBytes[2] << 16) | ((UInt32)countBytes[3] << 24);
				UInt32 read = 0;

				msg = new byte[count];
				while(read < count) {
					read += (UInt32)_socket.GetStream().Read(msg, (int)read, (int)(count - read));
				}
			}

			return System.Text.Encoding.UTF8.GetString(msg);
		}

		private void sendString(string s) {
			var msg = System.Text.Encoding.UTF8.GetBytes(s);

			UInt32 count = (UInt32)msg.Length;

			byte[] bytes = new byte[4 + count];

			bytes[0] = (byte)((count >> 0) & 0xFF);
			bytes[1] = (byte)((count >> 8) & 0xFF);
			bytes[2] = (byte)((count >> 16) & 0xFF);
			bytes[3] = (byte)((count >> 24) & 0xFF);

			msg.CopyTo(bytes, 4);

			lock(_upLock) {
				_socket.GetStream().Write(bytes, 0, bytes.Length);
			}
		}

		bool _petriRunning, _pause;
		volatile bool _sessionRunning;
		Thread _receiverThread;

		volatile TcpClient _socket;
		object _upLock = new object();
		object _downLock = new object();

		Document _document;
	}
}

