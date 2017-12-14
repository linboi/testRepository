/*----------------------------------------------------------------------
Compiler Generator Coco/R,
Copyright (c) 1990, 2004 Hanspeter Moessenboeck, University of Linz
extended by M. Loeberbauer & A. Woess, Univ. of Linz
with improvements by Pat Terry, Rhodes University

This program is free software; you can redistribute it and/or modify it 
under the terms of the GNU General Public License as published by the 
Free Software Foundation; either version 2, or (at your option) any 
later version.

This program is distributed in the hope that it will be useful, but 
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY 
or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License 
for more details.

You should have received a copy of the GNU General Public License along 
with this program; if not, write to the Free Software Foundation, Inc., 
59 Temple Place - Suite 330, Boston, MA 02111-1307, USA.

As an exception, it is allowed to write an extension of Coco/R that is
used as a plugin in non-free software.

If not otherwise stated, any source code generated by Coco/R (other than 
Coco/R itself) does not fall under the GNU General Public License.
-----------------------------------------------------------------------*/

using System;

namespace Tastier {



public class Parser {
	public const int _EOF = 0;
	public const int _number = 1;
	public const int _ident = 2;
	public const int _string = 3;
	public const int maxT = 46;

	const bool T = true;
	const bool x = false;
	const int minErrDist = 2;
	
	public Scanner scanner;
	public Errors  errors;

	public Token t;    // last recognized token
	public Token la;   // lookahead token
	int errDist = minErrDist;

const int // object kinds
      var = 0, proc = 1, constant = 3;

   const int // types
      undef = 0, integer = 1, boolean = 2;
      
   const int // sorts
      scalar = 0, array = 1;

   public SymbolTable tab;
   public CodeGenerator gen;
  
/*-------------------------------------------------------------------------------------------*/



	public Parser(Scanner scanner) {
		this.scanner = scanner;
		errors = new Errors();
	}

