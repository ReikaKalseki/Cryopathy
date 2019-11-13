using System;
using System.IO;    //For data read/write methods
using System.Collections;   //Working with Lists and Collections
using System.Collections.Generic;   //Working with Lists and Collections
using System.Linq;   //More advanced manipulation of lists/collections
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using UnityEngine;  //Needed for most Unity Enginer manipulations: Vectors, GameObjects, Audio, etc.

namespace ReikaKalseki.Cryopathy
{
	public class InstructionHandlers
	{
		public InstructionHandlers() {
			
		}
		
		internal static void nullInstructions(List<CodeInstruction> li, int begin, int end) {
			for (int i = begin; i <= end; i++) {
				CodeInstruction insn = li[i];
				insn.opcode = OpCodes.Nop;
				insn.operand = null;
			}
		}
		
		internal static CodeInstruction createMethodCall(string owner, string name, bool instance, params string[] args) {
			return new CodeInstruction(OpCodes.Call, convertMethodOperand(owner, name, instance, args));
		}
		
		internal static CodeInstruction createMethodCall(string owner, string name, bool instance, params Type[] args) {
			return new CodeInstruction(OpCodes.Call, convertMethodOperand(owner, name, instance, args));
		}
		
		internal static MethodInfo convertMethodOperand(string owner, string name, bool instance, params string[] args) {
			Type[] types = new Type[args.Length];
			for (int i = 0; i < args.Length; i++) {
				types[i] = AccessTools.TypeByName(args[i]);
			}
			return convertMethodOperand(owner, name, instance, types);
		}
		
		internal static MethodInfo convertMethodOperand(string owner, string name, bool instance, params Type[] args) {
			MethodInfo ret = AccessTools.Method(AccessTools.TypeByName(owner), name, args);
			//ret.IsStatic = !instance;
			return ret;
		}
		
		internal static FieldInfo convertFieldOperand(string owner, string name) {
			return AccessTools.Field(AccessTools.TypeByName(owner), name);
		}
		
		internal static int getInstruction(List<CodeInstruction> li, int start, int index, OpCode opcode, params object[] args) {
			int count = 0;
			for (int i = start; i < li.Count; i++) {
				CodeInstruction insn = li[i];
				if (insn.opcode == opcode) {
					if (match(insn, args)) {
						if (count == index)
							return i;
						else
							count++;
					}
				}
			}
			return -1;
		}
		
		internal static int getLastInstructionBefore(List<CodeInstruction> li, int before, OpCode opcode, params object[] args) {
			for (int i = before-1; i >= 0; i--) {
				CodeInstruction insn = li[i];
				if (insn.opcode == opcode) {
					if (match(insn, args)) {
						return i;
					}
				}
			}
			return -1;
		}
		
		internal static bool match(CodeInstruction a, CodeInstruction b) {
			return a.opcode == b.opcode && a.operand == b.operand;
		}
		
		internal static bool match(CodeInstruction insn, params object[] args) {
			//FileLog.Log("Comparing "+insn.operand.GetType()+" "+insn.operand.ToString()+" against seek of "+String.Join(",", args.Select(p=>p.ToString()).ToArray()));
			if (insn.opcode == OpCodes.Call || insn.opcode == OpCodes.Callvirt) { //string class, string name, bool instance, string args
				MethodInfo info = convertMethodOperand((string)args[0], (string)args[1], (bool)args[2], (Type[])args[3]);
				return insn.operand == info;
			}
			else if (insn.opcode == OpCodes.Isinst || insn.opcode == OpCodes.Newobj) { //string class
				return insn.operand == AccessTools.TypeByName((string)args[0]);
			}
			else if (insn.opcode == OpCodes.Ldfld || insn.opcode == OpCodes.Stfld || insn.opcode == OpCodes.Ldsfld || insn.opcode == OpCodes.Stsfld) { //string class, string name
				FieldInfo info = convertFieldOperand((string)args[0], (string)args[1]);
				return insn.operand == info;
			}
			else if (insn.opcode == OpCodes.Ldarg) { //int pos
				return insn.operand == args[0];
			}
			return true;
		}
		
		internal static string toString(List<CodeInstruction> li) {
			return String.Join("\n", li.Select(p=>toString(p)).ToArray());
		}
		
		internal static string toString(List<CodeInstruction> li, int idx) {
			return "#"+idx+" = "+toString(li[idx]);
		}
		
		internal static string toString(CodeInstruction ci) {
			return ci.opcode.Name+" "+(ci.operand != null ? ci.operand.ToString() : "");
		}
	}
}
