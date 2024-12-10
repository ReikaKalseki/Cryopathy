using UnityEngine;  //Needed for most Unity Enginer manipulations: Vectors, GameObjects, Audio, etc.
using System.IO;    //For data read/write methods
using System;    //For data read/write methods
using System.Collections.Generic;   //Working with Lists and Collections
using System.Linq;   //More advanced manipulation of lists/collections
using System.Reflection;
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
    public static ushort magmaTurretBlockID;
		
	private static readonly float[] spawnerPauseTimes = new float[8];
	private static readonly Coordinate[] cryoSpawners = new Coordinate[8];
    
    public CryopathyMod() : base("Cryopathy") {
    	config = new Config<CRConfig.ConfigEntries>(this);
    }
	
	public static Config<CRConfig.ConfigEntries> getConfig() {
		return config;
	}
	
	public static float getDNADropScalar() {
    	return config.getFloat(CRConfig.ConfigEntries.DROP_CHANCE);
	}

    protected override void loadMod(ModRegistrationData registrationData) {           
        config.load();
        
        runHarmony();
        
		registrationData.RegisterEntityHandler(eSegmentEntity.CryoMine);
        
		registrationData.RegisterEntityHandler("ReikaKalseki.CryoMissileTurret");
		TerrainDataEntry terrainDataEntry;
		TerrainDataValueEntry terrainDataValueEntry;
		TerrainData.GetCubeByKey("ReikaKalseki.CryoMissileTurret_Key", out terrainDataEntry, out terrainDataValueEntry);
		missileTurretBlockID = terrainDataEntry.CubeType;
		
		registrationData.RegisterEntityHandler("ReikaKalseki.MagmaTurret");
		TerrainData.GetCubeByKey("ReikaKalseki.MagmaTurret_Key", out terrainDataEntry, out terrainDataValueEntry);
		magmaTurretBlockID = terrainDataEntry.CubeType;
        
        GenericAutoCrafterDataEntry entry = GenericAutoCrafterNew.mMachinesByKey["OrganicReassembler"];
        entry.Recipe.addIngredient("ReikaKalseki.MagmaDNA", 10);
    }
    
	public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters) {
		ModCreateSegmentEntityResults modCreateSegmentEntityResults = new ModCreateSegmentEntityResults();
		try {
			if (parameters.Cube == missileTurretBlockID) {
				parameters.ObjectType = SpawnableObjectEnum.MissileTurret_T1;
				modCreateSegmentEntityResults.Entity = new CryoMissileTurret(parameters);
			}
			else if (parameters.Type == eSegmentEntity.CryoMine) {
				parameters.ObjectType = SpawnableObjectEnum.CryoMine;
				modCreateSegmentEntityResults.Entity = new SmarterCryoMine(parameters);
			}
			else if (parameters.Cube == magmaTurretBlockID) {
				parameters.ObjectType = SpawnableObjectEnum.Creep_Melter;
				modCreateSegmentEntityResults.Entity = new MagmaTurret(parameters);
			}
		}
		catch (Exception e) {
			FUtil.log(e.ToString());
		}
		return modCreateSegmentEntityResults;
	}
	
	private static readonly FieldInfo spawnerID = typeof(ColdCreepSpawner).GetField("mnThisID", BindingFlags.Instance | BindingFlags.NonPublic);
    
    public static void pauseCryospawner(ColdCreepSpawner spawner) {
		int id = (int)spawnerID.GetValue(spawner);
    	spawnerPauseTimes[id] = Time.time;
    }
    
    public static bool isCryospawnerPaused(ColdCreepSpawner spawner) {
    	int id = (int)spawnerID.GetValue(spawner);
    	return isCryospawnerPaused(spawner, id);
    }
    
    public static bool isCryospawnerPaused(ColdCreepSpawner spawner, int id) {
    	setCryospawner(spawner, id);
    	return Time.time-spawnerPauseTimes[id] < config.getFloat(CRConfig.ConfigEntries.STUN_TIME);
    }
		
    public static ColdCreepSpawner getCryospawner(int index, Func<long, long, long, Segment> segmentFetch) {
    	if (CCCCC.mabCornerDestroyed[index] || cryoSpawners[index] == null)
    		return null;
    	Coordinate c = cryoSpawners[index];
    	Segment s = c.getSegment(segmentFetch);
    	if (!s.isSegmentValid())
    		return null;
    	return s.FetchEntity(eSegmentEntity.ColdCreepSpawner, c.xCoord, c.yCoord, c.zCoord) as ColdCreepSpawner;
    }
    
    public static void setCryospawner(int idx, long x, long y, long z) {
    	if (cryoSpawners[idx] == null || !cryoSpawners[idx].equals(x, y, z)) {
    		cryoSpawners[idx] = new Coordinate(x, y, z);
    		FUtil.log("Caching cryospawner #"+idx+" at "+cryoSpawners[idx]+" ["+cryoSpawners[idx].fromRaw()+"]");
    	}
    }
    
    public static void setCryospawner(ColdCreepSpawner cc, int idx) {
    	setCryospawner(idx, cc.mnX, cc.mnY, cc.mnZ); //do not offset down
    }
    
    public static bool shouldAvoidBlock(ushort ID) {
    	return CubeHelper.IsReinforced(ID) || CubeHelper.IsOre(ID);
    }
    
    public static ushort getCubeForCryoCheckAt(Segment s, long x, long y, long z, ushort cryoToBuild) { //their code returns if the returned value from getCube == cryoToBuild
    	ushort real = s.GetCube(x, y, z);
    	return CubeHelper.IsMachine(real) || CubeHelper.HasEntity(real) || (config.getBoolean(CRConfig.ConfigEntries.CRYO_ORE_BLOCK) && CubeHelper.IsOre(real)) || MagmaTurret.isLocationProtected(x, y, z) ? cryoToBuild : real;
    }
    
    private static Coordinate explodeFXQueued = null;
    
    public static void tickPlayer(LocalPlayerScript ep) {
    	if (explodeFXQueued != null) {
    		FUtil.log("Processing queued explode FX @ "+explodeFXQueued.ToString());
		   	Vector3 position = WorldScript.instance.mPlayerFrustrum.GetCoordsToUnity(explodeFXQueued.xCoord, explodeFXQueued.yCoord, explodeFXQueued.zCoord) + WorldHelper.DefaultBlockOffset;
			if (true/*dist < 128*/) {
		   		FUtil.log("Spawning FX @ "+position);
		   		if (SurvivalParticleManager.instance.CryoDust) {
					SurvivalParticleManager.instance.CryoDust.transform.position = position;
					SurvivalParticleManager.instance.CryoDust.Emit(120);
		   		}
		   		else {
		   			FUtil.log("Cryo dust FX was null?");
		   		}
			}
			else {
				FUtil.log("Skipping FX, too far");
			}
			FUtil.log("Playing explode sound");
			if (AudioHUDManager.instance.mSource && AudioHUDManager.instance.mDeleteSFX) {
				AudioHUDManager.instance.mSource.pitch = 0.2F;
				AudioHUDManager.instance.mSource.transform.position = position;
				AudioHUDManager.instance.mSource.PlayOneShot(AudioHUDManager.instance.mDeleteSFX, 3F);
			}
			else if (AudioHUDManager.instance.mSource) {
				FUtil.log("Sound was null?");
			}
			else {
				FUtil.log("Sound source was null?");
			}
	    	explodeFXQueued = null;
    	}
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
		    			FUtil.log("Cryo met magma @ "+x2+", "+y2+", "+z2+" "+Coordinate.fromRawXYZ(x2, y2, z2).ToString()+", exploding R="+size);
    					WorldScript.instance.BuildFromEntity(from, rawX, rawY, rawZ, eCubeTypes.Air, TerrainData.DefaultAirValue);
    					s.SetCubeTypeNoChecking(x, y, z, eCubeTypes.Air, TerrainData.DefaultAirValue);
						clearLava(rawX, rawY, rawZ, size);
    					killWorms(rawX, rawY, rawZ, size*2);
    					
    					Player ep = WorldScript.mLocalPlayer;
						Vector3 vec = new Vector3(x2-ep.mnWorldX, y2-ep.mnWorldY, z2-ep.mnWorldZ);
						float dist = vec.magnitude;
						if (dist <= size) {
							float damage = Mathf.Lerp(100, 0, Mathf.Clamp01((dist-size/2F)*2F/size)); //kill at <50% radius
							if (damage > 0) {
		    					FUtil.log("Player was close at dist "+dist+", dmg = "+damage);
								SurvivalPowerPanel.HurtWithReason(damage, false, "You are just as explodable as worms");
							}
						}
						explodeFXQueued = new Coordinate(x2, y2, z2);
						FUtil.log("Queuing FX @ "+explodeFXQueued.ToString());
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
    	FUtil.log("Killing worms around "+x0+", "+y0+", "+z0);
		int count = MobManager.instance.mActiveMobs.Count;
		int n = 0;
		for (int index = 0; index < count; index++) {
			MobEntity e = MobManager.instance.mActiveMobs[index];
			if (e != null && (e.mType == MobType.WormBoss || e.mType == MobType.WormBossLava) && e.mnHealth > 0) {
				n++;
				Vector3 vec = Vector3.zero;
				vec.x = (float) (e.mnX - x0);
				vec.y = (float) (e.mnY - y0);
				vec.z = (float) (e.mnZ - z0);
				FUtil.log("Worm @ "+new Coordinate(e)+": Dist is "+vec+" = "+vec.magnitude);
				if (vec.magnitude <= size*1.25) {
					e.TakeDamage(Int32.MaxValue); //DIE DIE DIE DIE DIE
					FloatingCombatTextManager.instance.QueueText(e.mnX, e.mnY + 4L, e.mnZ, 1.5f, "Lava Worm Killed!", Color.magenta, 2F, 4096F);
				}
			}
		}
    	FUtil.log("Scanned "+n+" worms of "+count+" mobs");
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
										if (WorldScript.instance.BuildFromEntity(segment, x, y, z, eCubeTypes.Air, TerrainData.DefaultAirValue)) {
											segment.AddedFluid(false);
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
