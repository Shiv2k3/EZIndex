#if UNITY_EDITOR
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using EZ.Index.DemoUtils;
using UnityEditor;
using System.Collections.Generic;

namespace EZ.Index.GizmoDemo
{
    public class GizmosDemo : MonoBehaviour
    {
        public enum Domain
        {
            Whole2D,
            Center2D,
            Corner2D,

            Whole3D,
            Center3D,
            Corner3D,

            Spherical
        }

        [System.Serializable]
        struct Configuration
        {
            [Tooltip("The node-index space")]
            public Domain domain;
            [Tooltip("The ratio of the bounds that includes the node-index space")]
            public int3 ratio;
            [Tooltip("Number of nodes layers on the sphere, only used when the domain is Polar")]
            public int layers;
            [Tooltip("Radius of the sphere, only used when the domain is Polar")]
            public float radius;

            public static Configuration Default => new()
            {
                domain = Domain.Center2D,
                ratio = 5,
                layers = 15,
                radius = 5
            };
        }

        [System.Serializable]
        struct Result
        {
            [Tooltip("The minimum node within domain bounds")]
            public float3 minimumNode;
            [Tooltip("The maximmum node within domain bounds")]
            public float3 maximumNode;
            [Tooltip("The index min-max range within domain bounds")]
            public int2 indexRange;
            [Tooltip("The total number of nodes within domain bounds")]
            public int total;

            public static Result Default => new()
            {
                minimumNode = INFINITY,
                maximumNode = -INFINITY,
                indexRange = int2(int.MaxValue, int.MinValue),
                total = 0
            };

            public void Update(in float3 node, in int index)
            {
                minimumNode = min(node, minimumNode);
                maximumNode = max(node, maximumNode);
                indexRange = int2(min(indexRange.x, index), max(indexRange.y, index));
            }
        }

        [SerializeField, Tooltip("Node generation parameters")] Configuration configuration = Configuration.Default;
        [SerializeField, Tooltip("Results of the node generation"), ReadOnly] Result result = Result.Default;

        enum DebugLevel
        {
            None,
            OnlyIndex,
            CrossCheck
        }

        [Header("Error Debugging")]
        [SerializeField] DebugLevel debugLevel;
        [SerializeField] bool throwError;

