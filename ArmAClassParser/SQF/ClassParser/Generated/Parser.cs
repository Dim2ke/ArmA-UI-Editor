using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQF.ClassParser;



using System;


namespace SQF.ClassParser
{
    public class Parser {
    	public const int _EOF = 0;
	public const int _T_SCALAR = 1;
	public const int _T_STRING = 2;
	public const int _T_STRINGTABLESTRING = 3;
	public const int _T_IDENT = 4;
	public const int maxT = 16;

        const bool _T = true;
        const bool _x = false;
        const int minErrDist = 2;
        
        public Scanner scanner;
        public Errors  errors;

        public Token t;    // last recognized token
        public Token la;   // lookahead token
        int errDist = minErrDist;

    

        public SQF.ClassParser.File Base { get; set;}
        
        public Parser(Scanner scanner) {
            this.scanner = scanner;
            errors = new Errors();
        }
        
        bool peekCompare(params int[] values)
        {
            Token t = la;
            foreach(int i in values)
            {
                if(i != -1 && t.kind != i)
                {
                    scanner.ResetPeek();
                    return false;
                }
                if (t.next == null)
                    t = scanner.Peek();
                else
                    t = t.next;
            }
            scanner.ResetPeek();
            return true;
        }
        bool peekString(int count, params string[] values)
        {
            Token t = la;
            for(; count > 0; count --)
                t = scanner.Peek();
            foreach(var it in values)
            {
                if(t.val == it)
                {
                    scanner.ResetPeek();
                    return true;
                }
            }
            scanner.ResetPeek();
            return false;
        }
        
        
        void SynErr (int n) {
            if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
            errDist = 0;
        }
        void Warning (string msg) {
            errors.Warning(la.line, la.col, msg);
        }

        public void SemErr (string msg) {
            if (errDist >= minErrDist) errors.SemErr(t.line, t.col, msg);
            errDist = 0;
        }
        
        void Get () {
            for (;;) {
                t = la;
                la = scanner.Scan();
                if (la.kind <= maxT) { ++errDist; break; }
    
                la = t;
            }
        }
        
        void Expect (int n) {
            if (la.kind==n) Get(); else { SynErr(n); }
        }
        
        bool StartOf (int s) {
            return set[s, la.kind];
        }
        
        void ExpectWeak (int n, int follow) {
            if (la.kind == n) Get();
            else {
                SynErr(n);
                while (!StartOf(follow)) Get();
            }
        }


        bool WeakSeparator(int n, int syFol, int repFol) {
            int kind = la.kind;
            if (kind == n) {Get(); return true;}
            else if (StartOf(repFol)) {return false;}
            else {
                SynErr(n);
                while (!(set[syFol, kind] || set[repFol, kind] || set[0, kind])) {
                    Get();
                    kind = la.kind;
                }
                return StartOf(syFol);
            }
        }

        
    	void CONFIGFILE() {
		Data data; 
		if(Base == null)
		{
		   Base = new SQF.ClassParser.File();
		}
		else
		{
		   Base.BeginEdit();
		}
		
		CONFIG(out data, Base);
		Base[data] = data; 
		while (la.kind == 5) {
			CONFIG(out data, Base);
			Base[data] = data; 
		}
		if(errors.count == 0)
		{
		   Base.EndEdit();
		}
		else
		{
		   Base.CancelEdit();
		}
		
	}

	void CONFIG(out Data data, ConfigClass parent) {
		ConfigClass cc = new ConfigClass(); data = new Data(cc); Data d; 
		Expect(5);
		Expect(4);
		if(parent.ContainsKey(t.val))
		{
		   SemErr("Class is already defined in current scope");
		}
		data.Name = t.val;
		
		if (la.kind == 6) {
			Get();
			Expect(4);
			if(parent.ContainsKey(t.val))
			{
			   cc.Parent = parent[t.val].Class;
			}
			else
			{
			   SemErr("Parent is not yet existing");
			};
			
		}
		if (la.kind == 7) {
			Get();
			while (la.kind == 4 || la.kind == 5) {
				if (la.kind == 4) {
					FIELD(out d, data.Class);
					data.Class[d] = d; 
				} else {
					CONFIG(out d, data.Class);
					data.Class[d] = d; 
				}
			}
			Expect(8);
		}
		Expect(9);
	}

	void FIELD(out Data data, ConfigClass parent) {
		data = null; bool isArray = false; 
		Expect(4);
		string name = t.val; 
		if (la.kind == 10) {
			Get();
			Expect(11);
			isArray = true; 
		}
		Expect(12);
		if (la.kind == 7) {
			ARRAY(out data);
			if(!isArray) SemErr("Invalid field syntax: Missing [] at field name"); 
		} else if (la.kind == 1) {
			SCALAR(out data);
			if(isArray) SemErr("Invalid field syntax: Located [] at field name"); 
		} else if (la.kind == 2 || la.kind == 3) {
			STRING(out data);
			if(isArray) SemErr("Invalid field syntax: Located [] at field name"); 
		} else if (la.kind == 13 || la.kind == 14) {
			BOOLEAN(out data);
			if(isArray) SemErr("Invalid field syntax: Located [] at field name"); 
		} else SynErr(17);
		Expect(9);
		if(data != null) data.Name = name; 
	}

