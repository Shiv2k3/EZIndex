#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace EZ.Index.InverseHashing
{
    [CustomEditor(typeof(HeightmapGenerator))]
    public class HeightmapGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            HeightmapGenerator myScript = (HeightmapGenerator)target;
            if (myScript.texture != null)
            {
                GUILayout.Label(myScript.texture, GUILayout.Width(myScript.texture.width), GUILayout.Height(myScript.texture.height));
            }
        }
    }
}
#endif