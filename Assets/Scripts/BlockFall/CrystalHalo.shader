Shader "BlockFall/CrystalHalo" {
 Properties { _Color("Glow",Color)=(.12,.5,1,.2) }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha One ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 fixed4 _Color;
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o;}
 fixed4 frag(v2f i):SV_Target {float r=length(i.uv-.5)*2;return fixed4(_Color.rgb,_Color.a*pow(saturate(1-r),2.5));}
 ENDCG }
 }
}
