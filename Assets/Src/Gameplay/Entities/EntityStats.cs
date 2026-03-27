using System;
using System.Collections.Generic;
using UnityEngine;

namespace PendingName.Entities {

	[CreateAssetMenu(menuName = "Entities/Stats")]
	public class EntityStats : ScriptableObject {

		[Tooltip("List of the entity's stats ; if a stat is omitted, it defaults to 0.")]
		public List<EntityStatWrapper> Stats;
	}

	[Serializable]
	public enum EntityStat {
		None = -1,
		Health,
		Armor,
		Count,
	}

	[Serializable]
	public struct EntityStatWrapper {
		public EntityStat Stat;
		public double Value;
	}
}