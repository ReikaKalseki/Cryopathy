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
using ReikaKalseki.FortressCore;

namespace ReikaKalseki.Cryopathy {
	
	[HarmonyPatch(typeof(ColdCreepSpawner))]
	[HarmonyPatch("UpdateCreep")]
	public static class CreepSeekPatch {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				int loc = InstructionHandlers.getInstruction(codes, 0, 0, OpCodes.Call, typeof(CubeHelper), "IsReinforced", false, new Type[]{typeof(ushort)});
				FileLog.Log("Found instruction "+InstructionHandlers.toString(codes, loc));
				codes[loc].operand = InstructionHandlers.convertMethodOperand(typeof(CryopathyMod), "shouldAvoidBlock", false, typeof(ushort));
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
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				int loc = InstructionHandlers.getInstruction(codes, 0, 0, OpCodes.Callvirt, typeof(Segment), "GetCube", true, new Type[]{typeof(long), typeof(long), typeof(long)});
				FileLog.Log("Found instruction "+InstructionHandlers.toString(codes, loc));
				codes[loc].opcode = OpCodes.Call;
				codes[loc].operand = InstructionHandlers.convertMethodOperand(typeof(CryopathyMod), "getCubeForCryoCheckAt", false, typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(ushort));
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
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				int loc7 = InstructionHandlers.getInstruction(codes, 0, 0, OpCodes.Ldloc_S, 7);
				int loc8 = InstructionHandlers.getInstruction(codes, 0, 0, OpCodes.Ldloc_S, 8);
				int loc9 = InstructionHandlers.getInstruction(codes, 0, 0, OpCodes.Ldloc_S, 9);
				List<CodeInstruction> li = new List<CodeInstruction>();
				li.Add(new CodeInstruction(OpCodes.Ldarg_0));
				li.Add(new CodeInstruction(OpCodes.Ldloc_S, codes[loc7].operand));
				li.Add(new CodeInstruction(OpCodes.Ldloc_S, codes[loc8].operand));
				li.Add(new CodeInstruction(OpCodes.Ldloc_S, codes[loc9].operand));
				int loc = InstructionHandlers.getLastInstructionBefore(codes, codes.Count, OpCodes.Callvirt, typeof(Segment), "SetCubeTypeNoChecking", true, new Type[]{typeof(int), typeof(int), typeof(int), typeof(ushort), typeof(ushort)});
				codes[loc].opcode = OpCodes.Call;
				codes[loc].operand = InstructionHandlers.convertMethodOperand(typeof(CryopathyMod), "onFluidMove", false, typeof(Segment), typeof(int), typeof(int), typeof(int), typeof(ushort), typeof(ushort), typeof(Segment), typeof(long), typeof(long), typeof(long));
				codes.InsertRange(loc, li);
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
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
	
	[HarmonyPatch(typeof(FALCORBomber))]
	[HarmonyPatch("DoBombClear")]
	public static class CryoDropPatch_Bomber {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				int loc = InstructionHandlers.getLastInstructionBefore(codes, codes.Count, OpCodes.Callvirt, typeof(WorldScript), "BuildFromEntity", true, new Type[]{typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(ushort)});
				codes[loc].opcode = OpCodes.Call;
				codes[loc].operand = InstructionHandlers.convertMethodOperand(typeof(CryopathyMod), "deleteCryo", false, typeof(WorldScript), typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(ushort), typeof(float));
				codes.Insert(loc, new CodeInstruction(OpCodes.Ldc_R4, 0.1F));
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
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
	
	[HarmonyPatch(typeof(T4_CreepBurner))]
	[HarmonyPatch("UpdateDischarge")]
	public static class CryoDropPatch_Dazzler {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				int loc = InstructionHandlers.getLastInstructionBefore(codes, codes.Count, OpCodes.Callvirt, typeof(WorldScript), "BuildFromEntity", true, new Type[]{typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(ushort)});
				codes[loc].opcode = OpCodes.Call;
				codes[loc].operand = InstructionHandlers.convertMethodOperand(typeof(CryopathyMod), "deleteCryo", false, typeof(WorldScript), typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(ushort), typeof(float));
				codes.Insert(loc, new CodeInstruction(OpCodes.Ldc_R4, 0.0005F));
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
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
	
	[HarmonyPatch(typeof(CryoMine))]
	[HarmonyPatch("LowFrequencyUpdate")]
	public static class CryoDropPatch_Cryomine {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				int loc = InstructionHandlers.getLastInstructionBefore(codes, codes.Count, OpCodes.Callvirt, typeof(WorldScript), "BuildFromEntity", true, new Type[]{typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(ushort)});
				codes[loc].opcode = OpCodes.Call;
				codes[loc].operand = InstructionHandlers.convertMethodOperand(typeof(CryopathyMod), "deleteCryo", false, typeof(WorldScript), typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(ushort), typeof(float));
				codes.Insert(loc, new CodeInstruction(OpCodes.Ldc_R4, 0.05F));
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
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
	
	[HarmonyPatch(typeof(CreepLancer))]
	[HarmonyPatch("AttemptSetToAir")]
	public static class CryoDropPatch_Lancer {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				int loc = InstructionHandlers.getLastInstructionBefore(codes, codes.Count, OpCodes.Callvirt, typeof(WorldScript), "BuildFromEntity", true, new Type[]{typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(ushort), typeof(ushort)});
				codes[loc].opcode = OpCodes.Call;
				codes[loc].operand = InstructionHandlers.convertMethodOperand(typeof(CryopathyMod), "deleteCryo", false, typeof(WorldScript), typeof(Segment), typeof(long), typeof(long), typeof(long), typeof(ushort), typeof(ushort), typeof(float));
				codes.Insert(loc, new CodeInstruction(OpCodes.Ldc_R4, 0.02F));
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
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
	
	[HarmonyPatch(typeof(LocalPlayerScript))]
	[HarmonyPatch("FixedUpdate")]
	public static class PlayerTickHook {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				InstructionHandlers.patchInitialHook(codes, new List<CodeInstruction>{new CodeInstruction(OpCodes.Ldarg_0), InstructionHandlers.createMethodCall(typeof(CryopathyMod), "tickPlayer", false, new Type[]{typeof(LocalPlayerScript)})});
				FileLog.Log("Done patch "+MethodBase.GetCurrentMethod().DeclaringType);
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
	[HarmonyPatch("LowFrequencyUpdate")]
	public static class CreepLFUPause {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				Label lb = gen.DefineLabel();
				CodeInstruction[] add = new CodeInstruction[]{
					new CodeInstruction(OpCodes.Ldarg_0),
					InstructionHandlers.createMethodCall(typeof(CryopathyMod), "isCryospawnerPaused", false, new Type[]{typeof(ColdCreepSpawner)}),
					new CodeInstruction(OpCodes.Brfalse, lb),
				};
				codes[0].labels.Add(lb);
				InstructionHandlers.patchInitialHook(codes, add);
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
	[HarmonyPatch("LowFrequencyUpdate")]
	public static class CreepAggressionControl {
		
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			try {
				FileLog.Log("Running patch "+MethodBase.GetCurrentMethod().DeclaringType);
				
				
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
