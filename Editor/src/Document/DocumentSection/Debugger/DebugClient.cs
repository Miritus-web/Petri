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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;
using System.Linq;
using Gtk;

namespace Petri
{
	public class DebugClient {
		public DebugClient(Document doc) {
			_document = doc;
			_sessionRunning = false;
			_petriRunning = false;
			_pause = false;
		}

		~DebugClient() {
			if(_petriRunning || _sessionRunning) {
				throw new Exception(Configuration.GetLocalized("Debugger still running!"));
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
							this.SendObject(new JObject(new JProperty("type", "pause")));
						}
						else {
							this.SendObject(new JObject(new JProperty("type", "resume")));
						}
					}
					catch(Exception e) {
						GLib.Timeout.Add(0, () => {
							MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString(Configuration.GetLocalized("An error occurred in the debugger:") + " " + e.Message));
							d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
							d.Run();
							d.Destroy();

							return false;
						});

						this.Detach();
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
				return "0.2";
			}
		}

		public void Attach() {
			_sessionRunning = true;
			_receiverThread = new Thread(this.Receiver);
			_pause = false;
			_receiverThread.Start();
			DateTime time = DateTime.Now.AddSeconds(1);
			while(_socket == null && DateTime.Now.CompareTo(time) < 0);
		}

		public void Detach() {
			this.StopOrDetach(false);
		}

		public void StopSession() {
			this.StopOrDetach(true);
		}

		private void StopOrDetach(bool stop) {
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
					if(stop) {
						this.SendObject(new JObject(new JProperty("type", "exitSession")));
					}
					else {
						this.SendObject(new JObject(new JProperty("type", "exit")));
					}
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
					this.SendObject(new JObject(new JProperty("type", "start"), new JProperty("payload", new JObject(new JProperty("hash", _document.GetHash())))));
			}
			catch(Exception e) {
				GLib.Timeout.Add(0, () => {
					MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString(Configuration.GetLocalized("An error occurred in the debugger:") + " " + e.Message));
					d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
					d.Run();
					d.Destroy();

					return false;
				});

				this.Detach();
			}
		}

		public void StopPetri() {
			_pause = false;
			try {
				if(_petriRunning)
					this.SendObject(new JObject(new JProperty("type", "stop")));
			}
			catch(Exception e) {
				GLib.Timeout.Add(0, () => {
					MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString(Configuration.GetLocalized("An error occurred in the debugger:") + " " + e.Message));
					d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
					d.Run();
					d.Destroy();

					return false;
				});

				this.Detach();
			}
		}

		public void ReloadPetri() {
			GLib.Timeout.Add(0, () => {
				_document.Window.DebugGui.Status = Configuration.GetLocalized("Reloading the petri net…");
				return false;
			});
			GLib.Timeout.Add(1, () => {
				this.StopPetri();
				if(!_document.Compile(true)) {
					GLib.Timeout.Add(0, () => {
						MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString(Configuration.GetLocalized("The compilation has failed.")));
						d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
						d.Run();
						d.Destroy();

						return false;
					});
				}
				else {
					try {
						this.SendObject(new JObject(new JProperty("type", "reload")));
					}
					catch(Exception e) {
						GLib.Timeout.Add(0, () => {
							MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString(Configuration.GetLocalized("An error occurred in the debugger:") + " " + e.Message));
							d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
							d.Run();
							d.Destroy();

							return false;
						});
						this.Detach();
					}
				}

				return false;
			});
		}

		public void UpdateBreakpoints() {
			if(PetriRunning) {
				var breakpoints = new JArray();
				foreach(var p in _document.DebugController.Breakpoints) {
					breakpoints.Add(new JValue(p.ID));
				}
				this.SendObject(new JObject(new JProperty("type", "breakpoints"), new JProperty("payload", breakpoints)));
			}
		}

		public void Evaluate(Cpp.Expression expression) {
			if(!PetriRunning) {
				var literals = expression.GetLiterals();
				foreach(var l in literals) {
					if(l is Cpp.VariableExpression) {
						throw new Exception(Configuration.GetLocalized("A variable of the petri net cannot be evaluated when the petri net is not running."));
					}
				}
			}

			string sourceName = System.IO.Path.GetTempFileName();

			var petriGen = PetriGen.PetriGenFromLanguage(_document.Settings.Language, _document);
			petriGen.WriteExpressionEvaluator(expression, sourceName);

			string libName = System.IO.Path.GetTempFileName();

			var c = new CppCompiler(_document);
			var o = c.CompileSource(sourceName, libName);
			if(o != "") {
				throw new Exception(Configuration.GetLocalized("Compilation error:") + " " + o);
			}
			else {
				try {
					this.SendObject(new JObject(new JProperty("type", "evaluate"), new JProperty("payload", new JObject(new JProperty("lib", libName)))));
				}
				catch(Exception e) {
					this.Detach();
					_document.Window.DebugGui.UpdateToolbar();
					throw e;
				}
			}
			System.IO.File.Delete(sourceName);
		}

		private void Hello() {
			try {
				this.SendObject(new JObject(new JProperty("type", "hello"), new JProperty("payload", new JObject(new JProperty("version", Version)))));
		
				var ehlo = this.ReceiveObject();
				if(ehlo != null && ehlo["type"].ToString() == "ehlo") {
					GLib.Timeout.Add(0, () => {
						_document.Window.DebugGui.Status = Configuration.GetLocalized("Sucessfully connected.");
						return false;
					});
					_document.Window.DebugGui.UpdateToolbar();
					return;
				}
				else if(ehlo != null && ehlo["type"].ToString() == "error") {
					throw new Exception(Configuration.GetLocalized("An error was returned by the debugger:") + " " + ehlo["payload"]);
				}
				throw new Exception(Configuration.GetLocalized("Invalid message received from debugger (expected ehlo)."));
			}
			catch(Exception e) {
				GLib.Timeout.Add(0, () => {
					MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString(Configuration.GetLocalized("An error occurred in the debugger:") + " " + e.Message));
					d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
					d.Run();
					d.Destroy();

					return false;
				});
				this.Detach();
				_document.Window.DebugGui.UpdateToolbar();
			}
		}

		private void Receiver() {
			try {
				_socket = new TcpClient(_document.Settings.Hostname, _document.Settings.Port);
			}
			catch(Exception e) {
				this.Detach();

				GLib.Timeout.Add(0, () => {
					MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString(Configuration.GetLocalized("Unable to connect to the server:") + " " + e.Message));
					d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
					d.Run();
					d.Destroy();

					return false;
				});
				return;
			}

			try {
				this.Hello();

				while(_sessionRunning && _socket.Connected) {
					JObject msg = this.ReceiveObject();
					if(msg == null)
						break;

					if(msg["type"].ToString() == "ack") {
						if(msg["payload"].ToString() == "start") {
							_petriRunning = true;
							_document.Window.DebugGui.UpdateToolbar();
							this.UpdateBreakpoints();
							GLib.Timeout.Add(0, () => {
								_document.Window.DebugGui.Status = Configuration.GetLocalized("The petri net is running.");
								return false;
							});
						}
						else if(msg["payload"].ToString() == "stop") {
							_petriRunning = false;
							_document.Window.DebugGui.UpdateToolbar();
							lock(_document.DebugController.ActiveStates) {
								_document.DebugController.ActiveStates.Clear();
							}
							_document.Window.DebugGui.View.Redraw();
							GLib.Timeout.Add(0, () => {
								_document.Window.DebugGui.Status = Configuration.GetLocalized("The petri net execution has ended.");
								return false;
							});
						}
						else if(msg["payload"].ToString() == "reload") {
							_document.Window.DebugGui.UpdateToolbar();
							GLib.Timeout.Add(0, () => {
								_document.Window.DebugGui.Status = Configuration.GetLocalized("The Petri net has been successfully reloaded.");
								return false;
							});
						}
						else if(msg["payload"].ToString() == "pause") {
							_pause = true;
							_document.Window.DebugGui.UpdateToolbar();
							GLib.Timeout.Add(0, () => {
								_document.Window.DebugGui.Status = Configuration.GetLocalized("Paused.");
								return false;
							});
						}
						else if(msg["payload"].ToString() == "resume") {
							_pause = false;
							_document.Window.DebugGui.UpdateToolbar();
							GLib.Timeout.Add(0, () => {
								_document.Window.DebugGui.Status = Configuration.GetLocalized("The petri net is running.");
								return false;
							});
						}
					}
					else if(msg["type"].ToString() == "error") {
						GLib.Timeout.Add(0, () => {
							MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString(Configuration.GetLocalized("An error occurred in the debugger:") + " " + msg["payload"].ToString()));
							d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
							d.Run();
							d.Destroy();

							return false;
						});

						if(_petriRunning) {
							this.StopPetri();
						}
					}
					else if(msg["type"].ToString() == "exit" || msg["type"].ToString() == "exitSession") {
						if(msg["payload"].ToString() == "kbye") {
							_sessionRunning = false;
							_petriRunning = false;
							_document.Window.DebugGui.UpdateToolbar();
							GLib.Timeout.Add(0, () => {
								_document.Window.DebugGui.Status = Configuration.GetLocalized("Disconnected.");
								return false;
							});
						}
						else {
							_sessionRunning = false;
							_petriRunning = false;

							throw new Exception(Configuration.GetLocalized("Remote debugger requested a session termination for reason:") + " " + msg["payload"].ToString());
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
									throw new Exception(Configuration.GetLocalized("Entity sent from runtime doesn't exist on our side! (id: {0})", id));
								}
								_document.DebugController.ActiveStates[e as State] = int.Parse(s["count"].ToString());
							}
						}

						_document.Window.DebugGui.View.Redraw();
					}
					else if(msg["type"].ToString() == "evaluation") {
						var lib = msg["payload"]["lib"].ToString();
						if(lib != "") {
							System.IO.File.Delete(lib);
						}
						GLib.Timeout.Add(0, () => {
							_document.Window.DebugGui.OnEvaluate(msg["payload"]["eval"].ToString());
							return false;
						});
					}
				}
				if(_sessionRunning) {
					throw new Exception(Configuration.GetLocalized("Socket unexpectedly disconnected"));
				}
			}
			catch(Exception e) {
				GLib.Timeout.Add(0, () => {
					MessageDialog d = new MessageDialog(_document.Window, DialogFlags.Modal, MessageType.Question, ButtonsType.None, MainClass.SafeMarkupFromString(Configuration.GetLocalized("An error occurred in the debugger:") + " " + e.Message));
					d.AddButton(Configuration.GetLocalized("Cancel"), ResponseType.Cancel);
					d.Run();
					d.Destroy();

					return false;
				});
				this.Detach();
			}

			try {
				_socket.Close();
			}
			catch(Exception) {}
			_document.Window.DebugGui.UpdateToolbar();
		}

		private JObject ReceiveObject() {
			int count = 0;
			while(_sessionRunning) {
				string val = this.ReceiveString();

				if(val.Length > 0)
					return JObject.Parse(val);

				if(++count > 5) {
					throw new Exception(Configuration.GetLocalized("Remote debugger isn't available anymore!"));
				}
				Thread.Sleep(1);
			}

			return null;
		}

		private void SendObject(JObject o) {
			this.SendString(o.ToString());
		}

		private string ReceiveString() {
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

		private void SendString(string s) {
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

