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
        Debug.Log("Ran mod register, started harmony");
        try {
			harmony.PatchAll();
        }
        catch (Exception e) {
			FileLog.Log("Caught exception when running patcher!");
			FileLog.Log(e.Message);
			FileLog.Log(e.StackTrace);
			FileLog.Log(e.ToString());
        }
        
        return registrationData;
    }
    
    public static bool shouldAvoidBlock(ushort ID) {
    	return CubeHelper.IsReinforced(ID) || CubeHelper.IsOre(ID);
    }
    
    public static ushort getCubeForCryoCheckAt(Segment s, long x, long y, long z, ushort cryoToBuild) { //their code returns false if the returned value from getCube == cryoToBuild
    	ushort real = s.GetCube(x, y, z);
    	return CubeHelper.IsMachine(real) || CubeHelper.HasEntity(real) ? cryoToBuild : real;
    }
    
    public static void onFluidMove(Segment s, int x, int y, int z, ushort block, ushort meta, long rawX, long rawY, long rawZ) {
    	s.SetCubeTypeNoChecking(x, y, z, block, meta);
    	if (block == eCubeTypes.ColdCreepFluid) {
    		Segment below = WorldScript.instance.GetLocalSegment(rawX, rawY-1, rawZ);
    		if (below != null && below.IsSegmentInAGoodState()) {
	    		int x2 = (int) (rawX % 16L);
	    		int y2 = (int) ((rawY-1) % 16L);
				int z2 = (int) (rawZ % 16L);
	    		ushort at = below.GetCube(x2, y2, z2);
	    		if (at == eCubeTypes.Magma || at == eCubeTypes.MagmaFluid) {
	    			Debug.Log("Cryo fell onto magma!");
					bool flag;
			    	do {
			        	flag = WorldScript.instance.Explode(rawX, rawY, rawZ, 4, 1000000);
			        	if (!flag)
			        		Thread.Sleep(100);
			    	} while (!flag);
					wormSpawnSuccessChance *= WORM_SPAWN_SUCCESS_MULT;
					int killed = 0;
					foreach (MobEntity e in MobManager.instance.mActiveMobs) {
			            if (e != null && e.mType == MobType.WormBossLava && e.mnHealth > 0) {
							double dist = py3d(e.mnX, e.mnY, e.mnZ, rawX, rawY, rawZ);
							if (dist < 400) {
		    					e.TakeDamage(e.mnHealth+1);
		    					killed++;
							}
			            }
		        	}
					Debug.Log("Killed "+killed+" worms.");
	    		}
    		}
    	}
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
