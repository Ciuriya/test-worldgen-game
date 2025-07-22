Shader "Custom/Tiling2D_URP"
{
    Properties
    {
        _Color("Tint", Color) = (1,1,1,1)
        _MainTex("Sprite Atlas", 2D) = "white" {}
        _IndexTex("Index Map", 2D) = "white" {}
        _AtlasDims("Atlas Dims (cols, rows)", Vector) = (8,8,0,0)
        _IndexTex_TexelSize("Index Map Texel Size (1/w, 1/h)", Vector) = (0,0,0,0)
        _UV("UV Set (TEXCOORD0, TEXCOORD1, etc.)", Float) = 0
        _Cull("Cull Mode", Float) = 2 // 0: off 1: front 2: back
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 100
        Cull [_Cull]

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MainTex_ST;
                float4 _AtlasDims; // xy = cols, rows
                float4 _IndexTex_TexelSize;
                float _UV; // which UV set to use
            CBUFFER_END

            Texture2D<float4> _MainTex;
            SamplerState sampler_MainTex;

            Texture2D<float4> _IndexTex;
            SamplerState sampler_IndexTex;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 uv2         : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                // UVs are already scaled in the mesh so that 1 UV unit == 1 tile cell
                OUT.uv = TRANSFORM_TEX(_UV == 0.0 ? IN.uv : IN.uv2, _MainTex);
                OUT.uv2 = IN.uv2;

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = _UV == 0.0 ? IN.uv : IN.uv2;
                float2 cell = floor(IN.uv);  // integer cell coordinate
                float2 localUV = frac(IN.uv); // 0-1 inside the cell

                // convert cell coord -> lookup into index texture (nearest filtering)
                float2 indexUV = frac((cell + 0.5) * _IndexTex_TexelSize.xy);
                
                // the index is stored in red and green channels (0-1 range). 
                // assume 0-255 mapping
                half4 channels = _IndexTex.Sample(sampler_IndexTex, indexUV);
                float tileIndex = round(channels.r * 255.0 + channels.g * 255.0 * 256.0);

                if (tileIndex == 0) return _Color;

                float cols = _AtlasDims.x;
                float rows = _AtlasDims.y;

                float row = floor(tileIndex / cols);
                float col = tileIndex - (cols * row);

                // atlas starts in the top-left, but UV goes from bottom to top
                // so we flip y to fix that
                float2 atlasUV = (float2(col, rows - 1.0 - row) + localUV) / _AtlasDims.xy;

                return _MainTex.Sample(sampler_MainTex, atlasUV) * _Color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
} 