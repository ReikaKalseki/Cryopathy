/*
 * Created by SharpDevelop.
 * User: Reika
 * Date: 04/11/2019
 * Time: 11:28 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.IO;    //For data read/write methods
using System.Collections;   //Working with Lists and Collections
using System.Collections.Generic;   //Working with Lists and Collections
using System.Linq;   //More advanced manipulation of lists/collections
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using UnityEngine;  //Needed for most Unity Enginer manipulations: Vectors, GameObjects, Audio, etc.

namespace ReikaKalseki.Cryopathy {
	
	[HarmonyPatch(typeof(ColdCreepSpawner))]
	[HarmonyPatch("UpdateCreep")]
	public static class CreepSeekPatch {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				int loc = InstructionHandlers.getInstruction(codes, 0, 0, OpCodes.Call, "CubeHelper", "IsReinforced", false, new Type[]{typeof(ushort)});
				FileLog.Log("Running patch, which found instruction "+InstructionHandlers.toString(codes, loc));
				codes[loc].operand = InstructionHandlers.convertMethodOperand("ReikaKalseki.Cryopathy.CryopathyMod", "shouldAvoidBlock", false, typeof(ushort));
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
				//FileLog.Log("Codes are "+InstructionHandlers.toString(codes));
			}
			catch (Exception e) {
				FileLog.Log("Caught exception when running patch "+MethodBase.GetCurrentMethod().DeclaringType+"!");
				FileLog.Log(e.Message);
				FileLog.Log(e.StackTrace);
				FileLog.Log(e.ToString());
			}
			return codes.AsEnumerable();
		}
	}
	
	[HarmonyPatch(typeof(ColdCreepSpawner))]
	[HarmonyPatch("BuildCreepAt")]
	public static class CreepBuildPatch {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				int loc = InstructionHandlers.getInstruction(codes, 0, 0, OpCodes.Callvirt, "Segment", "GetCube", true, new Type[]{typeof(long), typeof(long), typeof(long)});
				FileLog.Log("Running patch, which found instruction "+InstructionHandlers.toString(codes, loc));
				codes[loc].opcode = OpCodes.Call;
				codes[loc].operand = InstructionHandlers.convertMethodOperand("ReikaKalseki.Cryopathy.CryopathyMod", "getCubeForCryoCheckAt", false, typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(ushort));
				codes.Insert(loc, new CodeInstruction(OpCodes.Ldloc_1));
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
				//FileLog.Log("Codes are "+InstructionHandlers.toString(codes));
			}
			catch (Exception e) {
				FileLog.Log("Caught exception when running patch "+MethodBase.GetCurrentMethod().DeclaringType+"!");
				FileLog.Log(e.Message);
				FileLog.Log(e.StackTrace);
				FileLog.Log(e.ToString());
			}
			return codes.AsEnumerable();
		}
	}
	
	[HarmonyPatch(typeof(Segment))]
	[HarmonyPatch("AttemptToMoveFluid")]
	public static class FluidMovePatch {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				int loc = InstructionHandlers.getLastInstructionBefore(codes, codes.Count, OpCodes.Callvirt, "Segment", "SetCubeTypeNoChecking", true, new Type[]{typeof(int), typeof(int), typeof(int), typeof(ushort), typeof(ushort)});
				FileLog.Log("Running patch, which found instruction "+InstructionHandlers.toString(codes, loc));
				codes[loc].opcode = OpCodes.Call;
				codes[loc].operand = InstructionHandlers.convertMethodOperand("ReikaKalseki.Cryopathy.CryopathyMod", "onFluidMove", false, typeof(Segment), typeof(int), typeof(int), typeof(int), typeof(ushort), typeof(ushort), typeof(long), typeof(long), typeof(long));
				//int raws = InstructionHandlers.getLastInstructionBefore(codes, loc, OpCodes.Callvirt, "WorldScript", "BuildFromEntity", typeof(Segment), typeof(int), typeof(int), typeof(int), typeof(ushort), typeof(ushort));
				//raws = InstructionHandlers.getLastInstructionBefore(codes, raws, OpCodes.Callvirt, "WorldScript", "BuildFromEntity", typeof(Segment), typeof(int), typeof(int), typeof(int), typeof(ushort), typeof(ushort));
				int raws = InstructionHandlers.getLastInstructionBefore(codes, loc, OpCodes.Ldsfld, "WorldScript", "instance");
				int raw1 = InstructionHandlers.getInstruction(codes, raws, 0, OpCodes.Ldloc_S);
				codes.Insert(loc, new CodeInstruction(OpCodes.Ldloc_S, codes[raw1+2].operand));
				codes.Insert(loc, new CodeInstruction(OpCodes.Ldloc_S, codes[raw1+1].operand));
				codes.Insert(loc, new CodeInstruction(OpCodes.Ldloc_S, codes[raw1].operand));
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
				//FileLog.Log("Codes are "+InstructionHandlers.toString(codes));
			}
			catch (Exception e) {
				FileLog.Log("Caught exception when running patch "+MethodBase.GetCurrentMethod().DeclaringType+"!");
				FileLog.Log(e.Message);
				FileLog.Log(e.StackTrace);
				FileLog.Log(e.ToString());
			}
			return codes.AsEnumerable();
		}
	}
	/*
	[HarmonyPatch(typeof(MobSpawnManager))]
	[HarmonyPatch("SpawnMobOnGroundAt")]
	public static class WormSpawnPatch {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				int loc = InstructionHandlers.getInstruction(codes, 0, 0, OpCodes.Callvirt, "MobManager", "SpawnMob", true, new Type[]{typeof(MobType), typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(Vector3), typeof(Vector3)});
				FileLog.Log("Running patch, which found instruction "+InstructionHandlers.toString(codes, loc));
				codes[loc].opcode = OpCodes.Call;
				codes[loc].operand = InstructionHandlers.convertMethodOperand("ReikaKalseki.Cryopathy.CryopathyMod", "onMobAttemptSpawn", false, typeof(MobManager), typeof(MobType), typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(Vector3), typeof(Vector3));
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
				//FileLog.Log("Codes are "+InstructionHandlers.toString(codes));
			}
			catch (Exception e) {
				FileLog.Log("Caught exception when running patch "+MethodBase.GetCurrentMethod().DeclaringType+"!");
				FileLog.Log(e.Message);
				FileLog.Log(e.StackTrace);
				FileLog.Log(e.ToString());
			}
			return codes.AsEnumerable();
		}
	}*/
}
