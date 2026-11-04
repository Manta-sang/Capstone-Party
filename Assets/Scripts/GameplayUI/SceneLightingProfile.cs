using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
// Scene-local lighting values captured from the existing game, not from the ceremony.
[DefaultExecutionOrder(-1000)]
public sealed class SceneLightingProfile : MonoBehaviour
{
    public AmbientMode ambientMode;
    public Color sky, equator, ground, fogColor;
    public float ambientIntensity, reflectionIntensity, fogDensity, fogStart, fogEnd;
    public bool fog;
    public FogMode fogMode;
    public Material skybox;
    public Light sun;
    void Awake(){Apply();}
    IEnumerator Start(){yield return null;Apply();DynamicGI.UpdateEnvironment();}
    public void Apply(){RenderSettings.ambientMode=ambientMode;RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;RenderSettings.ambientIntensity=ambientIntensity;RenderSettings.reflectionIntensity=reflectionIntensity;RenderSettings.skybox=skybox;RenderSettings.sun=sun;RenderSettings.fog=fog;RenderSettings.fogMode=fogMode;RenderSettings.fogColor=fogColor;RenderSettings.fogDensity=fogDensity;RenderSettings.fogStartDistance=fogStart;RenderSettings.fogEndDistance=fogEnd;}
}
