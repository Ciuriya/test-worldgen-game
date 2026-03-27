using Unity.Entities;

namespace PendingName.Entities.ECS {
    public struct EntityStatData : IBufferElementData {
		public double Value;
	}

    public struct EntityStatModData : IBufferElementData {
		public double Value;
	}

    // if disabled, the entity is currently inactive and should not be processed
    public struct EntityStateData : IComponentData, IEnableableComponent {
		public EntityState State;
	}
}