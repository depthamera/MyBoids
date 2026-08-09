using System.Collections.Generic;
using UnityEngine;

public class BoidSpawner : MonoBehaviour
{
    public BoidSettingsSO Settings;

    public GameObject BoidPrefab;

    private List<Boid> _boids;

   private void Start()
    {
        _boids = new List<Boid>();
        for (int i = 0; i < Settings.BoidCount; i++)
        {
            var boidObj = Instantiate(BoidPrefab, transform);
            boidObj.transform.position = new Vector3(
                Random.Range(-Settings.Bounds.x, Settings.Bounds.x),
                Random.Range(-Settings.Bounds.y, Settings.Bounds.y),
                Random.Range(-Settings.Bounds.z, Settings.Bounds.z)
            );

            var boid = boidObj.GetComponent<Boid>();

            boid.Init(Settings, _boids);
            _boids.Add(boid);
        }
    }


}
