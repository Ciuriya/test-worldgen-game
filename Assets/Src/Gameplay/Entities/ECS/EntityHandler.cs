using Unity.Burst;
using Unity.Entities;

namespace PendingName.Entities.ECS {
	
	[RequireMatchingQueriesForUpdate]
	public partial struct EntityHandler : ISystem {

		[BurstCompile]
		public void OnCreate(ref SystemState state) {
			// require something for update, a flag that the game is started...?
			// same for other systems
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state) {

		}
	}
}