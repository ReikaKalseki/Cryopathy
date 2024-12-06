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
using System.Reflection.Emit;
using Harmony;
using UnityEngine;
  //Needed for most Unity Enginer manipulations: Vectors, GameObjects, Audio, etc.
using ReikaKalseki.FortressCore;

namespace ReikaKalseki.Cryopathy {
	
	public class CryoMissileTurret : FCoreMachine, PowerConsumerInterface {
		
		public static readonly int MIN_RANGE = 48;
		public static readonly int MAX_RANGE = 512;

		public static readonly float REQUIRED_PPS = 512;

		//cryo bombers are 6, and melters are single target
		public static readonly int BLAST_RADIUS = 7; //15x15x15
	
		public CryoMissileTurret(Segment segment, long x, long y, long z, ushort cube, byte flags, ushort lValue, bool loadFromDisk) : base(eSegmentEntity.Mod, SpawnableObjectEnum.MissileTurret_T1, x, y, z, cube, flags, lValue, Vector3.zero, segment) {
			this.mbNeedsLowFrequencyUpdate = true;
			this.mbNeedsUnityUpdate = true;
			this.mnRequestMissileFire = false;
			this.mRand = new System.Random();
			this.maAttachedHoppers = new StorageMachineInterface[6];
		}

		public override void DropGameObject() {
			base.DropGameObject();
			this.mbLinkedToGO = false;
		}

		public override bool ShouldSave() {
			return true;
		}

		public override void Write(BinaryWriter writer) {
			writer.Write(this.maStoredMissileType);
			writer.Write(this.mrCurrentPower);
			writer.Write(this.mrLoadTimer);
			writer.Write(this.fireCooldown);
		}

		public override void Read(BinaryReader reader, int entityVersion) {
			this.maStoredMissileType = reader.ReadInt32();
			this.mrCurrentPower = reader.ReadSingle();
			this.mrLoadTimer = reader.ReadSingle();
			this.fireCooldown = reader.ReadSingle();
		}

		public override void ReadNetworkUpdate(BinaryReader reader) {
			this.Read(reader, this.GetVersion());
			bool flag = reader.ReadBoolean();
			if (flag) {
				visualTarget = Coordinate.read(reader);
				this.mnClientPreviousMissileID = reader.ReadInt32();
				this.maStoredMissileType = this.mnClientPreviousMissileID;
				this.mrClientShotDelayTimer = 1f;
				Vector3 zero = Vector3.zero;
				zero.x = (float)(visualTarget.xCoord - this.mnX);
				zero.y = (float)(visualTarget.yCoord - this.mnY - 1L);
				zero.z = (float)(visualTarget.zCoord - this.mnZ);
				this.mVectorToTarget = zero;
			}
			this.CountLoadedMissiles();
		}

		public override void WriteNetworkUpdate(BinaryWriter writer) {
			this.Write(writer);
			if (this.visualTarget != null) {
				writer.Write(true);
				visualTarget.write(writer);
				writer.Write(this.mnClientPreviousMissileID);
				visualTarget = null;
				this.mnClientPreviousMissileID = 0;
			}
			else {
				writer.Write(false);
			}
		}

		public override void UnitySuspended() {
			this.Turret = null;
			this.melterGFX = null;
			this.spawnerGFX = null;
		}

		private void LinkToGO() {
			if (this.mWrapper.mGameObjectList == null) {
				return;
			}
			if (this.mWrapper.mGameObjectList.Count == 0) {
				return;
			}
			if (this.mWrapper.mGameObjectList[0].gameObject == null) {
				FUtil.log("Missile "+this+" missing game object #0 (GO)?");
			}
			GameObject go = this.mWrapper.mGameObjectList[0].gameObject;
			this.Turret = go.transform.Search("Missile Launcher Swivel").gameObject;
			melterGFX = go.transform.Search("Freeze Missile").gameObject;
			spawnerGFX = go.transform.Search("Poison Missile").gameObject;
			melterGFX.SetActive(false);
			spawnerGFX.SetActive(false);
			this.mAnim = this.mWrapper.mGameObjectList[0].gameObject.GetComponent<Animation>();
			this.mbLinkedToGO = true;
			this.mnRequestMissileFire = false;
		}

		private void FlipActiveMissiles() {
			GameObject fx = getFX();
			if (this.mbNoMissilesLoaded) {
				this.mAnim.CrossFade("MoveToUnLoaded");
				if (fx)
					fx.SetActive(false);
			}
			else {
				this.mAnim.CrossFade("MoveToLoaded");
				if (fx)
					fx.SetActive(true);
			}
		}
		
