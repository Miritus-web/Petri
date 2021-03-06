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

namespace Petri.Editor.Code
{
    public class FunctionInvocation : Expression
    {
        public FunctionInvocation(Language language,
                                  Function function,
                                  params Expression[] arguments) : base(language,
                                                                            Code.Operator.Name.FunCall)
        {
            if(arguments.Length != function.Parameters.Count) {
                throw new Exception(Configuration.GetLocalized("Invalid arguments count."));
            }

            this.Arguments = new List<Expression>();
            foreach(var arg in arguments) {
                var a = arg;
                if(a.MakeUserReadable() == "")
                    a = LiteralExpression.CreateFromString("void", language);

                this.Arguments.Add(a);
            }

            // TODO: Perform type verification here
            this.Function = function;
        }

        public List<Expression> Arguments {
            get;
            private set;
        }

        public Function Function {
            get;
            private set;
        }

        public override bool UsesFunction(Function f)
        {
            bool res = false;
            res = res || Function == f;
            foreach(var e in Arguments) {
                res = res || e.UsesFunction(f);
            }

            return res;
        }

        public override string MakeCode()
        {
            string args = "";
            for(int i = 0; i < Function.Parameters.Count; ++i) {
                if(i > 0) {
                    args += ", ";
                }

                var code = Arguments[i].MakeCode();
                bool cast = !(Function.Parameters[i].Type.Equals(Type.UnknownType(Language)));

                switch(Language) {
                case Language.Cpp:
                    if(cast) {
                        code = "static_cast<" + Function.Parameters[i].Type.ToString() + ">(" + code + ")";
                    }
                    break;
                case Language.C:
                case Language.CSharp:
                    if(cast) {
                        code = "(" + Function.Parameters[i].Type.ToString() + ")(" + code + ")";
                    }
                    break;
                default:
                    throw new Exception("FunctionInvoction.MakeCode: Should not get there!");
                }

                args += code;
            }

            string template = "";
            if(Function.Template) {
                template = "<" + Function.TemplateArguments + ">";
            }

            return Function.QualifiedName + template + "(" + args + ")";
        }

        public override string MakeUserReadable()
        {
            string args = "";
            foreach(var arg in Arguments) {
                if(args.Length > 0)
                    args += ", ";
                args += arg.MakeUserReadable();
            }

            return Function.QualifiedName + "(" + args + ")";
        }

        public override List<LiteralExpression> GetLiterals()
        {
            var l1 = new List<LiteralExpression>();
            foreach(var e in Arguments) {
                var l2 = e.GetLiterals();
                l1.AddRange(l2);
            }

            return l1;
        }
    }

    public class MethodInvocation : FunctionInvocation
    {
        public MethodInvocation(Language language,
                                Method function,
                                Expression that,
                                bool indirection,
                                params Expression[] arguments) : base(language,
                                                                          function,
                                                                          arguments)
        {
            this.This = that;
            this.Indirection = indirection;
        }

        public Expression This {
            get;
            private set;
        }

        public bool Indirection {
            get;
            private set;
        }

        public override string MakeUserReadable()
        {
            string args = "";
            foreach(var arg in Arguments) {
                if(args.Length > 0)
                    args += ", ";
                args += arg.MakeUserReadable();
            }

            return This.MakeUserReadable() + (Indirection ? "->" : ".") + Function.QualifiedName + "(" + args + ")";
        }

        public override List<LiteralExpression> GetLiterals()
        {
            var l1 = base.GetLiterals();
            l1.AddRange(This.GetLiterals());

            return l1;
        }
    }

    public class ConflictFunctionInvocation : FunctionInvocation
    {
        public ConflictFunctionInvocation(Language language, string value) : base(language,
                                                                                  GetDummy(language))
        {
            _value = value;
        }

        public override string MakeCode()
        {
            throw new InvalidOperationException(Configuration.GetLocalized("The function is conflicting."));
        }

        public override string MakeUserReadable()
        {
            return _value;
        }

        static Function GetDummy(Language language)
        {
            if(_dummy == null) {
                _dummy = new Function(new Type(language, "void"), null, "dummy", false);
            }
            return _dummy;
        }

        string _value;
        static Function _dummy;
    }

    /// <summary>
    /// This class wraps an expression into a function. For instance, an Action can have the expression ++$i attached to it, wrapped into a function for the runtime.
    /// </summary>
    public class WrapperFunctionInvocation : FunctionInvocation
    {
        public WrapperFunctionInvocation(Language language,
                                         Type returnType,
                                         Expression expr) : base(language,
                                                                     GetWrapperFunction(language,
                                                                                        returnType),
                                                                     expr)
        {
				
        }

        public static Function GetWrapperFunction(Language language, Type returnType)
        {
            var f = new Function(returnType,
                                 Scope.MakeFromNamespace(language, "Utility"),
                                 "",
                                 false);
            f.AddParam(new Param(new Type(language, "void"), "param"));
            return f;
        }

        public override bool NeedsReturn {
            get {
                if(Language == Language.C) {
                    return true;
                }

                return false;
            }
        }

        public override string MakeCode()
        {
            if(Language == Language.C) {
                return Arguments[0].MakeCode() + ";";
            }
            else if(Language == Language.Cpp) {
                return "([&petriNet]() -> " + Function.ReturnType.Name + " { " + Arguments[0].MakeCode() + "; return {}; })()";
            }
            else if(Language == Language.CSharp) {
                return "new System.Func<ActionResult>(() => { " + Arguments[0].MakeCode() + "; return default(" + Function.ReturnType.Name + "); }).Invoke()";
            }

            throw new Exception("WrapperFunctionInvocation.MakeCode: Should not get there!");
        }

        public override string MakeUserReadable()
        {
            return Arguments[0].MakeUserReadable();
        }
    }
}

