using UnityEngine;
using Unity.Entities;
using MyBox;
using System.Collections.Generic;
using System;

namespace PendingName.Entities.ECS {

	[DisallowMultipleComponent]
	public class EntityAuthoring : MonoBehaviour {

		[Tooltip("Is this entity living? Can it be afflicted by debuffs?")]
		public bool IsAlive;

		[Tooltip("Does this entity have stats?")]
		public bool HasStats;

		[ConditionalField(nameof(HasStats))]
		[Tooltip("The reference to the scriptable object containing the entity's stats")]
		public EntityStats StatReference;
	}

	public class EntityAuthoringBaker : Baker<EntityAuthoring> {
		public override void Bake(EntityAuthoring authoring) {
			var entity = GetEntity(TransformUsageFlags.Dynamic);

			if (authoring.HasStats && authoring.StatReference != null) {
				AddBuffer<EntityStatData>(entity);
				AddBuffer<EntityStatModData>(entity);

				List<EntityStatWrapper> stats = authoring.StatReference.Stats;

				if (stats.Count > 0) {
					double[] statVals = new double[(int) EntityStat.Count];

					foreach (EntityStatWrapper statWrapper in stats)
						statVals[(int) statWrapper.Stat] = statWrapper.Value;

					foreach (double statVal in statVals) {
						AppendToBuffer(entity, new EntityStatData() { Value = statVal });
						AppendToBuffer(entity, new EntityStatModData() { Value = 0 });
					}
				}
			}

			if (authoring.IsAlive)
				AddComponent(entity, new EntityStateData() { State = EntityState.Alive });

			//AddBuffer<EntityCollisionEvent>(entity);
			//AddComponent(entity, new EntityCollisionBufferTag());
		}
	}
}