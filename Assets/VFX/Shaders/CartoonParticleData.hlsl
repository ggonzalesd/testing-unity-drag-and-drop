#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/SampleUVMapping.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl"

// Fades the sprite out as it approaches whatever is already in the depth buffer.
// HDRP has no soft particle support of its own - there is no _SoftParticle
// anywhere in its runtime - so without this every billboard cuts the floor along
// a hard straight line the moment it touches it, which is what gives cheap smoke
// away instantly.
//
// The sky writes a far depth, so particles against it fade by nothing.
float ParticleDepthFade(PositionInputs posInput)
{
    if (_DepthFade <= 0.0) return 1.0;

    float sceneDepth = LinearEyeDepth(LoadCameraDepth(posInput.positionSS), _ZBufferParams);

    return saturate((sceneDepth - posInput.linearDepth) / _DepthFade);
}

void GetSurfaceAndBuiltinData(FragInputs input, float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData RAY_TRACING_OPTIONAL_PARAMETERS)
{
    float2 uv = TRANSFORM_TEX(input.texCoord0.xy, _BaseMap);
    float4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);

    // The particle system writes its start colour and its Colour over Lifetime
    // into the vertex colour, so the entire fade in and fade out lives here.
    // HDRP's own Unlit never reads it, which is the whole reason this shader
    // exists rather than a stock one.
    float4 tint = texel * _BaseColor * input.color;

    // Colour only. Multiplying the alpha as well would move the cut out edge
    // every time the brightness changed, so a sprite turned up to glow would
    // also quietly get fatter.
    surfaceData.color = tint.rgb * _Brightness;
    // The pass can be asked to export a geometric normal, so it cannot be left
    // uninitialised even though nothing here is lit.
    surfaceData.normalWS = 0.0;

    float alpha = tint.a * ParticleDepthFade(posInput);

    ZERO_BUILTIN_INITIALIZE(builtinData); // No lighting, so no InitBuiltinData call.
    builtinData.opacity = alpha;
    builtinData.emissiveColor = 0.0;

#ifdef _ALPHATEST_ON
    clip(alpha - _AlphaCutoff);
    builtinData.alphaClipTreshold = _AlphaCutoff;
#endif

#if defined(DEBUG_DISPLAY)
    builtinData.renderingLayers = GetMeshRenderingLayerMask();
#endif

    ApplyDebugToBuiltinData(builtinData);

    RAY_TRACING_OPTIONAL_ALPHA_TEST_PASS
}