        float3 GetNode(in int i)
        {
            var node = configuration.domain switch
            {
                Domain.Center2D => float3(Grid.CenterNode(i, configuration.ratio.xy), 0),
                Domain.Corner2D => float3(Grid.CornerNode(i, configuration.ratio.xy), 0),
                Domain.Whole2D => float3(Grid.WholeNode(i, configuration.ratio.xy), 0),
                Domain.Whole3D => Lattice.WholeNode(i, configuration.ratio),
                Domain.Center3D => Lattice.CenterNode(i, configuration.ratio),
                Domain.Corner3D => Lattice.CornerNode(i, configuration.ratio),
                Domain.Spherical => Spherical.GetNode(i, configuration.layers).GetCartesian(),
                _ => throw new("Unknown input"),
            };

            if (debugLevel > DebugLevel.None)
            {
                var matchingIndex = configuration.domain switch
                {
                    Domain.Center2D => Grid.CenterIndex(node.xy, configuration.ratio.xy),
                    Domain.Corner2D => Grid.CornerIndex(node.xy, configuration.ratio.xy),
                    Domain.Whole2D => Grid.WholeIndex(node.xy, configuration.ratio.xy),
                    Domain.Whole3D => Lattice.WholeIndex(node, configuration.ratio),
                    Domain.Center3D => Lattice.CenterIndex(node, configuration.ratio),
                    Domain.Corner3D => Lattice.CornerIndex(node, configuration.ratio),
                    Domain.Spherical => Spherical.GetIndex(new Spherical.Angle(node), configuration.layers),
                    _ => throw new("Unknown input"),
                };

                if (debugLevel == DebugLevel.OnlyIndex)
                {
                    if (i != matchingIndex)
                    {
                        var msg = $"Expected: {i} Result: {matchingIndex}";
                        if (throwError)
                            throw new(msg);
                        else
                            Debug.LogError(msg);
                    }
                }
                else
                {
                    var matchingNode = configuration.domain switch
                    {
                        Domain.Center2D => float3(Grid.CenterNode(matchingIndex, configuration.ratio.xy), 0),
                        Domain.Corner2D => float3(Grid.CornerNode(matchingIndex, configuration.ratio.xy), 0),
                        Domain.Whole2D => float3(Grid.WholeNode(matchingIndex, configuration.ratio.xy), 0),
                        Domain.Whole3D => Lattice.WholeNode(matchingIndex, configuration.ratio),
                        Domain.Center3D => Lattice.CenterNode(matchingIndex, configuration.ratio),
                        Domain.Corner3D => Lattice.CornerNode(matchingIndex, configuration.ratio),
                        Domain.Spherical => Spherical.GetNode(matchingIndex, configuration.layers).GetCartesian(),
                        _ => throw new("Unknown input"),
                    };

                    if (matchingIndex != i || any(matchingNode != node))
                    {
                        string msg;
                        if (configuration.domain == Domain.Spherical)
                        {
                            var expected = new Spherical.Angle(node);
                            var result = new Spherical.Angle(matchingNode);
                            msg = $"(Expected : ({i} == {expected}) !=  Result: {matchingIndex} == {result})";
                        }
                        else
                        {
                            msg = $"(Expected : ({i} == {node}) !=  Result: {matchingIndex} == {matchingNode})";
                        }

                        if (throwError)
                            throw new(msg);
                        else
                            Debug.LogError(msg);
                    }
                }
            }

            if (configuration.domain == Domain.Spherical)
            {
                node *= configuration.radius;
            }

            return node;
        }
        int GetTotal()
        {
            return configuration.domain switch
            {
                Domain.Center2D => Grid.GetTotal(configuration.ratio.xy, Index.Domain.Centers),
                Domain.Corner2D => Grid.GetTotal(configuration.ratio.xy, Index.Domain.Corners),
                Domain.Whole2D => Grid.GetTotal(configuration.ratio.xy, Index.Domain.Wholes),
                Domain.Whole3D => Lattice.GetTotal(configuration.ratio, Index.Domain.Wholes),
                Domain.Center3D => Lattice.GetTotal(configuration.ratio, Index.Domain.Centers),
                Domain.Corner3D => Lattice.GetTotal(configuration.ratio, Index.Domain.Corners),
                Domain.Spherical => Spherical.GetTotal(configuration.layers),
                _ => throw new("Unknown input"),
            };
        }
        float3 GetBoundry(in float3 node)
        {
            return configuration.domain switch
            {
                Domain.Center2D => float3(Grid.BoundryCenter(node.xy, configuration.ratio.xy), 0),
                Domain.Corner2D => float3(Grid.BoundryCorner(node.xy, configuration.ratio.xy), 0),
                Domain.Whole2D => float3(Grid.BoundryWhole(node.xy, configuration.ratio.xy), 0),
                Domain.Whole3D => Lattice.BoundryWhole(node, configuration.ratio),
                Domain.Center3D => Lattice.BoundryCenter(node, configuration.ratio),
                Domain.Corner3D => Lattice.BoundryCorner(node, configuration.ratio),
                Domain.Spherical => 0,

                _ => throw new("Unknown input"),
            };

        }

        [System.Serializable]
        struct Gizmo
        {
            public enum Handle
            {
                Index,
                Node,
                Boundry,
                None
            }

            [Header("Labels")]
            [Tooltip("Kind of node's infomation to draw")]
            public Handle handle;
            [Tooltip("Screen space offset of the text labels")]
            public float2 handleOffset;

            [Header("    Gizmos")]
            [Tooltip("Color the node based on the handle (x), Draw the node's text labels (y)")]
            public bool2 colorText;
            [Tooltip("The maximum render distance for 3D nodes")]
            public float renderDistance;
            [Tooltip("Radius of the nodes")]
            public float radius;

            [Header("Extra")]
            [Tooltip("Additionally draw the Corner nodes when the domain is set to Centers")]
            public bool centerChecker;
            [Tooltip("Renders a sphere at intersection between the current camera and the sphere")]
            public bool debugAngles;
            [Min(0), Tooltip("Last node index to draw")] public int maxIndex;

            public static Gizmo Default => new()
            {
                radius = 0.05f,
                handleOffset = float2(1, 1),
                maxIndex = int.MaxValue,
                colorText = bool2(true, false),
                renderDistance = 60f
            };
        }

