// Unlit transparent shader for stylised particles.
//
// It exists because HDRP ships nothing usable for a ParticleSystem: there is not
// a single particle shader in its runtime, its own HDRP/Unlit never reads vertex
// colour - so Colour over Lifetime does nothing - and the legacy Particles/*
// shaders render magenta under HDRP.
//
// Built on HDRP's own Unlit material framework rather than as a standalone pass,
// so exposure, debug modes and the render queue keep behaving like every other
// material in the project. Only the surface function is ours.
Shader "VFX/Cartoon Particle"
{
    Properties
    {
        [MainTexture] _BaseMap("Sprite", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)

        // Anything above 1 pushes the colour past what a display can show, which
        // is what the bloom pass looks for. That is the only way to glow here:
        // the particle tint arrives as a vertex colour, and a vertex colour is
        // a byte per channel, so it can never exceed 1 on its own.
        [Tooltip(Multiplier on the output colour. 1 is the plain sprite. Above 1 the sprite is over bright and blooms.)]
        _Brightness("Brightness", Range(0.0, 8.0)) = 1.0

        // A pixel is drawn or it is not. Anything under the cutoff is discarded,
        // so the sprite keeps the hard edge it was drawn with.
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [Tooltip(Distance over which the sprite is eaten away as it approaches solid geometry. Only meaningful when blending. 0 disables it.)]
        _DepthFade("Depth Fade Distance", Float) = 0.0

        [HideInInspector] _AlphaCutoffEnable("Alpha Cutoff Enable", Float) = 1.0
        // Unused while the shader is cut out, kept declared because HDRP's shared
        // transparent path reads it. 0 = Alpha, 1 = Additive, 4 = Premultiply.
        [HideInInspector] _BlendMode("Blend Mode", Float) = 0.0
        [HideInInspector] _CullMode("Cull Mode", Float) = 0.0
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    // Always cut out, never blended. Hard defined rather than left to the
    // material keyword list, which is how a material ends up silently rendering
    // through a path the author never chose.
    #define _ALPHATEST_ON

    // _SURFACE_TYPE_TRANSPARENT is deliberately NOT defined. With it, HDRP's
    // ApplyBlendMode multiplies the colour by the opacity inside the shader, so a
    // particle would dim as its alpha dropped even with blending switched off.
    // The opaque branch leaves the colour alone, which is the whole point here.

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"

    #include "CartoonParticleProperties.hlsl"

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "HDUnlitShader" "Queue" = "AlphaTest" }

        // Feeds the depth prepass. Without it the particles are absent from the
        // depth buffer, and since HDRP depth tests opaque geometry with Equal
        // against that prepass, anything the smoke overlapped would either
        // vanish or win at random depending on draw order. Sorting alone cannot
        // fix that: the opaque queue sorts front to back, which is the wrong way
        // round for sprites that resolve overlap by whoever draws last.
        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode" = "DepthForwardOnly" }

            Cull [_CullMode]
            ZWrite On

            HLSLPROGRAM

            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ WRITE_MSAA_DEPTH

            #pragma vertex Vert
            #pragma fragment Frag

            #define SHADERPASS SHADERPASS_DEPTH_ONLY

            #define ATTRIBUTES_NEED_COLOR
            #define VARYINGS_NEED_COLOR

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/ShaderPass/UnlitDepthPass.hlsl"

            #include "CartoonParticleData.hlsl"

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            // No blending at all: a surviving pixel is written at full strength.
            Blend Off
            ZWrite On
            // LEqual rather than Equal, so the pass still draws correctly if the
            // prepass is ever skipped for this material.
            ZTest LEqual
            Cull [_CullMode]

            HLSLPROGRAM

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY

            #pragma vertex Vert
            #pragma fragment Frag

            #define SHADERPASS SHADERPASS_FORWARD_UNLIT

            // Both have to be defined before the shared pass builds the Attributes
            // and Varyings structs. Define them after and the structs are already
            // stamped out without a colour channel, so the particle's tint never
            // reaches the fragment and the smoke never fades.
            #define ATTRIBUTES_NEED_COLOR
            #define VARYINGS_NEED_COLOR

            #ifdef DEBUG_DISPLAY
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.hlsl"
            #endif

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/ShaderPass/UnlitSharePass.hlsl"

            #include "CartoonParticleData.hlsl"

            #include_with_pragmas "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForwardUnlit.hlsl"

            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
