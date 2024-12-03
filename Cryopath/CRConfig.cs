using System;

using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Xml;
using ReikaKalseki.FortressCore;

namespace ReikaKalseki.Cryopathy
{
	public class CRConfig
	{		
		public enum ConfigEntries {
			[ConfigEntry("Stun Missile Effect Duration (seconds)", typeof(int), 15, 5, 60, 0)]STUN_TIME,
			[ConfigEntry("Cryo DNA Drop Chance Multiplier", typeof(float), 1F, 0.2F, 10F, 0)]DROP_CHANCE,
			[ConfigEntry("Magma Drop Chance Multiplier", typeof(float), 1F, 0.2F, 10F, 0)]MAGMA_DROP_CHANCE,
			[ConfigEntry("Cryoplasm-Lava Blast Radius", typeof(int), 16, 6, 32, 0)]CRYO_LAVA_AOE,
		}
	}
}