		private bool isForcedOffline() {
			return CentralPowerHub.Destroyed || !CCCCC.ActiveAndWorking || !anyCryoExists();
		}
		
		private bool anyCryoExists() {
			for (int i = 0; i < 8; i++) {
				if (!CCCCC.mabCornerDestroyed[i])
					return true;
			}
			return false;
		}

		private void DoVisualAiming() {
			if (this.mrCurrentPower < REQUIRED_PPS) {
				this.mrLowPowerTime += Time.deltaTime;
			}
			else {
				this.mrLowPowerTime *= 0.5f;
			}
			bool flag = mrLowPowerTime > 1 || isForcedOffline();
			if (!flag && visualTarget == null && this.currentTarget == null) {
				if (this.mbNoMissilesLoaded) {
					flag = true;
				}
			}
			if (flag) {
				this.RestTime += Time.deltaTime;
				if (this.RestTime < 10f) {
					this.Turret.transform.forward += (Vector3.forward - this.Turret.transform.forward) * Time.deltaTime;
				}
			}
			else {
				this.RestTime = 0f;
				bool flag2 = currentTarget != null || mrClientShotDelayTimer > 0f;
				if (!flag2) {
					this.mrSpinTimer -= Time.deltaTime;
					if (this.mrSpinTimer > 0f) {
						this.Turret.transform.Rotate(0f, this.mrSpinRate * Time.deltaTime, 0f);
					}
					if (this.mrSpinTimer < -3f) {
						this.mrSpinRate = (float)UnityEngine.Random.Range(-48, 48);
						this.mrSpinTimer = (float)UnityEngine.Random.Range(1, 10);
					}
				}
				else {
					Vector3 a = this.mVectorToTarget;
					a.y = 0f;
					this.Turret.transform.forward += (a - this.Turret.transform.forward) * Time.deltaTime / 60f;
				}
			}
		}
		
		private GameObject getFX() {
			if (maStoredMissileType == MELTER_ITEM_ID)
				return melterGFX;
			else if (maStoredMissileType == SPAWNER_ITEM_ID)
				return spawnerGFX;
			return null;
		}

		private void DoClientFire() {
			if (visualTarget != null) {
				this.mrClientShotDelayTimer -= Time.deltaTime;
				if (this.mrClientShotDelayTimer < 0f) {
					GameObject fx = getFX();
					if (!fx) {
						FUtil.log("Error @ "+this+", can't set missile graphics as our loaded missile ID was " + (maStoredMissileType <= 0 ? "null" : ItemEntry.mEntriesById[maStoredMissileType].Key));
						return;
					}
					MissileEffectManager.instance.FireMissile(fx.transform.position, visualTarget.xCoord, visualTarget.yCoord, visualTarget.zCoord, fx.transform.forward, 1000000, MissileEffectManager.eVisualType.eImbued);
					fx.SetActive(false);
					visualTarget = null;
					this.mbNoMissilesLoaded = true;
					maStoredMissileType = -1;
					this.CountLoadedMissiles();
				}
			}
		}

		private void DoServerFire() {
			if (this.mnRequestMissileFire) {
				if (currentTarget != null) {
					GameObject fx = getFX();
					if (!fx) {
						FUtil.log(this+" cannot spawn missile FX, as the FX is null [loaded missile = "+(maStoredMissileType <= 0 ? "null" : ItemEntry.mEntriesById[maStoredMissileType].Key)+"]!");
					}
					else {
						MissileEffectManager.instance.FireMissile(fx.transform.position, currentTarget.xCoord, currentTarget.yCoord, currentTarget.zCoord, fx.transform.forward, 1000000, MissileEffectManager.eVisualType.eImbued);
						fx.SetActive(false);
					}
					maStoredMissileType = -1;
					this.mbNoMissilesLoaded = true;
				}
				this.mnRequestMissileFire = false;
			}
		}

		public override void UnityUpdate() {
			this.mnUnityUpdates++;
			if (this.mbLinkedToGO) {
				this.FlipActiveMissiles();
				this.DoVisualAiming();
				if (WorldScript.mbIsServer) {
					this.DoServerFire();
				}
				else {
					this.DoClientFire();
				}
				return;
			}
			if (this.mWrapper == null) {
				return;
			}
			this.LinkToGO();
		}

