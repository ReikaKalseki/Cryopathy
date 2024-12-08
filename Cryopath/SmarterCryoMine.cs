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
	
	public class SmarterCryoMine : FCoreMachine {
		
		public static readonly int BLAST_RADIUS = 5; //vanilla is 5
		public static readonly int MELT_RADIUS = 8;
		
		//private int mnDepthScanned;
	
		private bool mbLinkedToGO;
	
		//private Animation mAnimation;
	
		public Vector3 mUp;
	
		private float mrScanDelay;
	
		//private ushort mLastCube;
	
		private float mrRemoveTimer;
	
		private bool mbRequestVisualExplosion;
	
		private bool mbVisualExplosionTriggered;
	
		private bool mbScanned;
	
		private bool mbVisuallyScanned;
	
		private float TriggerDelay;
		
		private int lookDirection;
		
		private int powerBonus;
		
		private bool exploded;
		
		private bool spawnExplodeFX;
	
		public SmarterCryoMine(ModCreateSegmentEntityParameters parameters) : base(parameters) {
			this.mUp = SegmentCustomRenderer.GetRotationQuaternion(mFlags) * Vector3.up;
			this.mUp.Normalize();
		}
	
		public override void DropGameObject() {
			base.DropGameObject();
			this.mbLinkedToGO = false;
		}
	
		public override void UnityUpdate() {
			if (!this.mbLinkedToGO) {
				if (this.mWrapper == null || !this.mWrapper.mbHasGameObject) {
					return;
				}
				if (this.mWrapper.mGameObjectList == null) {
					Debug.LogError("RA missing game object #0?");
				}
				if (this.mWrapper.mGameObjectList[0].gameObject == null) {
					Debug.LogError("RA missing game object #0 (GO)?");
				}
				this.mbLinkedToGO = true;
			}
			GameObject go = this.mWrapper.mGameObjectList[0].gameObject;
			if (this.mbRequestVisualExplosion && !this.mbVisualExplosionTriggered) {
				go.transform.Search("Bomb_Sphere").gameObject.SetActive(false);
				go.transform.Search("ExplodeEffect").gameObject.SetActive(true);
				go.transform.Search("LingerEffect").gameObject.SetActive(true);
				this.mbVisualExplosionTriggered = true;
			}
			if (this.mbScanned && !this.mbVisuallyScanned) {
				go.transform.Search("ActiveLight").gameObject.SetActive(true);
				this.mbVisuallyScanned = true;
			}
			
			if (spawnExplodeFX) {
				Vector3 position = WorldScript.instance.mPlayerFrustrum.GetCoordsToUnity(mnX, mnY, mnZ) + WorldHelper.DefaultBlockOffset;
				SurvivalParticleManager.instance.CryoDust.transform.position = position;
				SurvivalParticleManager.instance.CryoDust.Emit(60);
				if (AudioHUDManager.instance.mSource && AudioHUDManager.instance.mChargeExplosion) {
					AudioHUDManager.instance.mSource.pitch = 0.5F;
					AudioHUDManager.instance.mSource.transform.position = position;
					AudioHUDManager.instance.mSource.PlayOneShot(AudioHUDManager.instance.mChargeExplosion, 3F);
				}
				else if (AudioHUDManager.instance.mSource) {
					FUtil.log("Sound was null?");
				}
				else {
					FUtil.log("Sound source was null?");
				}
				spawnExplodeFX = false;
			}
		}
	
		public override void LowFrequencyUpdate() {
			this.UpdatePlayerDistanceInfo();
			if (this.TriggerDelay > 0f) {
				this.TriggerDelay -= LowFrequencyThread.mrPreviousUpdateTimeStep;
				if (this.TriggerDelay <= 0f) {
					this.StartGas();
				}
			}
			if (this.mrScanDelay > 0f) {
				this.mrScanDelay -= LowFrequencyThread.mrPreviousUpdateTimeStep;
				return;
			}
			
			if (mrRemoveTimer > 0) {
				this.mrRemoveTimer -= LowFrequencyThread.mrPreviousUpdateTimeStep;
				if (this.mrRemoveTimer <= 0f) {
					WorldScript.instance.BuildFromEntity(this.mSegment, this.mnX, this.mnY, this.mnZ, eCubeTypes.Air, TerrainData.DefaultAirValue);
					return;
				}
			}
			
			if (this.mrRemoveTimer <= 0f || !this.mbScanned) {
				if (!this.mbScanned) {
					for (int i = -(MELT_RADIUS + 1); i <= MELT_RADIUS + 1; i++) {
						for (int j = 0; j < MELT_RADIUS + 1; j++) {
							for (int k = -(MELT_RADIUS + 1); k <= MELT_RADIUS + 1; k++) {
								if (AttemptGetSegment(this.mnX + (long)i, this.mnY + (long)j, this.mnZ + (long)k) == null) {
									this.mrScanDelay = 0.5f;
									return;
								}
							}
						}
					}
				}
				
				lookDirection = (lookDirection+1)%6;
				Coordinate vec = Coordinate.ZERO;
				switch(lookDirection) {
					case 0:
						vec = new Coordinate(-1, 0, 0);
						break;
					case 1:
						vec = new Coordinate(1, 0, 0);
						break;
					case 2:
						vec = new Coordinate(0, -1, 0);
						break;
					case 3:
						vec = new Coordinate(0, 1, 0);
						break;
					case 4:
						vec = new Coordinate(0, 0, -1);
						break;
					case 5:
						vec = new Coordinate(0, 0, 1);
						break;
				}
				vec = vec.offset(mnX, mnY, mnZ);
				
				this.mbScanned = true;
				Segment s = vec.getSegment(AttemptGetSegment);
				if (s != null) {
					ushort cube = s.GetCube(vec.xCoord, vec.yCoord, vec.zCoord);
					if (cube == eCubeTypes.ColdCreep || cube == eCubeTypes.ColdCreepFluid) {
						this.StartGas();
					}
				}
				return;
			}
			
			if (exploded)
				return;
			
			int r = MELT_RADIUS+this.powerBonus;			
			int r2 = r*3/2;
			int rx = Mathf.Abs(mUp.x) > 0.2 ? r2 : r;
			int ry = Mathf.Abs(mUp.y) > 0.2 ? r2 : r;
			int rz = Mathf.Abs(mUp.z) > 0.2 ? r2 : r;
			float fx = rx/(float)r;
			float fy = ry/(float)r;
			float fz = rz/(float)r;
			
			FUtil.log("Detonating "+this+" with bonus "+powerBonus+", R="+r+" -> "+rx+"x"+ry+"x"+rz);
			
			int destroy = 0;
			int melt = 0;
			HashSet<Segment> rerender = new HashSet<Segment>();
			for (int l = -rx; l <= rx; l++) {
				for (int m = -ry; m <= ry; m++) {
					for (int n = -rz; n <= rz; n++) {
						if (l == 0 && m == 0 && n == 0)
							continue;
						double dist = MathUtil.py3d(l/fx, m/fy, n/fz);
						if (dist <= r) {
							Coordinate c = new Coordinate(mnX+l+(long)(mUp.x*3), mnY+m+(long)(mUp.y*3), mnZ+n+(long)(mUp.z*3));
							Segment segment = c.getSegment(AttemptGetSegment);
							if (segment != null) {
								ushort id = c.getBlock(segment);
								if (id == eCubeTypes.ColdCreep || id == eCubeTypes.ColdCreepFluid) {
									if (dist <= BLAST_RADIUS) {
										if (WorldScript.instance.BuildFromEntity(segment, c.xCoord, c.yCoord, c.zCoord, eCubeTypes.Air, TerrainData.DefaultAirValue)) {
											CCCCC.CryoKillCount++;
											destroy++;
											if (UnityEngine.Random.Range(0F, 1F) < 0.025*CryopathyMod.getConfig().getFloat(CRConfig.ConfigEntries.DROP_CHANCE)) {
								    			FUtil.dropItem(c.xCoord, c.yCoord, c.zCoord, "ReikaKalseki.CryoExtract");
								    		}
										}
									}
									else if (id == eCubeTypes.ColdCreep) {
										segment.SetCubeTypeNoChecking((int)(c.xCoord % 16L), (int)(c.yCoord % 16L), (int)(c.zCoord % 16L), eCubeTypes.ColdCreepFluid, TerrainData.GetDefaultValue(eCubeTypes.ColdCreepFluid));
										segment.AddedFluid(false);
										melt++;
									}
									rerender.Add(segment);
								}
								else if (id == eCubeTypes.CryoMine) {
									SmarterCryoMine cryoMine = segment.FetchEntity(eSegmentEntity.CryoMine, c.xCoord, c.yCoord, c.zCoord) as SmarterCryoMine;
									if (cryoMine != null) {
										cryoMine.TriggerRipple(powerBonus+1);
									}
								}
							}
							else {
								FUtil.log("Error, active mine hadn't prepaged segments");
							}
						}
					}
				}
			}
			
			FUtil.log("Destroyed "+destroy+"; Melted "+melt+"; rerendering "+rerender.Count+" segments");
			
			foreach (Segment s in rerender)
				s.RequestRegenerateGraphics();
			
			Player ep = WorldScript.mLocalPlayer;
			Vector3 epdist = new Vector3(mnX-ep.mnWorldX, mnY-ep.mnWorldY, mnZ-ep.mnWorldZ);
			float dd = epdist.magnitude;
			if (dd <= MELT_RADIUS) {
				float damage = Mathf.Lerp(0, 30, Mathf.Clamp01(MELT_RADIUS-dd)/MELT_RADIUS);
				if (damage > 0) {
					FUtil.log("Player was close at dist "+dd+", dmg = "+damage);
					SurvivalPowerPanel.HurtWithReason(damage, false, "You are just as explodable as cryoplasm");
				}
			}
			
			spawnExplodeFX = true;
			exploded = true;
		}
	
		private void StartGas() {
			this.mrRemoveTimer = 4f;
			this.mbRequestVisualExplosion = true;
			this.RequestImmediateNetworkUpdate();
		}
	
		public void TriggerRipple(int power) {
			powerBonus = power;
			if (this.TriggerDelay <= 0f) {
				this.TriggerDelay = 0.5F*(1+power*0.25F);
			}
		}
	
		public override bool ShouldNetworkUpdate() {
			return true;
		}
	
		public override void ReadNetworkUpdate(BinaryReader reader) {
			this.mbRequestVisualExplosion = reader.ReadBoolean();
			this.mbScanned = reader.ReadBoolean();
		}
	
		public override void WriteNetworkUpdate(BinaryWriter writer) {
			writer.Write(this.mbRequestVisualExplosion);
			writer.Write(this.mbScanned);
		}
	
		public override void OnDelete() {
			base.OnDelete();
		}
	}
	
}
