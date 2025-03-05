using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;

namespace EZ.Index.GizmoDemo
{
    public class PickupExample : MonoBehaviour
    {
        private readonly Dictionary<int, GameObject> healthdrops = new();
        [SerializeField] GameObject heartPrefab;
        [SerializeField] Transform player;
        [SerializeField] int2 ratio;
        private void Start()
        {
            var total = Grid.GetTotal(ratio, Domain.Centers);
            for (int i = 0; i < total; i++)
            {
                if (Unity.Mathematics.Random.CreateFromIndex((uint)(i + 938983)).NextBool()) continue;
                var go = Instantiate(heartPrefab, transform);
                go.transform.position = float3(Grid.CenterNode(i, ratio), 0);
                healthdrops.Add(i, go);
            }
        }

        private void Update()
        {
            var playerPosition = float3(player.position);
            var node = Grid.NeareastCenter(playerPosition.xy, ratio);
            var index = Grid.CenterIndex(node, ratio);
            if (healthdrops.TryGetValue(index, out var drop))
            {
                var closeEnough = distance(playerPosition, drop.transform.position) <= 1;
                if (closeEnough)
                {
                    healthdrops.Remove(index);
                    Destroy(drop);
                }
            }
        }

    }
}