		private void UpdateAttachedHoppers(bool lbInput) {
			int num = 0;
			this.mnNumInvalidAttachedHoppers = 0;
			for (int i = 0; i < 6; i++) {
				long num2 = this.mnX;
				long num3 = this.mnY;
				long num4 = this.mnZ;
				if (i == 0) {
					num2 -= 1L;
				}
				if (i == 1) {
					num2 += 1L;
				}
				if (i == 2) {
					num3 -= 1L;
				}
				if (i == 3) {
					num3 += 1L;
				}
				if (i == 4) {
					num4 -= 1L;
				}
				if (i == 5) {
					num4 += 1L;
				}
				Segment segment = base.AttemptGetSegment(num2, num3, num4);
				if (segment != null) {
					StorageMachineInterface storageMachineInterface = segment.SearchEntity(num2, num3, num4) as StorageMachineInterface;
					if (storageMachineInterface != null) {
						this.mnNumInvalidAttachedHoppers++;
						eHopperPermissions permissions = storageMachineInterface.GetPermissions();
						if (permissions != eHopperPermissions.Locked) {
							if (lbInput || permissions != eHopperPermissions.AddOnly) {
								if (!lbInput || permissions != eHopperPermissions.RemoveOnly) {
									if (!lbInput || !storageMachineInterface.IsFull()) {
										if (lbInput || !storageMachineInterface.IsEmpty()) {
											this.maAttachedHoppers[num] = storageMachineInterface;
											this.mnNumInvalidAttachedHoppers--;
											num++;
										}
									}
								}
							}
						}
					}
				}
			}
			this.mnNumValidAttachedHoppers = num;
		}

		public override void OnDelete() {
			if (maStoredMissileType > 0) {
				ItemBase item = ItemManager.SpawnItem(maStoredMissileType);
				ItemManager.instance.DropItem(item, this.mnX, this.mnY, this.mnZ, Vector3.zero);
				maStoredMissileType = -1;
			}
			base.OnDelete();
		}

		private int RemoveMissileFromHopper(int id) {
			for (int i = 0; i < this.mnNumValidAttachedHoppers; i++) {
				if (this.maAttachedHoppers[i].TryExtractItems(this, id, 1))
					return id;
			}
			return -1;
		}
		
		private ushort getSeekBlock() {
			if (maStoredMissileType == MELTER_ITEM_ID)
				return eCubeTypes.ColdCreep;
			else if (maStoredMissileType == SPAWNER_ITEM_ID)
				return eCubeTypes.ColdCreepSpawner;
			else
				return 0;
		}

		private void LoadMissile() {
			this.mrLoadTimer -= LowFrequencyThread.mrPreviousUpdateTimeStep;
			if (this.mrLoadTimer < 0f) {
				this.UpdateAttachedHoppers(false);
				if (this.mnNumValidAttachedHoppers == 0) {
					return;
				}
				int old = maStoredMissileType;
				maStoredMissileType = this.RemoveMissileFromHopper(MELTER_ITEM_ID);
				if (maStoredMissileType <= 0)
					maStoredMissileType = this.RemoveMissileFromHopper(SPAWNER_ITEM_ID);
				if (this.maStoredMissileType != old) {
					this.CountLoadedMissiles();
					this.RequestImmediateNetworkUpdate();
				}
			}
		}

		private void CountLoadedMissiles() {
			mbNoMissilesLoaded = maStoredMissileType <= 0;
		}

