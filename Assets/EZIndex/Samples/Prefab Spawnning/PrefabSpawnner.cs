using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;

namespace EZ.Index.PrefabSpawnning
{
    public enum Domain
    {
        Polar,
        Whole2D,
        Corner2D,
        Center2D,

        Whole3D,
        Corner3D,
        Center3D,
    }

    [ExecuteInEditMode]
    public class PrefabSpawner : MonoBehaviour
    {
        [SerializeField] List<GameObject> prefabs;

        [Serializable]
        struct Jitter
        {
            public float3 position;
            public bool3 saturate;
            public float3 rotation;
            public float3 scale;
            public float2 range;
            public bool uniform;

            public static Jitter Zero =>
            new()
            {
                position = Unity.Mathematics.float3.zero,
                rotation = Unity.Mathematics.float3.zero,
                scale = Unity.Mathematics.float3.zero
            };

            public override bool Equals(object obj)
            {
                return base.Equals(obj);
            }

            public override readonly int GetHashCode()
            {
                return base.GetHashCode();
            }

            public static bool3x3 operator ==(Jitter left, Jitter right)
            {
                return new
                (
                    left.position == right.position,
                    left.rotation == right.rotation,
                    left.scale == right.scale
                );
            }

            public static bool3x3 operator !=(Jitter left, Jitter right)
            {
                return new
                (
                    left.position != right.position,
                    left.rotation != right.rotation,
                    left.scale != right.scale
                );
            }
        }

        [Serializable]
        struct Config
        {
            [Header(header: "Indexer Settings")]
            public Domain domain;
            public bool swapYZ;
            public int3 ratio;
            public float scale;
            public uint seed;

            [Header("Prefab Settings")]
            [Range(0.001f, 1f)] public float spawnRate;
            public Jitter jitter;

            [Header("Sphere")]
            [Min(2)] public int nodeLayers;
            [Min(1)] public float radius;
            public bool rotateOutwards;

            private int hash;
            public readonly bool Is3D => (int)domain > (int)Domain.Center2D;
            public bool Updated()
            {
                var h = domain.GetHashCode() + scale.GetHashCode() + seed.GetHashCode() + jitter.GetHashCode() + spawnRate.GetHashCode() + rotateOutwards.GetHashCode();

                if (domain is Domain.Polar)
                {
                    h += nodeLayers.GetHashCode() + radius.GetHashCode();
                }
                else
                {
                    if (Is3D)
                        h += ratio.GetHashCode();
                    else
                        h += ratio.xy.GetHashCode();

                    h += swapYZ.GetHashCode();
                }

                if (h != hash)
                {
                    hash = h;
                    return true;
                }

                return false;
            }
        }

        [Header("Parameters")]
        [SerializeField] float executionTime;

        [SerializeField]
        Config configuration = new()
        {
            seed = (uint)DateTime.Now.Millisecond,
            ratio = new(13, 19, 15),
            scale = 3,
            swapYZ = true,
            spawnRate = 0.25f,
            nodeLayers = 2,
            radius = 1f,

            jitter = new()
            {
                range = float2(-1, 1)
            }
        };

        private void Update()
        {
            if (prefabs == null || prefabs.Count == 0 || prefabs.Any((x) => x == null)) return;

            configuration.ratio = max(int3(1, 1, 1), configuration.ratio);
            configuration.scale = max(configuration.scale, 0.01f);

            if (configuration.Updated())
            {
                var t = Time.realtimeSinceStartup;
                SpawnPrefabs();
                executionTime = Time.realtimeSinceStartup - t;
            }
        }

        private void OnEnable()
        {
            transforms = new(0, Allocator.Persistent);
            indicesComplete = new(0, Allocator.Persistent);
        }
        private void OnDisable()
        {
            transforms.Dispose();
            indicesComplete.Dispose();
        }
        private NativeList<float3x3> transforms;
        private NativeList<int> indicesComplete;