        [SerializeField, Tooltip("The parameters for drawing gizmos")] Gizmo gizmos = Gizmo.Default;
        private void OnDrawGizmos()
        {
            if (gizmos.debugAngles)
            {
                var cam = Camera.current.transform; // Get the current camera's transform
                var angle = new Spherical.Angle(cam.position.normalized);  // get the camera's spherical coordinates
                var index = Spherical.GetIndex(angle, configuration.layers); // calculate the neareast node's index
                var camIntersection = angle.GetCartesian() * configuration.radius;
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(camIntersection, gizmos.radius); // draw the node as a cube
                Handles.Label(GetHandlePosition(camIntersection), index.ToString());
            }

            result = Result.Default;
            Draw();
            if (configuration.domain == Domain.Center2D && gizmos.centerChecker)
            {
                configuration.domain = Domain.Corner2D;
                Draw();
                configuration.domain = Domain.Center2D;
            }
            if (configuration.domain == Domain.Center3D && gizmos.centerChecker)
            {
                configuration.domain = Domain.Corner3D;
                Draw();
                configuration.domain = Domain.Center3D;
            }


            void Draw()
            {
                var total = GetTotal();
                result.total = total;
                for (int i = 0; i < total; i++)
                {
                    if (i == gizmos.maxIndex) break;

                    var node = GetNode(i);
                    result.Update(node, i);

                    if (configuration.domain > Domain.Corner2D && gizmos.renderDistance < distance(node, Camera.current.transform.position)) continue;

                    var rng = Unity.Mathematics.Random.CreateFromIndex((uint)i);
                    var color = (Vector4)float4(normalize(rng.NextFloat3()), 1);
                    var boundry = configuration.domain == Domain.Spherical ? 0 : GetBoundry(node);
                    if (any(gizmos.colorText))
                    {
                        switch (gizmos.handle)
                        {
                            case Gizmo.Handle.Index:
                                color = Color.white * (Vector4)float4(float3(i / ((float)total - 1)), 1);
                                if (gizmos.colorText.y)
                                {
                                    var style = new GUIStyle() { normal = new() { textColor = gizmos.colorText.x ? color : Color.black } };
                                    Handles.Label(GetHandlePosition(node), $"{i}", style);
                                }
                                break;

                            case Gizmo.Handle.Node:
                                var uv = configuration.domain == Domain.Spherical ? float3(new Spherical.Angle(normalize(node)).cords, 0) : node / GetNode(total - 1);
                                color = uv.y == 0 ? new Color(uv.x, uv.z, uv.y, 1) : new Color(uv.x, uv.y, uv.z, 1);
                                if (gizmos.colorText.y)
                                {
                                    var style = new GUIStyle() { normal = new() { textColor = gizmos.colorText.x ? color : Color.black } };

                                    if (configuration.domain == Domain.Spherical)
                                        Handles.Label(GetHandlePosition(node), $"({uv.x} , {uv.y})", style);
                                    else
                                        Handles.Label(GetHandlePosition(node), $"({node.x} , {(configuration.domain < Domain.Corner3D ? node.y : node.z)})", style);
                                }
                                break;

                            case Gizmo.Handle.Boundry:
                                var red = select(0, 1f, boundry.x == 1f);
                                red += select(0, 0.3f, boundry.x == -1);

                                var green = select(0, 1f, boundry.y == 1);
                                green += select(0, 0.3f, boundry.y == -1);

                                var blue = select(0, 1f, boundry.z == 1);
                                blue += select(0, 0.3f, boundry.z == -1);

                                color = (Vector4)float4(red, green, blue, 1);
                                if (gizmos.colorText.y)
                                {
                                    var style = new GUIStyle() { normal = new() { textColor = gizmos.colorText.x ? color : Color.black } };
                                    if (!all(boundry == 0))
                                    {
                                        var msg = blue == 0 ? $"{boundry.x}|{boundry.y}" : $"{boundry.x}|{boundry.y}|{boundry.z}";
                                        Handles.Label(GetHandlePosition(node), msg, style);
                                    }
                                }
                                break;
                        }
                    }

                    Gizmos.color = gizmos.colorText.x ? color : Color.white;
                    Gizmos.DrawSphere(node, gizmos.radius);
                }
                var center = all(result.minimumNode == 0) ? result.maximumNode / 2f : result.maximumNode + result.minimumNode;
                var size = result.maximumNode + abs(result.minimumNode);
                Gizmos.DrawWireCube(center, size);

                if (configuration.domain == Domain.Spherical) return;

                var sizeStyle = new GUIStyle() { normal = new() { textColor = new(183f / 255, 131f / 255, 72f / 255) }, fontSize = 25 };
                if (size.x > 1)
                {
                    var h = result.minimumNode + float3(size.x / 2f, 0, 0);
                    h.zy -= 0.5f;
                    Handles.Label(h, configuration.ratio.x.ToString(), sizeStyle);
                }

                if (size.y > 1)
                {
                    var h = result.minimumNode + float3(0, size.y / 2f, 0);
                    h.xz -= 0.5f;
                    Handles.Label(h, configuration.ratio.y.ToString(), sizeStyle);
                }

                if (size.z > 1)
                {
                    var h = result.minimumNode + float3(0, 0, size.z / 2f);
                    h.xy -= 0.5f;
                    Handles.Label(h, configuration.ratio.z.ToString(), sizeStyle);
                }
            }
            Vector3 GetHandlePosition(in Vector3 position)
            {
                var l = Camera.current.transform.worldToLocalMatrix * position;
                l += new Vector4(gizmos.handleOffset.x, gizmos.handleOffset.y, 0, 0) * gizmos.radius;
                return Camera.current.transform.localToWorldMatrix * l;
            }
        }
    }
}
#endif