		private void ApplyDelayedImpact() {
			if (this.currentTarget == null) {
				this.impactDelay = 0;
				FUtil.log("Target nulled before ["+isStunMissile+"] impact?!");
				isStunMissile = false;
				return;
			}
			//FUtil.log("Impact ["+isStunMissile+"] @ "+currentTarget.fromRaw());
			this.impactDelay = 0f;
			
			//ushort id = getSeekBlock();
			//if (id <= 0)
			//	return;

			int n = 0;
			for (int i = -BLAST_RADIUS; i <= BLAST_RADIUS; i++) {
				for (int j = -BLAST_RADIUS; j <= BLAST_RADIUS; j++) {
					for (int k = -BLAST_RADIUS; k <= BLAST_RADIUS; k++) {
						Vector3 vector = new Vector3((float)i, (float)j, (float)k);
						if (vector.sqrMagnitude <= (float)(BLAST_RADIUS * BLAST_RADIUS)) {
							long dx = this.currentTarget.xCoord + (long)i;
							long dy = this.currentTarget.yCoord + (long)j;
							long dz = this.currentTarget.zCoord + (long)k;
							Segment segment = base.AttemptGetSegment(dx, dy, dz);
							if (segment != null) {
								ushort cube = segment.GetCube(dx, dy, dz);
								//FUtil.log("AoE @ "+i+","+j+","+k+" hits "+FUtil.blockToString(new Coordinate(dx, dy, dz), this));
								//if (id) {
								//	WorldScript.instance.BuildFromEntity(segment, dx, dy, dz, place);
								//	CCCCC.CryoKillCount += 1U;
								//} do not count as cryo kill since it does not destroy it
								if (cube == eCubeTypes.ColdCreep && !isStunMissile) {
									ushort meta = global::TerrainData.GetDefaultValue(eCubeTypes.ColdCreepFluid);
									WorldScript.instance.BuildFromEntity(segment, dx, dy, dz, eCubeTypes.ColdCreepFluid, meta);
									segment.SetCubeTypeNoChecking((int)(dx % 16L), (int)(dy % 16L), (int)(dz % 16L), 683, meta);
									segment.AddedFluid(false);
									n++;
								}
								else if (isStunMissile && cube == eCubeTypes.ColdCreepSpawner) {
									ColdCreepSpawner spawner = segment.FetchEntity(eSegmentEntity.ColdCreepSpawner, dx, dy, dz) as ColdCreepSpawner;
									if (spawner != null) {
										CryopathyMod.pauseCryospawner(spawner);
										FUtil.log("Pausing cryospawner # "+spawner.mnID+" @ "+new Coordinate(spawner));
									}
									else {
										FUtil.log("Error: Missile impacted a spawner lacking an entity!");
									}
									n++;
								}
							}
							else {
								FUtil.log("Get segment failed @ "+new Coordinate(dx, dy, dz)+" = "+Coordinate.fromRawXYZ(dx, dy, dz));
							}
							//else if (this.TargetPaged) {
							//	Debug.LogError("Nasty error - cryo turret thinks it prepaged segments, but get segment failed?");
							//}
						}
					}
				}
			}
			//FUtil.log(n+" blocks affected.");
			if (n > 0) {
				if (isStunMissile)
					FloatingCombatTextManager.instance.QueueText(currentTarget.xCoord, currentTarget.yCoord, currentTarget.zCoord, 2f, "Impact!", Color.yellow, 5f, 512f);
				else
					FloatingCombatTextManager.instance.QueueText(currentTarget.xCoord, currentTarget.yCoord, currentTarget.zCoord, 2f, "Impact melted "+n+" cryoplasm!", Color.yellow, 5f, 512f);
			}
			else {
				FloatingCombatTextManager.instance.QueueText(currentTarget.xCoord, currentTarget.yCoord, currentTarget.zCoord, 2f, "Found no target!", Color.red, 5f, 512f);
			}
			isStunMissile = false;
			currentTarget = null;
		}
	
		private bool isValidTarget(ushort id, Segment s, int i, int j, int k) { //maybe add an AoE check, eg no more than 6 cryo in a 5x5x5 centered on
			if (id == eCubeTypes.ColdCreepSpawner) { //needs to be exposed
				for (int a = -1; a <= 1; a++) {
					for (int b = -1; b <= 1; b++) {
						for (int c = -1; c <= 1; c++) {
							int di = i+a;
							int dj = j+b;
							int dk = k+c;
							Segment s2 = s;
							if (WorldUtil.adjustCoordinateInPossiblyAdjacentSegment(ref s, ref di, ref dj, ref dk, AttemptGetSegment)) {
								ushort adj = s2.GetCubeNoChecking(di, dj, dk);
								if (adj == eCubeTypes.ColdCreep || adj == eCubeTypes.ColdCreepFluid) //do not allow targeting of a spawner with cryoplasm next to it
									return false;
							}
						}
					}
				}
			}
			return true;
		}
	
		private Coordinate findTarget(ushort id, long x, long y, long z, out Segment s) {
			s = AttemptGetSegment(x, y, z);
			if (!s.isSegmentValid())
				return null;
			for (int j = 15; j >= 0; j--) {
				for (int i = 0; i < 16; i++) {
					for (int k = 0; k < 16; k++) {
						if (s.GetCubeNoChecking(i, j, k) == id && isValidTarget(id, s, i, j, k)) {
							return new Coordinate(s.baseX + (long)i, s.baseY + (long)j, s.baseZ + (long)k);
						}
					}
				}
			}
			return null;
		}
		
