Shader "BlockFall/PastelMarble" {
 Properties { _Color("Base",Color)=(.4,.3,.5,1) _Tint("Highlight",Color)=(.7,.65,.8,1) _Scale("Vein Scale",Float)=1 }
 SubShader { Tags {"RenderType"="Opaque"} LOD 200
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows
 #pragma target 3.0
 fixed4 _Color,_Tint; float _Scale;
 struct Input { float3 worldPos; };
 void surf(Input IN,inout SurfaceOutputStandard o) {
  float3 p=IN.worldPos;
  float gradient=saturate(.5+p.z*.018+p.y*.06);
  float veins=pow(saturate(.5+.5*sin(p.x*.8+p.z*.45+sin(p.z*1.7)*.6)),18)*.1;
  o.Albedo=lerp(_Color.rgb,_Tint.rgb,gradient*.55+veins);
  o.Metallic=.12; o.Smoothness=.38; o.Alpha=1;
 }
 ENDCG
 } FallBack "Standard"
}
