﻿/*
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
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Petri
{
    public class DocumentSettings
    {
        public static DocumentSettings CreateSettings(HeadlessDocument doc, XElement node)
        {
            return new DocumentSettings(doc, node);
        }

        public static DocumentSettings GetDefaultSettings(HeadlessDocument doc)
        {
            return new DocumentSettings(doc, null);
        }

        public XElement GetXml()
        {
            var elem = new XElement("Settings");
            elem.SetAttributeValue("Name", Name);
            elem.SetAttributeValue("Enum", Enum.ToString());
            elem.SetAttributeValue("SourceOutputPath", SourceOutputPath);
            elem.SetAttributeValue("LibOutputPath", LibOutputPath);
            elem.SetAttributeValue("Hostname", Hostname);
            elem.SetAttributeValue("Port", Port.ToString());
            elem.SetAttributeValue("Language", Language.ToString());

            var node = new XElement("Compiler");
            node.SetAttributeValue("Invocation", Compiler);
            foreach(var f in CompilerFlags) {
                var e = new XElement("Flag");
                e.SetAttributeValue("Value", f);
                node.Add(e);
            }
            elem.Add(node);

            node = new XElement("IncludePaths");
            foreach(var p in IncludePaths) {
                var e = new XElement("IncludePath");
                e.SetAttributeValue("Path", p.Item1);
                e.SetAttributeValue("Recursive", p.Item2);
                node.Add(e);
            }
            elem.Add(node);

            node = new XElement("LibPaths");
            foreach(var p in LibPaths) {
                var e = new XElement("LibPath");
                e.SetAttributeValue("Path", p.Item1);
                e.SetAttributeValue("Recursive", p.Item2);
                node.Add(e);
            }
            elem.Add(node);

            node = new XElement("Libs");
            foreach(var p in Libs) {
                var e = new XElement("Lib");
                e.SetAttributeValue("Path", p);
                node.Add(e);
            }
            elem.Add(node);

            return elem;
        }

        private DocumentSettings(HeadlessDocument doc, XElement elem)
        {
            _document = doc;

            IncludePaths = new List<Tuple<string, bool>>();
            LibPaths = new List<Tuple<string, bool>>();
            Libs = new List<string>();
            CompilerFlags = new List<string>();

            this.Compiler = "/usr/bin/c++";
            this.SourceOutputPath = "";
            this.LibOutputPath = "";
            this.Hostname = "localhost";
            this.Port = 12345;
            this.Language = Language.Cpp;

            Name = "MyPetriNet";
            Enum = DefaultEnum;

            if(elem == null) {
                CompilerFlags.Add("-std=c++1y");
                CompilerFlags.Add("-g");
            }
            else {
                if(elem.Attribute("Name") != null)
                    Name = elem.Attribute("Name").Value;

                if(elem.Attribute("Enum") != null) {
                    Enum = new Cpp.Enum(elem.Attribute("Enum").Value);
                }
				
                if(elem.Attribute("SourceOutputPath") != null)
                    SourceOutputPath = elem.Attribute("SourceOutputPath").Value;
                if(elem.Attribute("LibOutputPath") != null)
                    LibOutputPath = elem.Attribute("LibOutputPath").Value;

                if(elem.Attribute("Hostname") != null)
                    Hostname = elem.Attribute("Hostname").Value;

                if(elem.Attribute("Port") != null)
                    Port = UInt16.Parse(elem.Attribute("Port").Value);

                if(elem.Attribute("Language") != null) {
                    try {
                        Language = (Language)Language.Parse(Language.GetType(), elem.Attribute("Language").Value);
                    }
                    catch(Exception) {
						
                    }
                }

                var node = elem.Element("Compiler");
                if(node != null) {
                    Compiler = node.Attribute("Invocation").Value;

                    foreach(var e in node.Elements()) {
                        CompilerFlags.Add(e.Attribute("Value").Value);
                    }
                }

                node = elem.Element("IncludePaths");
                if(node != null) {
                    foreach(var e in node.Elements()) {
                        IncludePaths.Add(Tuple.Create(e.Attribute("Path").Value, bool.Parse(e.Attribute("Recursive").Value)));
                    }
                }

                node = elem.Element("LibPaths");
                if(node != null) {
                    foreach(var e in node.Elements()) {
                        LibPaths.Add(Tuple.Create(e.Attribute("Path").Value, bool.Parse(e.Attribute("Recursive").Value)));
                    }
                }

                node = elem.Element("Libs");
                if(node != null) {
                    foreach(var e in node.Elements()) {
                        Libs.Add(e.Attribute("Path").Value);
                    }
                }
            }

            Modified = false;
        }

        public bool Modified {
            get;
            set;
        }

        public string Name {
            get;
            set;
        }

        public Cpp.Enum Enum {
            get;
            set;
        }

        public Cpp.Enum DefaultEnum {
            get {
                return new Cpp.Enum("ActionResult", new string[]{ "OK", "NOK" });
            }
        }

        // The bool stands for "recursive or not"
        public List<Tuple<string, bool>> IncludePaths {
            get;
            private set;
        }

        public List<Tuple<string, bool>> LibPaths {
            get;
            private set;
        }

        public List<string> Libs {
            get;
            private set;
        }

        public string Compiler {
            get;
            set;
        }

        public List<string> CompilerFlags {
            get;
            private set;
        }

        public string LibOutputPath {
            get;
            set;
        }

        public string SourceOutputPath {
            get;
            set;
        }

        public string Hostname {
            get;
            set;
        }

        public UInt16 Port {
            get;
            set;
        }

        public Language Language {
            get {
                return _language;
            }
            set {
                _language = value;

                if(_document.Settings != null && _document is Document) {
                    ((Document)_document).OnLanguageChanged();
                }
            }
        }

        public static string LanguageName(Language language)
        {
            switch(language) {
            case Language.Cpp:
                return "C++";
            case Language.C:
                return "C";
            }

            throw new Exception("Unsupported");
        }

        public string LanguageName()
        {
            return LanguageName(Language);
        }

        public string SourcePath {
            get {
                return System.IO.Path.Combine(SourceOutputPath, this.Name + "." + PetriGen.SourceExtensionFromLanguage(Language));
            }
        }

        public string LibPath {
            get {
                return System.IO.Path.Combine(LibOutputPath, this.Name + ".so");
            }
        }

        public string CompilerArguments(string source, string lib)
        {
            string val = "";

            if(Language == Language.C || Language == Language.Cpp) {
                val += "-shared ";
                if(Configuration.RunningPlatform == Platform.Mac) {
                    val += "-undefined dynamic_lookup -flat_namespace ";
                }
                else if(Configuration.RunningPlatform == Platform.Linux) {
                    val += "-fPIC ";
                }

                foreach(var f in CompilerFlags) {
                    val += f + " ";
                }

                foreach(var i in IncludePaths) {
                    // Recursive?
                    if(System.IO.Directory.Exists(i.Item1)) {
                        if(i.Item2) {
                            var directories = System.IO.Directory.EnumerateDirectories(i.Item1, "*", System.IO.SearchOption.AllDirectories);
                            foreach(var dir in directories) {
                                // Do not add dotted files
                                if(!dir.StartsWith(".")) {
                                    val += "-I'" + dir + "' ";
                                }
                            }
                        }
                    }
                    else {
                        Console.Error.WriteLine("Unable to find the include directory " + i.Item1 + "!");
                    }
                    val += "-iquote'" + i.Item1 + "' ";
                    val += "-I'" + i.Item1 + "' ";
                }
                val += "-iquote'" + SourceOutputPath + "' ";
                val += "-I'" + SourceOutputPath + "' ";

                foreach(var i in LibPaths) {
                    // Recursive?
                    if(i.Item2) {
                        var directories = System.IO.Directory.EnumerateDirectories(i.Item1, "*", System.IO.SearchOption.AllDirectories);
                        foreach(var dir in directories) {
                            val += "-L'" + dir + "' ";
                        }
                    }
                    val += "-L'" + i.Item1 + "' ";
                }

                foreach(var l in Libs) {
                    val += "-l'" + l + "' ";
                }

                if(Language == Language.Cpp) {
                    val += "-std=c++14 ";
                }

                if(Configuration.Arch == 64) {
                    val += "-m64 ";
                }
                else if(Configuration.Arch == 32) {
                    val += "-m32 ";
                }

                val += "-o '" + lib + "' ";

                if(Language == Language.Cpp) {
                    val += "-x c++ '" + source + "'";
                }
                else {
                    val += "-x c '" + source + "'";
                }
            }

            return val;
        }

        private Language _language;
        private HeadlessDocument _document;
    }
}