		private Coordinate ScanForTarget(ushort id) {			
			Coordinate offset = searchQueue.getPosition()*16;
			
			while (offset.asVector3().magnitude < MIN_RANGE) {
				searchQueue.step(); //still step forwards
				offset = searchQueue.getPosition()*16;
			}
			
			//FUtil.log(this+" scanning "+offset+" for "+id+" ("+TerrainData.mEntries[id].Name+")");
			
			long x = this.mnX + offset.xCoord;
			long y = this.mnY + offset.yCoord;
			long z = this.mnZ + offset.zCoord;
			Segment s;
			Coordinate found = findTarget(id, x, y, z, out s);
			//if (s == null)
			//	FUtil.log(this+" got null segment @ "+offset+" (xyz= "+(x-WorldUtil.COORD_OFFSET)+", "+(y-WorldUtil.COORD_OFFSET)+", "+(z-WorldUtil.COORD_OFFSET)+") F="+mFrustrum+", "+mFrustrum.GetSegment(x, y, z)+"/"+WorldScript.instance.GetSegment(x, y, z));
			//else
			//	FUtil.log(this+" checking segment "+(s.baseX-WorldUtil.COORD_OFFSET)+", "+(s.baseY-WorldUtil.COORD_OFFSET)+", "+(s.baseZ-WorldUtil.COORD_OFFSET)+" @ "+offset+" (found '"+target+"')");
			
			if (s == null) {
				segmentFailCount++;
				if (segmentFailCount < 10)
					return found;
			}
			else {
				segmentFailCount = 0;
			}
			
			searchQueue.step();
			if (offset.xCoord > MAX_RANGE || offset.zCoord > MAX_RANGE)
				searchQueue.reset();
			
			return found;
		}
		
		private void fire(Coordinate c) {
			string name = ItemEntry.mEntriesById[maStoredMissileType].Name;
			Coordinate c2 = c.fromRaw();
			//FUtil.log(this+" firing "+name+" upon "+FUtil.blockToString(c, this)+" @ "+c2);
			FloatingCombatTextManager.instance.QueueText(this.mnX, mnY, this.mnZ, 1f, "Firing "+name+" to "+c2+"!", Color.yellow, 3f, 128f);
			currentTarget = c;
			isStunMissile = maStoredMissileType == SPAWNER_ITEM_ID;
			Vector3 vec = Vector3.zero;
			vec.x = (float)(currentTarget.xCoord - this.mnX);
			vec.y = (float)(currentTarget.yCoord - this.mnY);
			vec.z = (float)(currentTarget.zCoord - this.mnZ);
			this.mVectorToTarget = vec;
			impactDelay = 1f + vec.magnitude / 32f;
			this.fireCooldown = 5f + (float)this.mRand.NextDouble();
			
			//FUtil.log("vec="+vec+" & delay="+impactDelay.ToString("0.000"));
			
			visualTarget = currentTarget;
			this.mnClientPreviousMissileID = this.maStoredMissileType;
			this.RequestImmediateNetworkUpdate();
			this.mnRequestMissileFire = true;
			//this.maStoredMissileType = -1;
			CentralPowerHub.mnMissilesFired++;
			
			this.CountLoadedMissiles();
			
			if (this.mbNoMissilesLoaded)
				this.mrLoadTimer = 5f;
			else
				this.fireCooldown = 1f;
		}

		public override void LowFrequencyUpdate() {
			if (isForcedOffline()) {
				this.currentTarget = null;
				this.mnRequestMissileFire = false;
				this.CountLoadedMissiles();
				return;
			}
			if (impactDelay > 0) {
				impactDelay -= LowFrequencyThread.mrPreviousUpdateTimeStep;
				if (impactDelay <= 0)
					this.ApplyDelayedImpact();
				return;
			}
			if (this.mrCurrentPower < REQUIRED_PPS) {
				this.mrLowPowerTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
			}
			else {
				this.mrLowPowerTime = 0f;
			}
			
			if (this.mrLoadTimer > 0f) {
				this.mrLoadTimer -= LowFrequencyThread.mrPreviousUpdateTimeStep;
				return;
			}
			this.CountLoadedMissiles();
			if (maStoredMissileType <= 0) {
				this.LoadMissile();
				return;
			}
			if (this.mbNoMissilesLoaded) {
				this.currentTarget = null;
				return;
			}
			if (fireCooldown > 0)
				fireCooldown -= LowFrequencyThread.mrPreviousUpdateTimeStep;
			if (!WorldScript.mbIsServer) {
				if (visualTarget != null && !mbLinkedToGO)
					visualTarget = null;
				return;
			}
			if (currentTarget == null && mrCurrentPower >= REQUIRED_PPS && maStoredMissileType > 0) {
				this.mrCurrentPower -= REQUIRED_PPS;
				if (fireCooldown <= 0) {
					ushort seek = getSeekBlock();
					if (seek == 0)
						return;
					
					Coordinate c = ScanForTarget(seek);
					if (c != null)
						fire(c);
				}
			}
		}

