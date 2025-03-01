using UnityEngine;

namespace EZ.Index.PrefabSpawnning
{
    public class OceanAnimator : MonoBehaviour
    {
        [SerializeField] Material material;
        [SerializeField, Min(0.001f)] float mainOffsetSpeed = 1;
        [SerializeField, Min(0.001f)] float detailOffsetSpeed = 1;

        void Update()
        {
            var mainOffset = mainOffsetSpeed * Time.time * Vector2.one;
            material.SetTextureOffset("_MainTex", mainOffset);

            var detailOffset = detailOffsetSpeed * Time.time * Vector2.one;
            material.SetTextureOffset("_DetailAlbedoMap", -detailOffset);
        }
    }
}