        [ContextMenu("Spawn Prefabs")]
        private void SpawnPrefabs()
        {
            EmptyTransform();

            if (!transforms.IsCreated || !indicesComplete.IsCreated)
                OnEnable(); 
                        
            var job = new SpawnJob(configuration, ref transforms, ref indicesComplete);
            var handle = job.Schedule(job.total, job.total / JobsUtility.ThreadIndexCount);

            var rng = Unity.Mathematics.Random.CreateFromIndex(configuration.seed);
            var completeSet = new HashSet<int>();
            while (handle.IsCompleted == false)
            {
                for (int index = 0; index < indicesComplete.Length; index++)
                {
                    if (completeSet.Contains(index)) continue;

                    var transform = transforms[index];
                    var prefab = Instantiate(prefabs[rng.NextInt(0, prefabs.Count)]).transform;
                    prefab.parent = this.transform;
                    prefab.localPosition = transform.c0;
                    prefab.localEulerAngles = transform.c1;
                    prefab.localScale = transform.c2;

                    completeSet.Add(index);
                }
            }

            handle.Complete();
        }

        [ContextMenu("Remove Children")]
        private void EmptyTransform()
        {
            while (transform.childCount > 0)
            {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        readonly struct SpawnJob : IJobParallelFor
        {
            public SpawnJob(in Config cfg, ref NativeList<float3x3> transforms, ref NativeList<int> indicesComplete)
            {
                this.cfg = cfg;

                total = cfg.domain is Domain.Polar
                    ? Spherical.GetTotal(cfg.nodeLayers)
                    : (int)cfg.domain > (int)Domain.Center2D ? cfg.ratio.x * cfg.ratio.y * cfg.ratio.z 
                    : cfg.ratio.x * cfg.ratio.y;

                transforms.Clear();
                indicesComplete.Clear();
                transforms.Capacity = total;
                indicesComplete.Capacity = total;

                tfs = transforms.AsParallelWriter();
                complete = indicesComplete.AsParallelWriter();

                unsafe
                {
                    var c = new NativeArray<int>(1, Allocator.Persistent);
                    c[0] = 0;
                    counter = new(c.GetUnsafePtr());
                }

            }

            public readonly int total;

            [WriteOnly, NativeDisableContainerSafetyRestriction] readonly NativeList<int>.ParallelWriter complete;
            [WriteOnly, NativeDisableContainerSafetyRestriction] readonly NativeList<float3x3>.ParallelWriter tfs;
            [NativeDisableUnsafePtrRestriction] readonly UnsafeAtomicCounter32 counter;
            readonly Config cfg;

            public void Execute(int i)
            {
                var rng = Unity.Mathematics.Random.CreateFromIndex((uint)(cfg.seed * (JobsUtility.ThreadIndex + 1) + i));
                if (rng.NextFloat() > cfg.spawnRate) return;

                var sample = cfg.domain switch
                {
                    Domain.Center2D => float3(Grid.CenterNode(i, cfg.ratio.xy), 0),
                    Domain.Corner2D => float3(Grid.CornerNode(i, cfg.ratio.xy), 0),
                    Domain.Whole2D => float3(Grid.WholeNode(i, cfg.ratio.xy), 0),
                    Domain.Center3D => Lattice.CenterNode(i, cfg.ratio),
                    Domain.Corner3D => Lattice.CornerNode(i, cfg.ratio),
                    Domain.Whole3D => Lattice.WholeNode(i, cfg.ratio),
                    Domain.Polar => Spherical.GetNode(i, cfg.nodeLayers).GetCartesian() * cfg.radius,
                    _ => throw new NotImplementedException(),
                };

                sample *= cfg.scale;
                sample.yz = cfg.swapYZ ? sample.zy : sample.yz;


                var transform = float3x3(sample, 0, 1);
                var isZero = cfg.jitter == Jitter.Zero;
                if (any(!isZero.c0))
                {
                    var position = rng.NextFloat3(-1f, 1f) * cfg.jitter.position;
                    position = select(position, abs(position) * sign(cfg.jitter.position), cfg.jitter.saturate);
                    transform.c0 += position;
                }

                if (cfg.rotateOutwards)
                {
                    var quat = Quaternion.FromToRotation(up(), normalizesafe(sample, up()));
                    transform.c1 = Euler(quat) * TODEGREES;
                }

                if (any(!isZero.c1))
                {
                    var rotation = rng.NextFloat3(-cfg.jitter.rotation, cfg.jitter.rotation);
                    transform.c1 += rotation;
                }

                if (any(!isZero.c2))
                {
                    var scale = !cfg.jitter.uniform ? rng.NextFloat3(cfg.jitter.range.x, cfg.jitter.range.y) : rng.NextFloat(cfg.jitter.range.x, cfg.jitter.range.y);
                    scale *= cfg.jitter.scale;
                    transform.c2 += scale;
                }

                tfs.AddNoResize(transform);
                complete.AddNoResize(counter.Add(1));
            }
        }
    }
}