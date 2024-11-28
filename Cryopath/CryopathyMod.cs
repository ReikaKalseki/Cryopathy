﻿using UnityEngine;  //Needed for most Unity Enginer manipulations: Vectors, GameObjects, Audio, etc.
using System.IO;    //For data read/write methods
using System;    //For data read/write methods
using System.Collections.Generic;   //Working with Lists and Collections
using System.Linq;   //More advanced manipulation of lists/collections
using System.Threading;
using Harmony;
using ReikaKalseki;
using ReikaKalseki.FortressCore;

namespace ReikaKalseki.Cryopathy
{
  public class CryopathyMod : FCoreMod
  {
    public const string MOD_KEY = "ReikaKalseki.Cryopathy";
    
    private static Config<CRConfig.ConfigEntries> config;

    public static ushort missileTurretBlockID;
    
    public CryopathyMod() : base("Cryopathy") {
    	config = new Config<CRConfig.ConfigEntries>(this);
    }
	
	public static Config<CRConfig.ConfigEntries> getConfig() {
		return config;
	}
	
	public static float getDNADropScalar() {
    	return config.getFloat(CRConfig.ConfigEntries.DROP_CHANCE);
	}

    public override ModRegistrationData Register()
    {
        ModRegistrationData registrationData = new ModRegistrationData();  
        
        config.load();
        
        runHarmony();
        
		registrationData.RegisterEntityHandler("ReikaKalseki.CryoMissileTurret");
		TerrainDataEntry terrainDataEntry;
		TerrainDataValueEntry terrainDataValueEntry;
		TerrainData.GetCubeByKey("ReikaKalseki.CryoMissileTurret_Key", out terrainDataEntry, out terrainDataValueEntry);
		missileTurretBlockID = terrainDataEntry.CubeType;
        
        GenericAutoCrafterDataEntry entry = GenericAutoCrafterNew.mMachinesByKey["OrganicReassembler"];
        RecipeUtil.addIngredient(entry.Recipe, "ReikaKalseki.MagmaDNA", 10);
        
        return registrationData;
    }
    
	public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters) {
		ModCreateSegmentEntityResults modCreateSegmentEntityResults = new ModCreateSegmentEntityResults();
		try {
			if (parameters.Cube == missileTurretBlockID)
				modCreateSegmentEntityResults.Entity = new CryoMissileTurret(parameters.Segment, parameters.X, parameters.Y, parameters.Z, parameters.Cube, parameters.Flags, parameters.Value, parameters.LoadFromDisk);
		}
		catch (Exception e) {
			FUtil.log(e.ToString());
		}
		return modCreateSegmentEntityResults;
	}
    
    private static Dictionary<int, float> spawnerPauseTimes = new Dictionary<int, float>();
    
    public static void pauseCryospawner(ColdCreepSpawner spawner) {
    	spawnerPauseTimes[spawner.mnID] = Time.time;
    }
    
    public static bool isCryospawnerPaused(ColdCreepSpawner spawner) {
    	if (!spawnerPauseTimes.ContainsKey(spawner.mnID))
    		return false;
    	return Time.time-spawnerPauseTimes[spawner.mnID] < config.getFloat(CRConfig.ConfigEntries.STUN_TIME);
    }
    
    public static bool shouldAvoidBlock(ushort ID) {
    	return CubeHelper.IsReinforced(ID) || CubeHelper.IsOre(ID);
    }
    
    public static ushort getCubeForCryoCheckAt(Segment s, long x, long y, long z, ushort cryoToBuild) { //their code returns false if the returned value from getCube == cryoToBuild
    	ushort real = s.GetCube(x, y, z);
    	return CubeHelper.IsMachine(real) || CubeHelper.HasEntity(real) ? cryoToBuild : real;
    }
    
    public static void onFluidMove(Segment s, int x, int y, int z, ushort block, ushort meta, Segment from, long rawX, long rawY, long rawZ) { //xyz are %16 within segment, raw are world coords
    	s.SetCubeTypeNoChecking(x, y, z, block, meta);
    	if (block == eCubeTypes.ColdCreepFluid) {
    		long belowY = rawY-1;
    		Segment below = WorldScript.instance.GetLocalSegment(rawX, belowY, rawZ);
    		if (below == null)
    			below = WorldScript.instance.GetSegment(rawX, belowY, rawZ);
    		if (below != null && below.IsSegmentInAGoodState()) {
    			try {
    				long x2 = rawX;//(int) (rawX % 16L);
    				long y2 = belowY;//(int) (belowY % 16L);
    				long z2 = rawZ;//(int) (rawZ % 16L);
		    		ushort at = below.GetCube(x2, y2, z2);
		    		if (at == eCubeTypes.Magma || at == eCubeTypes.MagmaFluid) {
		    			int size = config.getInt(CRConfig.ConfigEntries.CRYO_LAVA_AOE);
    					WorldScript.instance.BuildFromEntity(from, rawX, rawY, rawZ, eCubeTypes.Air, TerrainData.DefaultAirValue);
    					s.SetCubeTypeNoChecking(x, y, z, eCubeTypes.Air, TerrainData.DefaultAirValue);
						clearLava(rawX, rawY, rawZ, size);
    					killWorms(rawX, rawY, rawZ, size*2);
    					
    					Player ep = WorldScript.mLocalPlayer;
						Vector3 vec = new Vector3(x2-ep.mnWorldX, y2-ep.mnWorldY, z2-ep.mnWorldZ);
						float dist = vec.magnitude;
						if (dist <= size) {
							float damage = Mathf.Lerp(100, 0, Mathf.Clamp01((dist-size/2F)*2F/size)); //kill at <50% radius
							if (damage > 0)
								SurvivalPowerPanel.HurtWithReason(damage, false, "You are just as explodable as worms");
						}
						
			    		Vector3 position = WorldScript.instance.mPlayerFrustrum.GetCoordsToUnity(x2, y2, z2) + WorldHelper.DefaultBlockOffset;
						SurvivalParticleManager.instance.CryoDust.transform.position = position;
						SurvivalParticleManager.instance.CryoDust.Emit(120);
						AudioHUDManager.instance.mSource.pitch = 0.5F;
						AudioHUDManager.instance.mSource.transform.position = position;
						AudioHUDManager.instance.mSource.PlayOneShot(AudioHUDManager.instance.mBFL_Fire, 2f);
		    		}
    			}
    			catch (Exception e) {
    				string err = "Cryo flow patch exception @ "+x+"/"+y+"/"+z+" = "+rawX+"/"+rawY+"/"+rawZ+": "+e.ToString();
    				FUtil.log(err);
    				ARTHERPetSurvival.instance.SetARTHERReadoutText(err, 40, false, true);
    			}
    		}
    	}
    }
    
