using UnityEngine;

/// <summary>
/// Boids 시뮬레이션의 공통 파라미터를 담는 SO.
/// 세 씬이 동일한 에셋을 참조하여, 조건을 동일하게 유지한다.
/// </summary>
[CreateAssetMenu(fileName = "BoidSettings", menuName = "Boids/Boid Settings")]
public class BoidSettingsSO : ScriptableObject
{
    [Header("개체 수")]
    [Tooltip("시뮬레이션에 스폰할 개체 수")]
    [Min(1)]
    public int BoidCount = 500;

    [Header("이동")]
    [Tooltip("개체가 이동할 수 있는 최소 속도")]
    public float MinSpeed = 1f;

    [Tooltip("개체가 이동할 수 있는 최대 속도")]
    public float MaxSpeed = 5f;

    [Tooltip("한 프레임에 가할 수 있는 최대 힘")]
    public float MaxForce = 8f;

    [Header("이웃 탐색")]
    [Tooltip("개체가 이웃으로 인식할 범위")]
    public float NeighborRadius = 3f;

    [Tooltip("개체가 가까운 이웃으로 인식할 범위")]
    public float SeparationRadius = 1f;

    [Header("가중치")]
    [Tooltip("가까운 이웃으로부터 멀어지려는 힘의 가중치")]
    public float SeparationWeight = 1.5f;

    [Tooltip("이웃과 이동 방향을 맞추려는 힘의 가중치")]
    public float AlignmentWeight = 1f;

    [Tooltip("이웃의 중심으로 이동하려는 힘의 가중치")]
    public float CohesionWeight = 1f;

    [Header("회피")]
    [Tooltip("개체가 활동하는 공간의 절반 크기 (원점 기준 -bounds ~ +bounds)")]
    public Vector3 Bounds = new(25f, 15f, 10f);

    [Tooltip("개체가 경계를 인식하기 시작하는 거리")]
    public float BoundsAvoidanceMargin = 5f;

    [Tooltip("경계로부터 멀어지려는 힘의 가중치")]
    public float BoundsAvoidanceWeight = 3f;

    // 계산 최적화를 위한 제곱 값
    public float SqrMinSpeed { get; private set; }
    public float SqrNeighborRadius { get; private set; }
    public float SqrSeparationRadius { get; private set; }


    private void OnValidate()
    {
        if(MinSpeed > MaxSpeed)
        {
            MinSpeed = MaxSpeed;
        }

        if (SeparationRadius > NeighborRadius)
        {
            SeparationRadius = NeighborRadius;
        }

        CacheSqrValues();
    }
    private void OnEnable()
    {
        CacheSqrValues();
    }

    private void CacheSqrValues()
    {
        SqrMinSpeed = MinSpeed * MinSpeed;
        SqrNeighborRadius = NeighborRadius * NeighborRadius;
        SqrSeparationRadius = SeparationRadius * SeparationRadius;
    }


}
