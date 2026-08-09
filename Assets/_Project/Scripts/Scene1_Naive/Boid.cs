using System.Collections.Generic;
using UnityEngine;

public class Boid : MonoBehaviour
{
    public Vector3 Position { get; private set; }
    public Vector3 Velocity { get; set; }

    private BoidSettingsSO _settings;
    private List<Boid> _allBoids;

    private Vector3 _acceleration = Vector3.zero;

    public void Init(BoidSettingsSO settings, List<Boid> allBoids)
    {
        _settings = settings;
        _allBoids = allBoids;

        SetInitialVelocity();
    }

    private void Update()
    {
        Position = transform.position;

        var separationForce = Vector3.zero;
        var avgVelocity = Vector3.zero;
        var avgPosition = Vector3.zero;

        int neighborCount = 0;
        int separationCount = 0;

        foreach (var boid in _allBoids)
        {
            if (boid == this) continue;

            Vector3 diff = Position - boid.Position;
            float sqrDistance = Vector3.SqrMagnitude(diff);
            if (sqrDistance > _settings.SqrNeighborRadius) continue;

            avgVelocity += boid.Velocity;
            avgPosition += boid.Position;
            neighborCount++;

            if (sqrDistance <= _settings.SqrSeparationRadius && sqrDistance > 0.0001f)
            {
                separationForce += diff / sqrDistance;
                separationCount++;
            }
        }

        Vector3 totalSteer = Vector3.zero;

        if (separationCount > 0)
        {
            Vector3 desiredSeparation = Vector3.ClampMagnitude(separationForce, _settings.MaxSpeed);
            Vector3 steerSeparation = desiredSeparation - Velocity;

            totalSteer += steerSeparation * _settings.SeparationWeight;
        }

        if (neighborCount > 0)
        {
            

            avgVelocity /= neighborCount;
            Vector3 desiredAlignment = avgVelocity.normalized * _settings.MaxSpeed;
            Vector3 steerAlignment = desiredAlignment - Velocity;

            avgPosition /= neighborCount;
            Vector3 cohesionDirection = (avgPosition - Position).normalized;
            Vector3 desiredCohesion = cohesionDirection * _settings.MaxSpeed;
            Vector3 steerCohesion = desiredCohesion - Velocity;

            totalSteer += steerAlignment * _settings.AlignmentWeight
                       + steerCohesion * _settings.CohesionWeight;
        }

        _acceleration += totalSteer;
    }

    private void LateUpdate()
    {
        _acceleration += GetBoxBoundSteer();
        _acceleration = Vector3.ClampMagnitude(_acceleration, _settings.MaxForce);

        Velocity += _acceleration * Time.deltaTime;
        Velocity = Vector3.ClampMagnitude(Velocity, _settings.MaxSpeed);

        if(Velocity.sqrMagnitude < _settings.SqrMinSpeed)
        {
            Vector3 moveDir = (Velocity.sqrMagnitude < 0.0001f) ? transform.forward : Velocity.normalized;
            Velocity = moveDir * _settings.MinSpeed;
        }

        transform.position += Velocity * Time.deltaTime;

        if (Velocity.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(Velocity);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }

        _acceleration = Vector3.zero;
    }

    private Vector3 GetBoxBoundSteer()
    {
        var desiredBound = Vector3.zero;

        desiredBound.x = CalculateAxisBoundForce(Position.x, _settings.Bounds.x);
        desiredBound.y = CalculateAxisBoundForce(Position.y, _settings.Bounds.y);
        desiredBound.z = CalculateAxisBoundForce(Position.z, _settings.Bounds.z);

        Vector3 steerBound = Vector3.ClampMagnitude(desiredBound, _settings.MaxSpeed) - Velocity;

        return steerBound * _settings.BoundsAvoidanceWeight;
    }

    private float CalculateAxisBoundForce(float positionOnAxis, float boundLimit)
    {
        float limit = boundLimit - _settings.BoundsAvoidanceMargin;
        float absPos = Mathf.Abs(positionOnAxis);

        if (absPos > limit)
        {
            float penetrationDistance = absPos - limit;
            float targetSpeed = Mathf.Lerp(0, _settings.MaxSpeed, penetrationDistance / _settings.BoundsAvoidanceMargin);
            return -Mathf.Sign(positionOnAxis) * targetSpeed;
        }

        return 0f;
    }

    private void SetInitialVelocity()
    {
        Vector3 randomDirection = Random.insideUnitSphere.normalized;
        float randomSpeed = Random.Range(_settings.MinSpeed, _settings.MaxSpeed);

        Velocity = randomDirection * randomSpeed;
    }
}
