Shader "Hidden/AkiDevCat/AVL/DebugPass"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM

            #include "../Includes/AVLStructs.hlsl"
            
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 positionWS : TEXCOORD0;
                float2 texcoord   : TEXCOORD1;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            uint                                        _DebugMode;
            float                                       _DebugModeOpacity;
            float4                                      _ClusterSize;
            
            StructuredBuffer  <AVL_LightClusterData>    _LightClusterBuffer;
            StructuredBuffer  <AVL_LightData>           _GlobalLightBuffer;
            uint                                        _GlobalLightBufferSize;
            StructuredBuffer  <uint>                    _LightIndexBuffer;
            Texture2D<uint>                             _LightClusterDepthTexture;
            float4                                      _LightClusterDepthTexture_TexelSize;

            TEXTURE2D         (_CameraDepthTexture);
            TEXTURE2D         (_AVLFogTexture);
            SAMPLER           (sampler_CameraDepthTexture);

            float4x4 _CameraViewMatrix;

            float3 HUEtoRGB(in float H)
            {
                float R = abs(H * 6 - 3) - 1;
                float G = 2 - abs(H * 6 - 2);
                float B = 2 - abs(H * 6 - 4);
                return saturate(float3(R,G,B));
            }

            float CorrectDepth(float rawDepth)
            {
                float persp = LinearEyeDepth(rawDepth, _ZBufferParams);
                float ortho = (_ProjectionParams.z-_ProjectionParams.y)*(1-rawDepth)+_ProjectionParams.y;
                return lerp(persp,ortho,unity_OrthoParams.w);
            }

            Varyings vert (Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);
                float4 posWS = mul(UNITY_MATRIX_I_VP, float4(pos.xy, -1.0, 1.0));
                posWS.xyz /= posWS.w;

                #ifndef UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif

                output.positionCS = pos;
                output.positionWS = posWS;
                output.texcoord   = uv;

                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                
                uint2 idXY = uv * _ClusterSize.xy;
                uint id = idXY.x + idXY.y * _ClusterSize.x;
                AVL_LightClusterData cluster = _LightClusterBuffer[id];
                float hash = Hash(idXY.x + idXY.y * _ClusterSize.x);

                float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                float depth = CorrectDepth(rawDepth);

                float3 viewPlane = (input.positionWS.xyz - _WorldSpaceCameraPos) / dot(input.positionWS.xyz - _WorldSpaceCameraPos, unity_WorldToCamera._m20_m21_m22);

                depth *= -length(viewPlane);
                
                float3 rayOrigin = input.positionWS.xyz;
                float3 rayDir = normalize(input.positionWS.xyz - _WorldSpaceCameraPos);

                float3 wsPos = rayOrigin + rayDir * depth;

                float3 vsPos = mul(_CameraViewMatrix, float4(wsPos.xyz, 1)).xyz;
                vsPos.z = -vsPos.z;

                float4 result = 0;
                
                switch (_DebugMode)
                {
                default:
                case AVL_DEBUG_MODE_NONE:
                    return 0;
                    
                case AVL_DEBUG_MODE_LIGHT_CLUSTERS:
                    return (idXY.x + idXY.y) % 2 == 0 ? float4(1, 0, 1, _DebugModeOpacity) : float4(0, 1, 1, _DebugModeOpacity);

                case AVL_DEBUG_MODE_LIGHT_OVERDRAW:
                    result.rgb = saturate(cluster.LightCount / (float)MAX_LIGHT_PER_CLUSTER * _DebugModeOpacity + (1.0 - _DebugModeOpacity));
                    result.a = saturate(cluster.LightCount / (float)MAX_LIGHT_PER_CLUSTER * (1.0 - _DebugModeOpacity) + _DebugModeOpacity) * _DebugModeOpacity * _DebugModeOpacity;
                    return cluster.LightCount > 0 ? result : 0;

                case AVL_DEBUG_MODE_LIGHT_COUNT:
                    if (cluster.LightCount >= MAX_LIGHT_PER_CLUSTER)
                        return float4(1, 0, 1, saturate(_DebugModeOpacity * 4.0));
                    result.rgb = HUEtoRGB(1.0 / 3.0 - 1.0 / MAX_LIGHT_PER_CLUSTER + cluster.LightCount / (float)MAX_LIGHT_PER_CLUSTER);
                    return cluster.LightCount > 0 ? float4(result.rgb, _DebugModeOpacity) : 0;

                case AVL_DEBUG_MODE_VOLUMETRIC_LIGHT:
                    return SAMPLE_TEXTURE2D(_AVLFogTexture, sampler_PointClamp, uv);

                case AVL_DEBUG_MODE_CLUSTER_LIGHT_DEPTH:
                    float clusterDepth = _LightClusterDepthTexture.Load(int3(uv.x * _LightClusterDepthTexture_TexelSize.z, uv.y * _LightClusterDepthTexture_TexelSize.w, 0)) * 0.001f;
                    return float4(clusterDepth, clusterDepth, clusterDepth, 1.0f);
                }
                
                return 0;
            }
            ENDHLSL
        }
    }
}