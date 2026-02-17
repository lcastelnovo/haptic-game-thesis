Shader "Outlined/Hologram" {
	Properties{
		_RimColor("Rim Color", Color) = (0,0.5,0.5,0.0)
		_RimPower("Rim Power", Range(0.5, 8.0)) = 3.0
		_PulseFrequency("Pulse Frequency", Range(0, 100)) = 4
		_VerticalOffeset("Vertical Offset", Range(0, 100)) = 100
	}
	SubShader{

		Tags {"Queue" = "Transparent"}

		Pass {
			ZWrite On       // Fixes z-ordering of interior parts of zombunny coming through, by writing to z-buffer
			ColorMask 0     // do not write to the frame buffer
		}

		CGPROGRAM
		#pragma surface surf Lambert alpha:fade vertex:vert
		struct Input {
			float3 viewDir;
			float3 vertex;
		};

		 
		float4 _RimColor;
		float _RimPower;
		float _PulseFrequency;
		float _VerticalOffeset;
		
		void vert(inout appdata_full v, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input, o);

			o.vertex = v.vertex.xyz;
			o.vertex += 3;
		}

		void surf(Input IN, inout SurfaceOutput o) {
			half rim = 1 - saturate(dot(normalize(IN.viewDir), o.Normal));
			o.Emission = _RimColor.rgb * pow(rim, _RimPower) * 10;
			float alpha = pow(rim, _RimPower);

			o.Alpha = (sin(_Time.y * _PulseFrequency - IN.vertex.y * _VerticalOffeset) * alpha * 0.3) + alpha * 0.7;
			// o.color.a = alpha;
		}
		ENDCG
	}
	FallBack "Diffuse"
}