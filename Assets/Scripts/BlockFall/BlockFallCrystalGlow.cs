using UnityEngine;

namespace BlockFall
{
    [ExecuteAlways]
    public sealed class BlockFallCrystalGlow : MonoBehaviour
    {
        // 기존 씬과 배경 제작 도구의 직렬화 및 참조를 유지한다.
        public float seed;
        public Light source;
        public Renderer halo;

        [Tooltip("구역 대표 수정에만 실제 주변 조명을 켭니다.")]
        public bool enableLocalLight = true;
        [Min(0f), Tooltip("이름은 호환성을 위해 유지하며, 이제 고정 조명 밝기로 사용합니다.")]
        public float peakIntensity = 1.3f;
        [Min(0f), Tooltip("수정 자체의 고정 발광 강도입니다.")]
        public float emissionStrength = 4.5f;
        [Range(0f, 1f), Tooltip("후광의 고정 투명도입니다.")]
        public float haloAlpha = .366f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int HaloColor = Shader.PropertyToID("_Color");
        private Renderer crystal;
        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock haloProperties;
        private Camera cameraReference;
        private bool refreshRequested;

        private void OnEnable()
        {
            ApplyFixedAppearance();
        }

        private void OnValidate()
        {
            // Inspector 변경을 예약하고, 렌더러 접근은 다음 에디터/게임 프레임에서 처리한다.
            refreshRequested = true;
        }

        private void ApplyFixedAppearance()
        {
            refreshRequested = false;
            if (crystal == null) crystal = GetComponent<Renderer>();
            if (properties == null) properties = new MaterialPropertyBlock();
            if (haloProperties == null) haloProperties = new MaterialPropertyBlock();

            // 발광과 후광은 활성화 또는 설정 변경 시 한 번만 적용한다.
            if (crystal != null)
            {
                crystal.GetPropertyBlock(properties);
                properties.SetColor(EmissionColor, new Color(.12f, .5f, 1f) * Mathf.Max(0f, emissionStrength));
                crystal.SetPropertyBlock(properties);
            }

            if (source != null)
            {
                source.enabled = enableLocalLight;
                source.renderMode = LightRenderMode.ForcePixel;
                source.range = 7.6f;
                source.intensity = Mathf.Max(0f, peakIntensity);
            }

            if (halo != null)
            {
                halo.GetPropertyBlock(haloProperties);
                haloProperties.SetColor(HaloColor, new Color(.12f, .48f, 1f, Mathf.Clamp01(haloAlpha)));
                halo.SetPropertyBlock(haloProperties);
                Vector3 parentScale = halo.transform.parent != null ? halo.transform.parent.lossyScale : Vector3.one;
                halo.transform.localScale = new Vector3(
                    2.6f / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
                    2.6f / Mathf.Max(.001f, Mathf.Abs(parentScale.y)),
                    halo.transform.localScale.z);
            }
        }

        private void LateUpdate()
        {
            if (refreshRequested) ApplyFixedAppearance();

            // 카메라 방향이 달라졌을 때만 후광의 방향을 맞춘다. 밝기는 변경하지 않는다.
            if (halo == null) return;
            if (cameraReference == null || !cameraReference.isActiveAndEnabled) cameraReference = Camera.main;
            if (cameraReference != null && halo.transform.rotation != cameraReference.transform.rotation)
                halo.transform.rotation = cameraReference.transform.rotation;
        }
    }
}
