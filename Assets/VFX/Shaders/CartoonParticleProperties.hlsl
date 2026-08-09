#ifndef CARTOON_PARTICLE_PROPERTIES_INCLUDED
#define CARTOON_PARTICLE_PROPERTIES_INCLUDED

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

// Everything non-texture has to live in this one buffer under this exact name or
// the SRP batcher silently drops the material and every particle draws on its own.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseColor;
float _Brightness;
// Read by HDRP's own ApplyBlendMode in Material.hlsl, not by anything here. It
// has to exist or the shared transparent path fails to compile.
float _BlendMode;
float _DepthFade;
float _AlphaCutoff;
float _AlphaCutoffEnable;
float _SrcBlend;
float _DstBlend;
float _CullMode;
CBUFFER_END

#endif
