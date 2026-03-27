using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PendingName.Entities.Player {
	
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	public partial class PlayerInputGatherer : SystemBase {
		private InputAction _moveAction;

        protected override void OnCreate() {
            EntityManager.CreateSingleton<PlayerInputSingleton>("Player Input Singleton");
			
			_moveAction = InputSystem.actions.FindAction("Move");
        }
		
		protected override void OnUpdate() {
			ref PlayerInputSingleton inputObj = ref SystemAPI.GetSingletonRW<PlayerInputSingleton>().ValueRW;
			inputObj.Input.Move = _moveAction.ReadValue<Vector2>().ConvertToUnmanaged();

			// we want to use a singleton tag (require for update...)
			// then make this ping unity's input system
			// wrap all of this up in a neat little struct
			// set it as a singleton variable
		}

		protected override void OnDestroy() {
			
		}
	}
}