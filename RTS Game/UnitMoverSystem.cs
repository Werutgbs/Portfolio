using Authoring;
using Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Systems
{
    /// <summary>
    /// Moves units toward their target positions using physics-based velocity control.
    /// Supports parallel execution for high unit counts.
    /// </summary>
    internal partial struct UnitMoverSystem : ISystem
    {
        /// <summary>
        /// Squared distance threshold for reaching target (avoids sqrt calculation)
        /// </summary>
        public const float ReachedTargetPositionDistanceSq = 0.2f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var unitMoverJob = new UnitMoverJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            };

            unitMoverJob.ScheduleParallel();
        }
    }
}

[BurstCompile]
public partial struct UnitMoverJob : IJobEntity
{
    public float DeltaTime;

    private void Execute(ref LocalTransform localTransform, in UnitMover unitMover,
        ref PhysicsVelocity physicsVelocity)
    {
        var moveDirection = unitMover.TargetPosition - localTransform.Position;

        if (math.lengthsq(moveDirection) <= UnitMoverSystem.ReachedTargetPositionDistanceSq)
        {
            physicsVelocity.Linear = float3.zero;
            physicsVelocity.Angular = float3.zero;
            return;
        }

        moveDirection = math.normalize(moveDirection);

        localTransform.Rotation = math.slerp(localTransform.Rotation,
            quaternion.LookRotation(moveDirection, math.up()),
            DeltaTime * unitMover.RotationSpeed);

        physicsVelocity.Linear = moveDirection * unitMover.MoveSpeed;
        physicsVelocity.Angular = float3.zero;
    }
}