    private static void killWorms(long x0, long y0, long z0, int size) {
		int count = MobManager.instance.mActiveMobs.Count;
		for (int index = 0; index < count; index++) {
			MobEntity e = MobManager.instance.mActiveMobs[index];
			if (e != null && e.mType == MobType.WormBoss && e.mnHealth > 0) {
				Vector3 vec = Vector3.zero;
				vec.x = (float) (e.mnX - x0-WorldScript.mDefaultOffset);
				vec.y = (float) (e.mnY - y0-WorldScript.mDefaultOffset);
				vec.z = (float) (e.mnZ - z0-WorldScript.mDefaultOffset);
				if (vec.magnitude <= size*1.25) {
					e.TakeDamage(Int32.MaxValue); //DIE DIE DIE DIE DIE
					FloatingCombatTextManager.instance.QueueText(e.mnX, e.mnY + 4L, e.mnZ, 1.5f, "Lava Worm Killed!", Color.magenta, 2F, 4096F);
				}
			}
		}
    }
    
    private static void clearLava(long x0, long y0, long z0, int size) {
		int maxrSq = size + 1;
		maxrSq *= maxrSq;
		HashSet<Segment> hashSet = new HashSet<Segment>();
		try {
			for (int i = -size; i <= size; i++) {
				for (int j = -size; j <= size; j++) {
					for (int k = -size; k <= size; k++) {
						Vector3 vector = new Vector3((float)j, (float)i, (float)k);
						int num4 = (int)vector.sqrMagnitude;
						if (num4 < maxrSq) {
							long x = x0 + (long)j;
							long y = y0 + (long)i;
							long z = z0 + (long)k;
							Segment segment = WorldScript.instance.GetSegment(x, y, z);
							if (segment.isSegmentValid()) {
								if (!segment.mbIsEmpty) {
									if (!hashSet.Contains(segment)) {
										hashSet.Add(segment);
										segment.BeginProcessing();
									}
									ushort cube = segment.GetCube(x, y, z);
									bool magma = cube == eCubeTypes.Magma || cube == eCubeTypes.MagmaFluid;
									bool cryo = cube == eCubeTypes.ColdCreep || cube == eCubeTypes.ColdCreepFluid;
									if (magma || cryo) {
										if (WorldScript.instance.BuildFromEntity(segment, x, y, z, eCubeTypes.Air, global::TerrainData.DefaultAirValue)) {
											float rand = UnityEngine.Random.Range(0F, 1F)/config.getFloat(CRConfig.ConfigEntries.MAGMA_DROP_CHANCE);
											if (magma) {
												DroppedItemData stack = ItemManager.DropNewCubeStack(eCubeTypes.MagmaFluid, 0, 1, x, y, z, Vector3.zero);
												if (rand < 1/40F)
													FUtil.dropItem(x, y, z, "ReikaKalseki.MagmaDNA");
											}
											else if (cryo && rand < 1/20F) {
												FUtil.dropItem(x, y, z, "ReikaKalseki.CryoExtract");
											}
										}
									}
								}
							}
						}
					}
				}
			}
		}
		finally {
			foreach (Segment current in hashSet) {
				if (current.mbHasFluid) {
					current.FluidSleepTicks = 1;
				}
				current.EndProcessing();
			}
			WorldScript.instance.mNodeWorkerThread.KickNodeWorkerThread();
		}
    }
    
    public static bool deleteCryo(WorldScript world, Segment seg, long x, long y, long z, ushort cube, float chance) {
    	return deleteCryo(world, seg, x, y, z, cube, TerrainData.GetDefaultValue(cube), chance);
    }
    
    public static bool deleteCryo(WorldScript world, Segment seg, long x, long y, long z, ushort cube, ushort meta, float chance) {
    	bool flag = world.BuildFromEntity(seg, x, y, z, cube);
    	if (flag) {
    		if (UnityEngine.Random.Range(0F, 1F) < chance*0.5*config.getFloat(CRConfig.ConfigEntries.DROP_CHANCE)) {
    			FUtil.dropItem(x, y, z, "ReikaKalseki.CryoExtract");
    		}
    	}
    	return flag;
    }
    /*
    public static MobEntity onMobAttemptSpawn(MobManager inst, MobType type, Segment segment, long x, long y, long z, Vector3 blockOffset, Vector3 look) {
    	if (type == MobType.WormBossLava) {
    		Debug.Log("Intercepted lava worm spawn; has a "+wormSpawnSuccessChance*100+"% chance of success");
    		if (UnityEngine.Random.Range(0F, 1F) > wormSpawnSuccessChance) {
    			Debug.Log("Spawn cancelled.");
    			return null;
    		}
    	}
    	return inst.SpawnMob(type, segment, x, y, z, blockOffset, look);
    }*/

  }
}
