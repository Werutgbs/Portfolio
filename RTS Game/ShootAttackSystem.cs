using Authoring;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Handles ranged combat behavior: chase target, aim, and fire projectiles.
/// Units move toward targets outside attack range, then stop and shoot when in range.
/// </summary>
internal partial struct ShootAttackSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var entitiesReferences = SystemAPI.GetSingleton<EntitiesReferences>();
        foreach (var (localTransform, shootAttack, target, unitMover) in SystemAPI
                     .Query<RefRW<LocalTransform>, RefRW<ShootAttack>, RefRO<Target>, RefRW<UnitMover>>()
                     .WithDisabled<MoveOverride>())
        {
            if (target.ValueRO.TargetEntity == Entity.Null) continue;

            var targetLocalTransform = SystemAPI.GetComponent<LocalTransform>(target.ValueRO.TargetEntity);
            if (math.distance(localTransform.ValueRO.Position, targetLocalTransform.Position) >
                shootAttack.ValueRO.AttackDistance)
            {
                unitMover.ValueRW.TargetPosition = targetLocalTransform.Position;
                continue;
            }

            unitMover.ValueRW.TargetPosition = localTransform.ValueRO.Position;

            var aimDirection = math.normalize(targetLocalTransform.Position - localTransform.ValueRO.Position);

            var targetRotation = quaternion.LookRotation(aimDirection, math.up());

            localTransform.ValueRW.Rotation = math.slerp(localTransform.ValueRO.Rotation, targetRotation,
                SystemAPI.Time.DeltaTime * unitMover.ValueRO.RotationSpeed);

            shootAttack.ValueRW.Timer -= SystemAPI.Time.DeltaTime;
            if (shootAttack.ValueRO.Timer > 0f) continue;

            shootAttack.ValueRW.Timer = shootAttack.ValueRO.TimerMax;

            var bulletEntity = state.EntityManager.Instantiate(entitiesReferences.BulletPrefabEntity);
            var bulletSpawnWorldPosition =
                localTransform.ValueRO.TransformPoint(shootAttack.ValueRO.BulletSpawnLocalPosition);
            SystemAPI.SetComponent(bulletEntity, LocalTransform.FromPosition(bulletSpawnWorldPosition));

            var bullet = SystemAPI.GetComponentRW<Bullet>(bulletEntity);
            bullet.ValueRW.DamageAmount = shootAttack.ValueRO.DamageAmount;
            var bulletTarget = SystemAPI.GetComponentRW<Target>(bulletEntity);
            bulletTarget.ValueRW.TargetEntity = target.ValueRO.TargetEntity;

            shootAttack.ValueRW.OnShoot.ShootFromPosition = bulletSpawnWorldPosition;
            shootAttack.ValueRW.OnShoot.IsTriggered = true;
        }
    }
}