		public float GetRemainingPowerCapacity() {
			return this.mrMaxPower - this.mrCurrentPower;
		}

		public float GetMaximumDeliveryRate() {
			return this.mrMaxTransferRate;
		}

		public float GetMaxPower() {
			return this.mrMaxPower;
		}

		public bool DeliverPower(float amount) {
			if (amount > this.GetRemainingPowerCapacity()) {
				return false;
			}
			this.mrCurrentPower += amount;
			this.MarkDirtyDelayed();
			return true;
		}

		public bool WantsPowerFromEntity(SegmentEntity entity) {
			return true;
		}
		
		public override string GetPopupText() {
			string ret = base.GetPopupText();
			if (isForcedOffline()) {
				ret += "\nC5 Offline! Defences offline!";
				return ret;
			}
			ret += "\nPower: "+mrCurrentPower+"/"+mrMaxPower+" (needs "+REQUIRED_PPS+" PPS)";
			if (WorldUtil.getBiome(this) != WorldUtil.Biomes.COLDCAVES) {
				ret += "\nMust be placed in the cold caverns!";
				return ret;
			}
			if (maStoredMissileType > 0)
				ret += "\nHolding "+ItemEntry.mEntriesById[maStoredMissileType].Name;
			else
				ret += "\nNo ammo!";
			if (maStoredMissileType > 0 && mbNoMissilesLoaded)
				ret += "\nERROR: Has no missiles while holding a missile?!";
			ret += "\nScanning position: "+(searchQueue.getPosition()*16).offset(mnX-WorldUtil.COORD_OFFSET, mnY-WorldUtil.COORD_OFFSET, mnZ-WorldUtil.COORD_OFFSET).ToString();
			if (impactDelay > 0) {
				ret += "\nWaiting for impact";
			}
			else if (fireCooldown > 0) {
				ret += "\nNext shot in "+fireCooldown.ToString("0.0")+"s";
			}
			else if (mrLoadTimer > 0) {
				ret += "\nWakes in "+fireCooldown.ToString("0.0")+"s";
			}
			return ret;
		}

		private bool mbLinkedToGO;

		private System.Random mRand;

		public int maStoredMissileType;

		private Animation mAnim;

		private int mnClientPreviousMissileID;

		private float RestTime;

		private GameObject Turret;

		private GameObject melterGFX;
		private GameObject spawnerGFX;

		private int mnUnityUpdates;

		private float mrSpinRate;

		private float mrSpinTimer;
		
		private float fireCooldown;

		private Vector3 mVectorToTarget;

		private float mrLowPowerTime;

		private bool mnRequestMissileFire;

		private StorageMachineInterface[] maAttachedHoppers;

		public int mnNumValidAttachedHoppers;

		public int mnNumInvalidAttachedHoppers;

		public float mrLoadTimer;

		public bool mbNoMissilesLoaded;

		public float mrClientShotDelayTimer;

		public Coordinate visualTarget;

		public int mnTargetHealthLeft = -1;

		public float mrCurrentPower;

		public float mrMaxPower = 5000;

		public float mrMaxTransferRate = 5000;
		
		private Coordinate currentTarget;
		
		private bool isStunMissile;

		private float impactDelay;
	
		private int segmentFailCount;
		
		private readonly SpiralSearchQueue searchQueue = new SpiralSearchQueue(MAX_RANGE/16, -2, 3);
		
		public readonly int MELTER_ITEM_ID = ItemEntry.mEntriesByKey["ReikaKalseki.CryoMelterMissile"].ItemID;
		public readonly int SPAWNER_ITEM_ID = ItemEntry.mEntriesByKey["ReikaKalseki.CryoSpawnerMissile"].ItemID;
	}
	
}
