/*
 * Created by SharpDevelop.
 * User: Reika
 * Date: 04/11/2019
 * Time: 11:28 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.IO;
//For data read/write methods
using System.Collections;
//Working with Lists and Collections
using System.Collections.Generic;
//Working with Lists and Collections
using System.Linq;
//More advanced manipulation of lists/collections
using System.Reflection;
using System.Threading;
using System.Reflection.Emit;
using Harmony;
using UnityEngine;
//Needed for most Unity Enginer manipulations: Vectors, GameObjects, Audio, etc.
using ReikaKalseki.FortressCore;

namespace ReikaKalseki.Cryopathy {
	
	public class MagmaTurret : FCoreMachine {
		
		private static readonly FieldInfo storedMagma = typeof(T4_MagmaStorage).GetField("StoredGas", BindingFlags.NonPublic | BindingFlags.Instance);
		
		public static readonly int PIPE_RANGE = 64; //so 45x45x45
		
		public static readonly int RANGE = 22; //so 45x23x45 (no search below)
		public static readonly int CLEAR_AREA_SIZE = 1; //clear 3x3x3 at a time
		public static readonly int SEARCH_STEP_SIZE = CLEAR_AREA_SIZE+1; //step by 2s since clears a 3x3x3 so no need to check every adjacent, cuts block count by 8
		public static readonly int MAX_NEGATIVE = 1; //looks max 1 block below its horizontal
		public static readonly int PER_TICK = 10; //with a volume of 46575 (/8 = 5822 coords), 50/s, means 2 min per cycle for active clearing
		public static readonly int HALO_LIFETIME = 10; //2s

		//public static readonly float PPS = 2000F;		
		//public static readonly float MAX_POWER = 100000F;
		
		private static readonly List<MagmaTurret> cachedTurrets = new List<MagmaTurret>();

		//public float mrCurrentPower;

		//public float mrNormalisedPower;

		private bool mbLinkedToGO;

		private GameObject meltCubeHaloOriginal;

		private GameObject laserRender;

		private GameObject mTurretObject;

		private GameObject mGunObject;

		private GameObject mBarrelObject;

		public float mrTimeSinceShoot;

		public int mnBlocksAblated;

		private System.Random mRand;

		private float mrLinkedTime;

		private int mnTotalBlocksScanned;
		
		private float sourceSearchCooldown;
		
		private T4_MagmaStorage magmaSource;
		
		private int hasMagma;
		
		private Vector3 mUp;
		
		private readonly List<Coordinate> blocksToCheck = new List<Coordinate>();
		
		private readonly Mutex cacheMutex = new Mutex();
		
		private readonly List<CryoClearRecord> clearedCache = new List<CryoClearRecord>();
		
		class CryoClearRecord {
			
			private static readonly float FADE_START = MagmaTurret.HALO_LIFETIME*0.8F;
			
			internal readonly MagmaTurret owner;
			internal readonly Coordinate location;
			internal readonly ushort cryoType;
				
			internal readonly Vector3 unityPosition;
			
			private GameObject meltCubeHalo;
			
			private float age;
			
			internal CryoClearRecord(MagmaTurret mt, Coordinate c, ushort id) {
				owner = mt;
				location = c;
				cryoType = id;
				
				unityPosition = WorldScript.instance.mPlayerFrustrum.GetCoordsToUnity(c.xCoord, c.yCoord, c.zCoord) + new Vector3(0.5f, 0.5f, 0.5f);
			}
			
			internal bool unityTick() {
				if (!meltCubeHalo) {
					meltCubeHalo = UnityEngine.Object.Instantiate(owner.meltCubeHaloOriginal);
					meltCubeHalo.transform.SetParent(owner.meltCubeHaloOriginal.transform.parent);
					meltCubeHalo.transform.position = unityPosition;
				}
				float f = UnityEngine.Random.Range(3.5F, 5F);
				if (age >= FADE_START) {
					f *= Mathf.Lerp(1, 0, (age-FADE_START)/(HALO_LIFETIME-FADE_START));
				}
				meltCubeHalo.transform.localScale = Vector3.one*f;
				age += Time.deltaTime;
				if (age >= HALO_LIFETIME)
					UnityEngine.Object.Destroy(meltCubeHalo);
				return age >= HALO_LIFETIME;
			}
			
		}
		
		public static bool isLocationProtected(long x, long y, long z) {
			bool cleanup = false;
			foreach (MagmaTurret mt in cachedTurrets) {
				if (mt == null || mt.mbDelete) {
					cleanup = true;
					continue;
				}
				if (mt.tryProtect(x, y, z))
					return true;
			}
			if (cleanup)
				cachedTurrets.RemoveAll(mt => mt == null || mt.mbDelete);
			return false;
		}

		public MagmaTurret(ModCreateSegmentEntityParameters parameters) : base(parameters) { 
			this.mRand = new System.Random();
			mObjectType = SpawnableObjectEnum.Creep_Melter;
			this.mUp = SegmentCustomRenderer.GetRotationQuaternion(mFlags) * Vector3.up;
			this.mUp.Normalize();
			setupScanAoE();
			cachedTurrets.Add(this);
		}
		
		private void setupScanAoE() {
			blocksToCheck.Clear();
			int minX = mUp.x > 0.2F ? -MAX_NEGATIVE : -RANGE;
			int minY = mUp.y > 0.2F ? -MAX_NEGATIVE : -RANGE;
			int minZ = mUp.z > 0.2F ? -MAX_NEGATIVE : -RANGE;
			int maxX = mUp.x < -0.2F ? MAX_NEGATIVE : RANGE;
			int maxY = mUp.y < -0.2F ? MAX_NEGATIVE : RANGE;
			int maxZ = mUp.z < -0.2F ? MAX_NEGATIVE : RANGE;
			
			for (int i = minX; i <= maxX; i += SEARCH_STEP_SIZE) {
				for (int j = minY; j <= maxY; j += SEARCH_STEP_SIZE) {
					for (int k = minZ; k <= maxZ; k += SEARCH_STEP_SIZE) {
						Vector3 vector = new Vector3((float)i, (float)j, (float)k);
						if (vector.sqrMagnitude <= (float)(RANGE * RANGE)) {
							blocksToCheck.Add(new Coordinate(mnX + i, mnY + j, mnZ + k));
						}
					}
				}
			}
			Coordinate c0 = new Coordinate(mnX, mnY, mnZ);
			blocksToCheck.Sort((c1, c2) => c1.getTaxicabDistance(c0).CompareTo(c2.getTaxicabDistance(c0))); //put close coordinates first
		}

		public override int GetVersion() {
			return 1;
		}

		public override void Write(BinaryWriter writer) {
			//writer.Write(this.mrCurrentPower);
			writer.Write(this.mnBlocksAblated);
			writer.Write(this.hasMagma);
		}

		public override void Read(BinaryReader reader, int entityVersion) {
			//this.mrCurrentPower = reader.ReadSingle();
			this.mnBlocksAblated = reader.ReadInt32();
			this.hasMagma = reader.ReadInt32();
		}

		public override bool ShouldNetworkUpdate() {
			return true;
		}

		public override void WriteNetworkUpdate(BinaryWriter writer) {
			base.WriteNetworkUpdate(writer);
		}

		public override void ReadNetworkUpdate(BinaryReader reader) {
			base.ReadNetworkUpdate(reader);
		}

		public override void UnitySuspended() {
			this.meltCubeHaloOriginal = null;
			this.mTurretObject = null;
			this.mGunObject = null;
			this.mBarrelObject = null;
			this.laserRender = null;
		}

		public override void DropGameObject() {
			base.DropGameObject();
			this.mbLinkedToGO = false;
		}

		public override void UnityUpdate() {
			if (!this.mbLinkedToGO) {
				if (this.mWrapper == null) {
					return;
				}
				if (this.mWrapper.mGameObjectList == null) {
					return;
				}
				if (this.mWrapper.mGameObjectList[0].gameObject == null) {
					Debug.LogError("AutoExcavator missing game object #0 (GO)?");
				}
				this.meltCubeHaloOriginal = this.mWrapper.mGameObjectList[0].gameObject.transform.Search("Current Cube").gameObject;
				meltCubeHaloOriginal.SetActive(false);
				
				this.mTurretObject = this.mWrapper.mGameObjectList[0].gameObject.transform.Search("Excavator Turret Holder").gameObject;
				this.mGunObject = this.mWrapper.mGameObjectList[0].gameObject.transform.Search("Excavator Gun Holder").gameObject;
				this.mBarrelObject = this.mWrapper.mGameObjectList[0].gameObject.transform.Search("Excavator Gun").gameObject;
				this.laserRender = this.mWrapper.mGameObjectList[0].gameObject.transform.Search("Laser").gameObject;
				this.laserRender.SetActive(false);
				this.mbLinkedToGO = true;
				this.mrLinkedTime = 0f;
				return;
			}
			else {
				if (this.mDistanceToPlayer < 128f) {
					cacheMutex.WaitOne();
					for (int i = clearedCache.Count-1; i >= 0; i--) {
						CryoClearRecord cr = clearedCache[i];
						if (cr.unityTick()) {
							clearedCache.RemoveAt(i);
						}
					}
					cacheMutex.ReleaseMutex();
				}
				if (clearedCache.Count == 0) {
					if (this.mDistanceToPlayer < 128f) {
						this.laserRender.SetActive(false);
						this.mTurretObject.transform.forward += (Vector3.forward - this.mTurretObject.transform.forward) * Time.deltaTime * 0.21f;
						this.mGunObject.transform.forward += (Vector3.up - this.mGunObject.transform.forward) * Time.deltaTime * 0.21f;
					}
					mrTimeSinceShoot += Time.deltaTime;
					return;
				}
				mrTimeSinceShoot = 0;
				float d = 5F;
				if (this.mrLinkedTime < 1f) {
					this.mrLinkedTime += Time.deltaTime;
					d = 30f;
				}
				
				Vector3 aim = clearedCache[0].unityPosition;				
				if (this.mDistanceToPlayer < 128f) {
					Vector3 vector2 = aim - this.mTurretObject.transform.position;
					vector2.y = 0f;
					vector2.Normalize();
					this.mTurretObject.transform.forward += (vector2 - this.mTurretObject.transform.forward) * Time.deltaTime * 2f * d;
					vector2 = aim - this.mGunObject.transform.position;
					Vector3 a = this.mTurretObject.transform.forward;
					a = vector2;
					a.Normalize();
					this.mGunObject.transform.forward += (a - this.mGunObject.transform.forward) * Time.deltaTime * 2.75f * d;
					if (this.mrTimeSinceShoot <= 0.1F) {
						this.laserRender.SetActive(true);
						if (SurvivalParticleManager.instance == null || SurvivalParticleManager.instance.GreenShootParticles == null) {
							if (WorldScript.meGameMode == eGameMode.eCreative) {
								return;
							}
							Debug.LogError("Error, the AE's particles have gone null?!");
						}
						else {
							SurvivalParticleManager.instance.GreenShootParticles.transform.position = this.mBarrelObject.transform.position + this.mBarrelObject.transform.forward * 0.5f;
							SurvivalParticleManager.instance.GreenShootParticles.transform.forward = this.mBarrelObject.transform.forward;
							SurvivalParticleManager.instance.GreenShootParticles.Emit(25);
						}
						SurvivalDigScript.instance.DoCryoImpact(aim, 15, -vector2);
					}
				}
				float num2 = this.mrTimeSinceShoot * 25f;
				if (num2 < 2f) {
					float magnitude = (aim - this.mBarrelObject.transform.position).magnitude;
					this.laserRender.transform.localScale = new Vector3(magnitude, 2f - num2, 2f - num2);
				}
				else
				if (num2 >= 2f) {
					this.laserRender.SetActive(false);
				}
				return;
			}
		}

		private bool checkAndClearCryoAt(Coordinate c, out ushort id) {
			Segment segment = c.getSegment(AttemptGetSegment);
			if (segment.isSegmentValid()) {
				id = c.getBlock(segment);
				if ((id == eCubeTypes.ColdCreep || id == eCubeTypes.ColdCreepFluid) && LoadAdjacentSegmentsIfRequired(segment, c.xCoord, c.yCoord, c.zCoord)) {
					for (int i = -CLEAR_AREA_SIZE; i <= CLEAR_AREA_SIZE; i++) { 
						for (int j = -CLEAR_AREA_SIZE; j <= CLEAR_AREA_SIZE; j++) {
							for (int k = -CLEAR_AREA_SIZE; k <= CLEAR_AREA_SIZE; k++) {
								long dx = c.xCoord+i;
								long dy = c.yCoord+j;
								long dz = c.zCoord+k;
								Segment s2 = i == 0 && j == 0 && k == 0 ? segment : AttemptGetSegment(dx, dy, dz);
								if (s2.isSegmentValid()) {
									WorldScript.instance.BuildFromEntity(s2, dx, dy, dz, eCubeTypes.Air, TerrainData.DefaultAirValue);
									s2.SetCubeTypeNoChecking((int)(dx % 16L), (int)(dy % 16L), (int)(dz % 16L), eCubeTypes.Air, TerrainData.DefaultAirValue);
								}
							}
						}
					}
					return true;
				}//base.DropExtraSegments(this.mSegment); ?
			}
			id = 0;
			return false;
		}

		private bool tryProtect(long x, long y, long z) {
			if (hasMagma <= 0/* || mrCurrentPower < PPS*/)
				return false;
			Vector3 dist = new Vector3(x-mnX, y-mnY, z-mnZ);
			if (dist.sqrMagnitude <= RANGE*RANGE && Vector3.Dot(dist.normalized, mUp) > -0.1F) { //dot so it is not far below us
				hasMagma--;
				return true;
			}
			return false;
		}
		
		private void tryFindMagmaSource() {
			Vector3 mDown = mUp*-1;
			long px = mnX+(int)mDown.x;
			long py = mnY+(int)mDown.y;
			long pz = mnZ+(int)mDown.z;
			ushort id;
			int dist;
			SegmentEntity find;
			EntityManager.ePipeResult res = EntityManager.instance.WhatsAtTheOtherEndOfThisPipe(px, py, pz, FUtil.getPipeOrientation(mDown), PIPE_RANGE, this, out id, out find, out dist);
			magmaSource = find as T4_MagmaStorage;
			if (magmaSource != null && magmaSource.mLinkedCenter != null)
				magmaSource = magmaSource.mLinkedCenter;
			sourceSearchCooldown = 10;
		}
		
		private void tryExtractMagma() {
			if (magmaSource == null)
				return;
			ItemBase ib = (ItemBase)storedMagma.GetValue(magmaSource);
			if (ib == null || ib.GetAmount() <= 0)
				return;
			int amt = Math.Min(50, ib.GetAmount()); //max 50 per tick (250/s)
			hasMagma += amt;
			ib.SetAmount(ib.GetAmount()-amt);
		}

		public override void LowFrequencyUpdate() {
			this.UpdatePlayerDistanceInfo();
			
			if (!WorldScript.mbIsServer) {
				return;
			}
			
			if (FUtil.isFFDefenceOffline()) {
				return;
			}
			
			float dT = LowFrequencyThread.mrPreviousUpdateTimeStep;
			
			if (magmaSource == null) {
				if (sourceSearchCooldown > 0)
					sourceSearchCooldown -= dT;
				if (sourceSearchCooldown <= 0)
					tryFindMagmaSource();
			}
			
			if (hasMagma < 100)
				tryExtractMagma();
			
			if (hasMagma <= 0)
				return;
			
			//if (mrCurrentPower < PPS*dT)
			//	return;			
			//mrCurrentPower -= PPS*dT;
			
			for (int i = 0; i < PER_TICK && blocksToCheck.Count > 0 && hasMagma > 0; i++) {
				Coordinate c = blocksToCheck[0];
				mnTotalBlocksScanned++;
				ushort id;
				if (checkAndClearCryoAt(c, out id)) {
					cacheMutex.WaitOne();
					clearedCache.Add(new CryoClearRecord(this, c, id)); //this has potential threading issues
					cacheMutex.ReleaseMutex();
					mnBlocksAblated++;
					CCCCC.CryoKillCount++;
					hasMagma--;
				}
				blocksToCheck.RemoveAt(0);
			}
			
			if (blocksToCheck.Count == 0)
				setupScanAoE();
		}