	void ARRAY(out Data data) {
		data = null; Data tmp; 
		Expect(7);
		if (StartOf(1)) {
			if (la.kind == 1) {
				var list = new List<double>(); 
				SCALAR(out tmp);
				list.Add(tmp.Number); 
				while (la.kind == 15) {
					Get();
					SCALAR(out tmp);
					list.Add(tmp.Number); 
				}
				data = new Data(list); 
			} else if (la.kind == 2 || la.kind == 3) {
				var list = new List<string>(); 
				STRING(out tmp);
				list.Add(tmp.String); 
				while (la.kind == 15) {
					Get();
					STRING(out tmp);
					list.Add(tmp.String); 
				}
				data = new Data(list); 
			} else {
				var list = new List<bool>(); 
				BOOLEAN(out tmp);
				list.Add(tmp.Boolean); 
				while (la.kind == 15) {
					Get();
					BOOLEAN(out tmp);
					list.Add(tmp.Boolean); 
				}
				data = new Data(list); 
			}
		}
		Expect(8);
	}

	void SCALAR(out Data data) {
		Expect(1);
		data = new Data(Double.Parse(t.val)); 
	}

	void STRING(out Data data) {
		string content = default(string); 
		if (la.kind == 2) {
			Get();
			content = t.val.Substring(1, t.val.Length - 2); 
		} else if (la.kind == 3) {
			Get();
			content = t.val.Substring(1); 
		} else SynErr(18);
		data = new Data(content);
		
	}

	void BOOLEAN(out Data data) {
		bool flag = false; 
		if (la.kind == 13) {
			Get();
			flag = true; 
		} else if (la.kind == 14) {
			Get();
		} else SynErr(19);
		data = new Data(flag);
		
	}



        public void Parse() {
            la = new Token();
            la.val = "";		
            Get();
    		CONFIGFILE();
		Expect(0);

        }
        
        static readonly bool[,] set = {
    		{_T,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x},
		{_x,_T,_T,_T, _x,_x,_x,_x, _x,_x,_x,_x, _x,_T,_T,_x, _x,_x}

        };
    } // end Parser


    public class Errors {
        public int count = 0;                                    // number of errors detected
        public System.IO.TextWriter errorStream = Console.Out;   // error messages go to this stream
        public string errMsgFormat = "line {0} col {1}: {2}"; // 0=line, 1=column, 2=text

        public virtual void SynErr (int line, int col, int n) {
            string s;
            switch (n) {
    			case 0: s = "EOF expected"; break;
			case 1: s = "T_SCALAR expected"; break;
			case 2: s = "T_STRING expected"; break;
			case 3: s = "T_STRINGTABLESTRING expected"; break;
			case 4: s = "T_IDENT expected"; break;
			case 5: s = "\"class\" expected"; break;
			case 6: s = "\":\" expected"; break;
			case 7: s = "\"{\" expected"; break;
			case 8: s = "\"}\" expected"; break;
			case 9: s = "\";\" expected"; break;
			case 10: s = "\"[\" expected"; break;
			case 11: s = "\"]\" expected"; break;
			case 12: s = "\"=\" expected"; break;
			case 13: s = "\"true\" expected"; break;
			case 14: s = "\"false\" expected"; break;
			case 15: s = "\",\" expected"; break;
			case 16: s = "??? expected"; break;
			case 17: s = "invalid FIELD"; break;
			case 18: s = "invalid STRING"; break;
			case 19: s = "invalid BOOLEAN"; break;

                default: s = "error " + n; break;
            }
            Logger.Instance.log(Logger.LogLevel.ERROR, String.Format(errMsgFormat, line, col, s));
            //errorStream.WriteLine(errMsgFormat, line, col, s);
            count++;
        }

        public virtual void SemErr (int line, int col, string s) {
            Logger.Instance.log(Logger.LogLevel.ERROR, String.Format(errMsgFormat, line, col, s));
            //errorStream.WriteLine(errMsgFormat, line, col, s);
            count++;
        }
        
        public virtual void SemErr (string s) {
            Logger.Instance.log(Logger.LogLevel.ERROR, s);
            //errorStream.WriteLine(s);
            count++;
        }
        
        public virtual void Warning (int line, int col, string s) {
            Logger.Instance.log(Logger.LogLevel.WARNING, String.Format(errMsgFormat, line, col, s));
            //errorStream.WriteLine(errMsgFormat, line, col, s);
        }
        
        public virtual void Warning(string s) {
            Logger.Instance.log(Logger.LogLevel.WARNING, s);
            //errorStream.WriteLine(s);
        }
    } // Errors


    public class FatalError: Exception {
        public FatalError(string m): base(m) {}
    }
}
