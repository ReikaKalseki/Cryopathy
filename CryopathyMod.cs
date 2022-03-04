using UnityEngine;  //Needed for most Unity Enginer manipulations: Vectors, GameObjects, Audio, etc.
using System.IO;    //For data read/write methods
using System;    //For data read/write methods
using System.Collections.Generic;   //Working with Lists and Collections
using System.Linq;   //More advanced manipulation of lists/collections
using System.Threading;
using Harmony;
using ReikaKalseki;

namespace ReikaKalseki.Cryopathy
{
  public class CryopathyMod : FortressCraftMod
  {
    public const string MOD_KEY = "ReikaKalseki.Cryopathy";
    public const string CUBE_KEY = "ReikaKalseki.Cryopathy_Key";
    
    //private const float WORM_SPAWN_SUCCESS_MULT = 0.996F;//0.985F;//0.998F;
    //private static float wormSpawnSuccessChance = 1F;

    public override ModRegistrationData Register()
    {
        ModRegistrationData registrationData = new ModRegistrationData();
        //registrationData.RegisterEntityHandler(MOD_KEY);
        /*
        TerrainDataEntry entry;
        TerrainDataValueEntry valueEntry;
        TerrainData.GetCubeByKey(CUBE_KEY, out entry, out valueEntry);
        if (entry != null)
          ModCubeType = entry.CubeType;
         */        
        var harmony = HarmonyInstance.Create("ReikaKalseki.Cryopathy");
        HarmonyInstance.DEBUG = true;
        FileLog.Log("Ran mod register, started harmony (harmony log)");
        Util.log("Ran mod register, started harmony");
        try {
			harmony.PatchAll();
        }
        catch (Exception e) {
			FileLog.Log("Caught exception when running patcher!");
			FileLog.Log(e.Message);
			FileLog.Log(e.StackTrace);
			FileLog.Log(e.ToString());
        }
        
        GenericAutoCrafterDataEntry entry = GenericAutoCrafterNew.mMachinesByKey["OrganicReassembler"];
        Util.addIngredient(entry.Recipe, "ReikaKalseki.MagmaDNA", 10);
        
        return registrationData;
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
    					int size = 24;
    					WorldScript.instance.BuildFromEntity(from, rawX, rawY, rawZ, eCubeTypes.Air, TerrainData.DefaultAirValue);
    					s.SetCubeTypeNoChecking(x, y, z, eCubeTypes.Air, TerrainData.DefaultAirValue);
						clearLava(rawX, rawY, rawZ, size/2);
    					killWorms(rawX, rawY, rawZ, size);
    					//new FALCORBomber().;
    					//SurvivalParticleManager.instance.
    					//GameObject go = GameObject.Find ("CryoPlasm Impact Particles");
    					//ParticleSystem part = go.GetComponent<ParticleSystem>();
    					//part.transform.position = WorldScript.instance.mPlayerFrustrum.GetCoordsToUnity(rawX, rawY, rawZ) + WorldHelper.DefaultBlockOffset;
    					//part.Emit(40);
		    		}
    			}
    			catch (Exception e) {
    				string err = "Cryo flow patch exception @ "+x+"/"+y+"/"+z+" = "+rawX+"/"+rawY+"/"+rawZ+": "+e.ToString();
    				Util.log(err);
    				ARTHERPetSurvival.instance.SetARTHERReadoutText(err, 40, false, true);
    			}
    		}
    	}
    }
    
    private static void killWorms(long x0, long y0, long z0, int size) {
		int count = MobManager.instance.mActiveMobs.Count;
		for (int index = 0; index < count; index++) {
			MobEntity e = MobManager.instance.mActiveMobs[index];
			if (e != null && e.mType == MobType.WormBoss && e.mnHealth > 0) { //TODO: hurt players too
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
							if (segment != null && segment.mbInitialGenerationComplete && !segment.mbDestroyed) {
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
											if (magma) {
												DroppedItemData stack = ItemManager.DropNewCubeStack(eCubeTypes.MagmaFluid, 0, 1, x, y, z, Vector3.zero);
												if (UnityEngine.Random.Range(0, 40) == 0) {
													Util.dropItem(x, y, z, "ReikaKalseki.MagmaDNA");
												}
											}
											else if (cryo) {
												if (UnityEngine.Random.Range(0, 20) == 0) {
													Util.dropItem(x, y, z, "ReikaKalseki.CryoExtract");
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
    		if (UnityEngine.Random.Range(0, 1.0F) < chance*0.5) {
    			Util.dropItem(x, y, z, "ReikaKalseki.CryoExtract");
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
    
    private static double py3d(long rawX, long rawY, long rawZ, long rawX2, long rawY2, long rawZ2) {
    	long dx = rawX2-rawX;
    	long dy = rawY2-rawY;
    	long dz = rawZ2-rawZ;
    	return Math.Sqrt(dx*dx+dy*dy+dz*dz);
    }

  }
}