	void SynErr (int n) {
		if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
		errDist = 0;
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

	
	void AddOp(out Op op) {
		op = Op.ADD; 
		if (la.kind == 4) {
			Get();
		} else if (la.kind == 5) {
			Get();
			op = Op.SUB; 
		} else SynErr(47);
	}

	void Expr(out int reg,        // load value of Expr into register
out int type) {
		int typeR, regR; Op op; 
		SimExpr(out reg,
out type);
		if (StartOf(1)) {
			RelOp(out op);
			Expr(out regR,
  out typeR);
			if (type == typeR) {
			  type = boolean;
			  gen.RelOp(op, reg, regR);
			}
			else SemErr("incompatible types");
			
			if (la.kind == 6) {
				Get();
				ConditionalOp(out reg, out type);
			}
		}
		gen.ClearRegisters(); 
	}

	void SimExpr(out int reg,     //load value of SimExpr into register
out int type) {
		int typeR, regR; Op op; 
		Term(out reg,
out type);
		while (la.kind == 4 || la.kind == 5) {
			AddOp(out op);
			Term(out regR,
out typeR);
			if (type == integer && typeR == integer)
			  gen.AddOp(op, reg, regR);
			else SemErr("integer type expected");
			
		}
	}

	void RelOp(out Op op) {
		op = Op.EQU; 
		switch (la.kind) {
		case 22: {
			Get();
			break;
		}
		case 23: {
			Get();
			op = Op.LSS; 
			break;
		}
		case 24: {
			Get();
			op = Op.GTR; 
			break;
		}
		case 25: {
			Get();
			op = Op.NEQ; 
			break;
		}
		case 26: {
			Get();
			op = Op.LEQ; 
			break;
		}
		case 27: {
			Get();
			op = Op.GEQ; 
			break;
		}
		default: SynErr(48); break;
		}
	}

	void ConditionalOp(out int reg, 
out int type) {
		int l1, l2; l1 = 0; 
		l1 = gen.NewLabel(); 
		l2 = gen.NewLabel();
		
		gen.BranchFalse(l1);
		Expr(out reg, out type);
		gen.Branch(l2); 
		Expect(7);
		gen.Label(l1); 
		Expr(out reg, out type);
		gen.Label(l2); 
	}

	void Primary(out int reg,     // load Primary into register
out int type) {
		int n, index = 0; Obj obj; string name; 
		type = undef;
		reg = gen.GetRegister();
		
		switch (la.kind) {
		case 2: {
			Ident(out name);
			if (la.kind == 8) {
				Get();
				Expr(out reg,
out type);
				index = reg;//gen.GetRegister();
				//gen.LoadConstant(index, Int32.Parse(t.val));
				
				Expect(9);
			}
			obj = tab.Find(name); type = obj.type;
			if(obj.sort == scalar) {
			
			if (obj.kind == var) {
			  		if (obj.level == 0)
			     		gen.LoadGlobal(reg, obj.adr, name);
			  	else
			     	gen.LoadLocal(reg, tab.curLevel-obj.level, obj.adr, name);
			  	if (type == boolean)
			  		// reset Z flag in CPSR
			       gen.ResetZ(reg);
			}
			else SemErr("variable expected");
			}
			else if(obj.sort == array)
			{
			if (obj.kind == var) {
			  		if (obj.level == 0)
			     		gen.LoadIndexedGlobal(reg, obj.adr, index, name);
			  	else
			     	gen.LoadIndexedLocal(reg, tab.curLevel-obj.level, obj.adr, index, name);
			  	if (type == boolean)
			  		// reset Z flag in CPSR
			       gen.ResetZ(reg);
			}
			else SemErr("variable expected");
			}
			else SemErr("variable expected");
			
			break;
		}
		case 1: {
			Get();
			type = integer;
			n = Convert.ToInt32(t.val);
			gen.LoadConstant(reg, n);
			
			break;
		}
		case 5: {
			Get();
			Primary(out reg,
out type);
			if (type == integer)
			  gen.NegateValue(reg);
			else SemErr("integer type expected");
			
			break;
		}
		case 10: {
			Get();
			type = boolean;
			gen.LoadTrue(reg);
			
			break;
		}
		case 11: {
			Get();
			type = boolean;
			gen.LoadFalse(reg);
			
			break;
		}
		case 12: {
			Get();
			Expr(out reg,
out type);
			Expect(13);
			break;
		}
		default: SynErr(49); break;
		}
	}

	void Ident(out string name) {
		Expect(2);
		name = t.val; 
	}

	void String(out string text) {
		Expect(3);
		text = t.val; 
	}

	void MulOp(out Op op) {
		op = Op.MUL; 
		if (la.kind == 14) {
			Get();
		} else if (la.kind == 15 || la.kind == 16) {
			if (la.kind == 15) {
				Get();
			} else {
				Get();
			}
			op = Op.DIV; 
		} else if (la.kind == 17 || la.kind == 18) {
			if (la.kind == 17) {
				Get();
			} else {
				Get();
			}
			op = Op.MOD; 
		} else SynErr(50);
	}

	void ProcDecl(string progName) {
		string procName; 
		Expect(19);
		Ident(out procName);
		tab.NewObj(procName, proc, undef, scalar);
		if (procName == "main")
		  if (tab.curLevel == 0)
		     tab.mainPresent = true;
		  else SemErr("main not at lexic level 0");
		tab.OpenScope();
		
		Expect(12);
		Expect(13);
		Expect(20);
		while (la.kind == 42 || la.kind == 43 || la.kind == 45) {
			if (la.kind == 42 || la.kind == 43) {
				VarDecl();
			} else if (la.kind == 45) {
				ConstDecl();
			} else {
				ArrayDecl();
			}
		}
		while (la.kind == 19) {
			ProcDecl(progName);
		}
		if (procName == "main")
		  gen.Label("Main", "Body");
		else {
		  gen.ProcNameComment(procName);
		  gen.Label(procName, "Body");
		}
		
		Stat();
		while (StartOf(2)) {
			Stat();
		}
		Expect(21);
		if (procName == "main") {
		  gen.StopProgram(progName);
		  gen.Enter("Main", tab.curLevel, tab.topScope.nextAdr);
		} else {
		  gen.Return(procName);
		  gen.Enter(procName, tab.curLevel, tab.topScope.nextAdr);
		}
		tab.CloseScope();
		
	}

	void VarDecl() {
		string name; int type; Obj obj; 
		Type(out type);
		Ident(out name);
		obj = tab.NewObj(name, var, type, scalar); 
		if (la.kind == 8) {
			Get();
			Expect(1);
			obj.size = Int32.Parse(t.val); tab.convertToArray(obj, (Int32.Parse(t.val))); 
			Expect(9);
		}
		while (la.kind == 44) {
			Get();
			Ident(out name);
			tab.NewObj(name, var, type, scalar); 
		}
		Expect(29);
	}

	void ConstDecl() {
		int type; string name; Obj obj; int reg; 
		Expect(45);
		Type(out type);
		Ident(out name);
		obj = tab.NewObj(name, constant, type, scalar); 
		Expect(22);
		Expr(out reg,
out type);
		Expect(29);
		if (type == obj.type)
		   gen.StoreLocal(reg, tab.curLevel-obj.level, obj.adr, name);
		else SemErr("incompatible types");
		
	}

	void ArrayDecl() {
		string name; int type; Obj obj; 
		Type(out type);
		Ident(out name);
		obj = tab.NewObj(name, var, type, array); 
		Expect(8);
		Expect(1);
		obj.size = Int32.Parse(t.val); 
		Expect(9);
		Expect(29);
	}

	void Stat() {
		int type, typeI, index = 0; string name; Obj obj; int reg, regI; 
		switch (la.kind) {
		case 2: {
			Ident(out name);
			obj = tab.Find(name); 
			if (la.kind == 8) {
				Get();
				Expr(out regI, 
out typeI);
				if(obj.sort == array)
				{
				index = regI; //gen.GetRegister();
				//gen.LoadConstant(index, Int32.Parse(t.val));
				}
				else
				{
				SemErr("cannot index off a scalar value");
				index = 0;
				} 
				
				Expect(9);
			}
			if (la.kind == 28) {
				Get();
				if (obj.kind == constant)
				  SemErr("cannot reassign constant");
				else if(obj.kind != var)
				SemErr("cannot assign to procedure");
				  
				
				Expr(out reg,
out type);
				Expect(29);
				int l1 = gen.NewLabel();
				             if (type == obj.type)
				                if (obj.level == 0)
				                	  if(obj.sort == scalar)
				                   	gen.StoreGlobal(reg, obj.adr, name);
				                   else
				                   {
				                   	gen.GetRegister();
				                   	int tmp = gen.GetRegister();
				                   	gen.MoveRegister(tmp, reg);
				                   	gen.WriteString("Afdsafasdfdsafsd");
				                   	gen.RelOp(Op.LSS, tmp, index);
				                   	gen.BranchFalse(l1);
				                   	gen.WriteString("\"jesus youre after goin outta bounds lad\"");
				                   	gen.Label(l1);
				                   	gen.StoreIndexedGlobal(reg, obj.adr, index, name);
				                   }
				                else if(obj.sort == scalar)
				                		gen.StoreLocal(reg, tab.curLevel-obj.level, obj.adr, name);
				                else
				                		gen.StoreIndexedLocal(reg, tab.curLevel-obj.level, obj.adr, index, name);
				            else SemErr("incompatible types");
				          
			} else if (la.kind == 12) {
				Get();
				Expect(13);
				Expect(29);
				if (obj.kind == proc)
				  gen.Call(name);
				else SemErr("object is not a procedure");
				
			} else SynErr(51);
			break;
		}
		case 30: {
			Get();
			int l1, l2; l1 = 0; 
			Expr(out reg,
out type);
			if (type == boolean) {
			  l1 = gen.NewLabel();
			  gen.BranchFalse(l1);
			}
			else SemErr("boolean type expected");
			
			Stat();
			l2 = gen.NewLabel();
			gen.Branch(l2);
			gen.Label(l1);
			
			if (la.kind == 31) {
				Get();
				Stat();
			}
			gen.Label(l2); 
			break;
		}
		case 32: {
			Get();
			int l1, l2;
			l1 = gen.NewLabel();
			gen.Label(l1); l2=0;
			
			Expr(out reg,
out type);
			if (type == boolean) {
			  l2 = gen.NewLabel();
			  gen.BranchFalse(l2);
			}
			else SemErr("boolean type expected");
			
			Stat();
			gen.Branch(l1);
			gen.Label(l2);
			
			break;
		}
		case 33: {
			Get();
			int reg2 = gen.GetRegister();
			int type2, l1, l2, l3;
			
			Expr(out reg,	
out type);
			Expect(7);
			l1 = gen.NewLabel();
			l2 = gen.NewLabel();
			l3 = gen.NewLabel();
			
			while (la.kind == 34) {
				Get();
				gen.Label(l2);  
				Expr(out reg2,
out type2);
				Expect(7);
				if( type == type2)
				{	
				gen.RelOp(Op.EQU,reg2,reg);
				l2 = gen.NewLabel();
				gen.BranchFalse(l2);
				gen.Label(l1);
				l1 = gen.NewLabel();
				}
				else
				SemErr("case expression must be the same type as switch expression");
				
				Stat();
				if (la.kind == 35) {
					Get();
					gen.Branch(l3);	
				}
				gen.Branch(l1); 
			}
			Expect(36);
			gen.Label(l2);
			gen.Label(l1);
			//gen.ClearRegisters();
			
			Stat();
			gen.Label(l3); 
			break;
		}
		case 37: {
			Get();
			int l1, l2, l3, l4;
			l1 = gen.NewLabel(); l2 = gen.NewLabel(); 
			l3 = gen.NewLabel(); l4 = gen.NewLabel();
			
			Stat();
			gen.Label(l1); 
			Expr(out reg, out type);
			if (type == boolean) 
			{
			                      		gen.BranchFalse(l2);
			                      		gen.BranchTrue(l4);
			                   	}
			                   	else SemErr("boolean type expected");
			                   
			Expect(29);
			gen.Label(l3); 
			Stat();
			gen.Branch(l1); 
			Expect(13);
			gen.Label(l4);  
			Stat();
			gen.Branch(l3); 
			gen.Label(l2); 
			break;
		}
		case 38: {
			Get();
			Ident(out name);
			Expect(29);
			obj = tab.Find(name);
			if (obj.type == integer) {
			  gen.ReadInteger(); 
			  if (obj.level == 0)
			     gen.StoreGlobal(0, obj.adr, name);
			  else gen.StoreLocal(0, tab.curLevel-obj.level, obj.adr, name);
			}
			else SemErr("integer type expected");
			
			break;
		}
		case 39: {
			Get();
			string text; 
			if (StartOf(3)) {
				Expr(out reg,
out type);
				switch (type) {
				  case integer: gen.WriteInteger(reg, false);
				                break; 
				  case boolean: gen.WriteBoolean(false);
				                break;
				}
				
			} else if (la.kind == 3) {
				String(out text);
				gen.WriteString(text); 
			} else SynErr(52);
			Expect(29);
			break;
		}
		case 40: {
			Get();
			Expr(out reg,
out type);
			switch (type) {
			  case integer: gen.WriteInteger(reg, true);
			                break;
			  case boolean: gen.WriteBoolean(true);
			                break;
			}
			
			Expect(29);
			break;
		}
		case 20: {
			Get();
			tab.OpenSubScope(); 
			while (la.kind == 42 || la.kind == 43 || la.kind == 45) {
				if (la.kind == 42 || la.kind == 43) {
					VarDecl();
				} else if (la.kind == 45) {
					ConstDecl();
				} else {
					ArrayDecl();
				}
			}
			Stat();
			while (StartOf(2)) {
				Stat();
			}
			Expect(21);
			tab.CloseSubScope(); 
			break;
		}
		default: SynErr(53); break;
		}
	}

	void Term(out int reg,        // load value of Term into register
out int type) {
		int typeR, regR; Op op; 
		Primary(out reg,
out type);
		while (StartOf(4)) {
			MulOp(out op);
			Primary(out regR,
out typeR);
			if (type == integer && typeR == integer)
			  gen.MulOp(op, reg, regR);
			else SemErr("integer type expected");
			
		}
	}

	void Tastier() {
		string progName; 
		Expect(41);
		Ident(out progName);
		tab.OpenScope(); 
		Expect(20);
		while (la.kind == 42 || la.kind == 43 || la.kind == 45) {
			if (la.kind == 42 || la.kind == 43) {
				VarDecl();
			} else if (la.kind == 45) {
				ConstDecl();
			} else {
				ArrayDecl();
			}
		}
		while (la.kind == 19) {
			ProcDecl(progName);
		}
		tab.CloseScope(); 
		Expect(21);
	}

	void Type(out int type) {
		type = undef; 
		if (la.kind == 42) {
			Get();
			type = integer; 
		} else if (la.kind == 43) {
			Get();
			type = boolean; 
		} else SynErr(54);
	}



	public void Parse() {
		la = new Token();
		la.val = "";		
		Get();
		Tastier();
		Expect(0);

	}
	
	static readonly bool[,] set = {
		{T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, T,T,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
		{x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,T,x, T,T,x,x, x,T,T,T, T,x,x,x, x,x,x,x},
		{x,T,T,x, x,T,x,x, x,x,T,T, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x}

	};
} // end Parser


public class Errors {
	public int count = 0;                                    // number of errors detected
    public System.IO.TextWriter errorStream = Console.Error; // error messages go to this stream - was Console.Out DMA
    public string errMsgFormat = "-- line {0} col {1}: {2}"; // 0=line, 1=column, 2=text

	public virtual void SynErr (int line, int col, int n) {
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "number expected"; break;
			case 2: s = "ident expected"; break;
			case 3: s = "string expected"; break;
			case 4: s = "\"+\" expected"; break;
			case 5: s = "\"-\" expected"; break;
			case 6: s = "\"?\" expected"; break;
			case 7: s = "\":\" expected"; break;
			case 8: s = "\"[\" expected"; break;
			case 9: s = "\"]\" expected"; break;
			case 10: s = "\"true\" expected"; break;
			case 11: s = "\"false\" expected"; break;
			case 12: s = "\"(\" expected"; break;
			case 13: s = "\")\" expected"; break;
			case 14: s = "\"*\" expected"; break;
			case 15: s = "\"div\" expected"; break;
			case 16: s = "\"DIV\" expected"; break;
			case 17: s = "\"mod\" expected"; break;
			case 18: s = "\"MOD\" expected"; break;
			case 19: s = "\"void\" expected"; break;
			case 20: s = "\"{\" expected"; break;
			case 21: s = "\"}\" expected"; break;
			case 22: s = "\"=\" expected"; break;
			case 23: s = "\"<\" expected"; break;
			case 24: s = "\">\" expected"; break;
			case 25: s = "\"!=\" expected"; break;
			case 26: s = "\"<=\" expected"; break;
			case 27: s = "\">=\" expected"; break;
			case 28: s = "\":=\" expected"; break;
			case 29: s = "\";\" expected"; break;
			case 30: s = "\"if\" expected"; break;
			case 31: s = "\"else\" expected"; break;
			case 32: s = "\"while\" expected"; break;
			case 33: s = "\"switch\" expected"; break;
			case 34: s = "\"case\" expected"; break;
			case 35: s = "\"break;\" expected"; break;
			case 36: s = "\"default:\" expected"; break;
			case 37: s = "\"for(\" expected"; break;
			case 38: s = "\"read\" expected"; break;
			case 39: s = "\"write\" expected"; break;
			case 40: s = "\"writeln\" expected"; break;
			case 41: s = "\"program\" expected"; break;
			case 42: s = "\"int\" expected"; break;
			case 43: s = "\"bool\" expected"; break;
			case 44: s = "\",\" expected"; break;
			case 45: s = "\"const\" expected"; break;
			case 46: s = "??? expected"; break;
			case 47: s = "invalid AddOp"; break;
			case 48: s = "invalid RelOp"; break;
			case 49: s = "invalid Primary"; break;
			case 50: s = "invalid MulOp"; break;
			case 51: s = "invalid Stat"; break;
			case 52: s = "invalid Stat"; break;
			case 53: s = "invalid Stat"; break;
			case 54: s = "invalid Type"; break;

			default: s = "error " + n; break;
		}
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}

	public virtual void SemErr (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}
	
	public virtual void SemErr (string s) {
		errorStream.WriteLine(s);
		count++;
	}
	
	public virtual void Warning (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
	}
	
	public virtual void Warning(string s) {
		errorStream.WriteLine(s);
	}
} // Errors


public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}
}