/*
		public float GetRemainingPowerCapacity() {
			return MAX_POWER - this.mrCurrentPower;
		}

		public float GetMaximumDeliveryRate() {
			return float.MaxValue;
		}

		public float GetMaxPower() {
			return MAX_POWER;
		}

		public bool WantsPowerFromEntity(SegmentEntity entity) {
			return true;
		}

		public bool DeliverPower(float amount) {
			if (amount > this.GetRemainingPowerCapacity()) {
				return false;
			}
			this.mrCurrentPower += amount;
			this.MarkDirtyDelayed();
			return true;
		}
*/
		public override string GetPopupText() {
			string ret = base.GetPopupText();
			
			if (FUtil.isFFDefenceOffline()) {
				ret += "\n"+PersistentSettings.GetString("C5_Offline_defences_offline");
				return ret;
			}
			//ret += "\nPower: "+mrCurrentPower+"/"+MAX_POWER+" (needs "+PPS+" PPS)";
			if (magmaSource == null)
				ret += "\nNo magma storage found within "+PIPE_RANGE+"m! Searching again in "+sourceSearchCooldown.ToString("0.0")+"s";
			else if (hasMagma <= 0)
				ret += "\nNo magma available!";
			else
				ret += "\nStoring "+hasMagma+"m3 of magma.";
			
			string many = "Cleared "+mnBlocksAblated+" m3 of cryoplasm";
			if (clearedCache.Count > 0)
				many += " ("+clearedCache.Count+" in the last "+HALO_LIFETIME+"s)";
			ret += "\n"+many;
			
			return ret;
		}
		
		protected override bool setupHolobaseVisuals(Holobase hb, out GameObject model, out Vector3 size, out Color color) {
			bool flag = base.setupHolobaseVisuals(hb, out model, out size, out color);
			if (flag)
				color = Color.cyan;
			return flag;
		}
		
	}
	
}
