using EZ.Index.DemoUtils;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace EZ.Index.InverseHashing
{
    [ExecuteInEditMode]
    public class HeightmapGenerator : MonoBehaviour
    {
        enum Image
        {
            UV,
            Noise
        }

        [Serializable]
        struct Noise
        {
            public enum Algorithm
            {
                Perlin,
                Simplex,
                VoronoiF1,
                VoronoiF2,
                FastVoronoiF1,
                FastVoronoiF2
            }

            public static Noise Default => new()
            {
                algorithm = Algorithm.Simplex,
                frequency = 0.05f,
                amplitude = 1f,
                filter = AnimationCurve.Linear(0, 0, 1, 1),
                enabled = true
            };

            public Algorithm algorithm;
            public bool enabled;
            [Range(0.00001f, 0.1f)] public float frequency;
            [Min(0.01f)] public float amplitude;
            [Min(0)] public float domainWarp;
            public float2 offset;
            public AnimationCurve filter;

            public int UpdateHash()
            {
                var h = HashCode.Combine(enabled, algorithm, frequency, offset, amplitude, domainWarp, filter);
                return h;
            }
        }

        [Serializable]
        struct Config
        {
            public Image image;
            public int2 resolution;
            public List<Noise> modulations;
            public Gradient gradient;

            [Min(0.00001f)] public float zoom;
            public float2 offset;

            [Header("Mesh Visualization")]
            public bool meshVisualizer;
            public float height;

#if UNITY_EDITOR
            [ReadOnly]
#endif
            public float maxAmplitude;
            int hash;
            public bool Updated()
            {
                resolution = max(2, resolution);

                var h = HashCode.Combine(image, resolution);

                if (image == Image.Noise)
                {
                    h += HashCode.Combine(meshVisualizer, offset, zoom);

                    if (meshVisualizer)
                        h += HashCode.Combine(height, gradient);

                    maxAmplitude = 0;
                    for (int o = 0; o < modulations.Count; o++)
                    {
                        var modulation = modulations[o];

                        if (!modulation.enabled) continue;

                        if (modulation.frequency == 0)
                            modulation = Noise.Default;

                        h += HashCode.Combine(h) + modulation.UpdateHash();
                        maxAmplitude += modulation.amplitude;
                    }
                    maxAmplitude = max(1, maxAmplitude);
                }

                if (h != hash)
                {
                    hash = h;
                    return true;
                }
                return false;
            }
        }

        [SerializeField]
        Config cfg = new()
        {
            resolution = int2(256, 256),
            modulations = new() { Noise.Default },
            height = 1,
            gradient = new(),
            zoom = 1f,
            offset = float2(341,652)
        };

        [HideInInspector] public Texture2D texture;
        private void Update()
        {
            if (!cfg.Updated()) return;

            var pixelCount = cfg.resolution.x * cfg.resolution.y;
            texture = new(cfg.resolution.x, cfg.resolution.y);
            List<Vector3> vertices = new(pixelCount);
            List<int> indices = new(pixelCount * 3 * 2);
            List<Vector2> uvs = new(pixelCount * 3 * 2);
            var minHeight = cfg.gradient.colorKeys[0].time * cfg.height;
            for (int i = 0; i < pixelCount; i++)
            {
                var node = Grid.WholeNode(i, cfg.resolution);
                var uv = node / cfg.resolution;
                var texCoords = int2(node);
                float height;
                if (cfg.image == Image.UV)
                {
                    var color = new Color(uv.x, uv.y, 0);

                    texture.SetPixel(texCoords.x, texCoords.y, color);
                    height = length(uv);
                }
                else
                {
                    var ncfg = cfg.modulations;
                    var modulation = 0f;
                    foreach (var octave in ncfg)
                    {
                        if (!octave.enabled) continue;

                        var sample = node * cfg.zoom * octave.frequency + (octave.domainWarp * modulation) + octave.offset + cfg.offset;
                        var h = octave.algorithm switch
                        {
                            Noise.Algorithm.Simplex => remap(-1, 1, 0, 1, noise.snoise(sample)),
                            Noise.Algorithm.Perlin => remap(-1, 1, 0, 1, noise.cnoise(sample)),
                            Noise.Algorithm.VoronoiF1 => saturate(noise.cellular(sample).x),
                            Noise.Algorithm.VoronoiF2 => saturate(noise.cellular(sample).y),
                            Noise.Algorithm.FastVoronoiF1 => saturate(noise.cellular2x2(sample).x),
                            Noise.Algorithm.FastVoronoiF2 => saturate(noise.cellular2x2(sample).y),
                            _ => throw new NotImplementedException(),
                        };

                        h = octave.filter.Evaluate(h);
                        modulation += h * octave.amplitude;
                    }

                    var color = cfg.maxAmplitude == 0 ? Color.black : cfg.gradient.Evaluate(modulation / cfg.maxAmplitude);
                    texture.SetPixel(texCoords.x, texCoords.y, color);
                    height = cfg.maxAmplitude == 0 ?  0 : modulation / cfg.maxAmplitude * cfg.height;
                }

                if (!cfg.meshVisualizer) continue;

                vertices.Add(float3(node.x, max(minHeight, height), node.y));
                uvs.Add(uv);

                if (node.x == cfg.resolution.x - 1 || node.y == cfg.resolution.y - 1) continue;

                indices.Add(i + 0);
                indices.Add(i + cfg.resolution.x);
                indices.Add(i + cfg.resolution.x + 1);

                indices.Add(i + 1 + cfg.resolution.x);
                indices.Add(i + 1);
                indices.Add(i + 0);
            }
            texture.Apply();

            if (!cfg.meshVisualizer) return;

            var mesh = new Mesh();
            transform.GetComponent<MeshFilter>().mesh = mesh;
            mesh.indexFormat = pixelCount >= short.MaxValue ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.Optimize();

            transform.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_MainTex", texture);
        }
    }
}