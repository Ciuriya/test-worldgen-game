using System;
using Unity.Entities;
using Unity.Mathematics;

namespace PendingName.Entities.Player {
	public struct PlayerInputSingleton : IComponentData {
		public PlayerInputContainer Input;
	}

	[Serializable]
	public struct PlayerInputContainer {
		public float2 Move;
	}
}