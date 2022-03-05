Shader "Unlit/CameraScreen"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_WhichEye("Which Eye", int) = 1
	}

	SubShader
	{
	    Tags {"Queue" = "Overlay"}
	    ZWrite On ZTest Always Cull Off
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			uniform float _WhichEye;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

            sampler2D _MainTex;

            bool isVR() {
				// USING_STEREO_MATRICES
				#if UNITY_SINGLE_PASS_STEREO
					return true;
				#else
					return false;
				#endif
			}

			v2f vert(float2 uv : TEXCOORD0)
			{
				v2f o = (v2f)0;
				o.pos = float4((uv.x*2.0 - 1.0), -(uv.y*2.0 - 1.0), 0.0, 1.0);
				o.uv = uv;
				//checking random spots on the screen to see if there is actual data in the render texture and not just black
				float4 hasData = tex2Dlod(_MainTex, float4(.5,.5,0,0)) +
                        tex2Dlod(_MainTex, float4(.51,.4,0,0)) + tex2Dlod(_MainTex, float4(.4,.51,0,0)) +
                        tex2Dlod(_MainTex, float4(.61,.61,0,0)) + tex2Dlod(_MainTex, float4(.3,.71,0,0));
                if (all(hasData == 0)) {
                    o.pos = float4(1,1,1,1);
                }
                if (unity_StereoEyeIndex != _WhichEye) { o.pos = float4(1,1,1,1); }//send the verts to gay baby jail in the corner of the screen if its rendering in the wrong eye
				return o;
			}

			float4 frag(v2f i) : SV_Target//the i here means input UV coordinate
			{
				if (!isVR()) {//here is the "is in VR" check
					clip(-1);//I think clip just stops rendering this fragment?
					}
				float4 color = tex2D((_MainTex), i.uv);
				return color;
			}
			ENDCG
		}
	}
}