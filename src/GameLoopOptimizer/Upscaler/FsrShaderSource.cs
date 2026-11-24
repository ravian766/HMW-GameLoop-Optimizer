namespace GameLoopOptimizer.Upscaler;

/// <summary>
/// Embedded HLSL shader sources for FSR 1.0 spatial upscaling.
/// 
/// The pipeline uses two passes:
/// 1. EASU (Edge-Adaptive Spatial Upsampling) — upscales from input to output resolution
///    using a 12-tap filter with edge-direction-aware sharpening.
/// 2. RCAS (Robust Contrast-Adaptive Sharpening) — applies configurable sharpening
///    to the upscaled output with contrast-clamped adaptive filtering.
/// 
/// Both are implemented as pixel shaders operating on a fullscreen triangle for maximum
/// efficiency (no vertex buffer needed, single draw call per pass).
/// </summary>
public static class FsrShaderSource
{
    /// <summary>
    /// Simple fullscreen triangle vertex shader.
    /// Generates a triangle that covers the entire screen from vertex ID alone.
    /// </summary>
    public const string FullscreenVS = @"
struct VS_OUTPUT
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

VS_OUTPUT main(uint vertexId : SV_VertexID)
{
    VS_OUTPUT output;
    // Generate fullscreen triangle from vertex ID
    output.TexCoord = float2((vertexId << 1) & 2, vertexId & 2);
    output.Position = float4(output.TexCoord * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    return output;
}
";

    /// <summary>
    /// FSR 1.0 EASU (Edge-Adaptive Spatial Upsampling) pixel shader.
    /// 
    /// Simplified but faithful implementation of AMD's EASU algorithm:
    /// - Samples a 4×4 neighborhood from the low-res source
    /// - Estimates local edge direction and strength
    /// - Applies a directional 12-tap filter to produce edge-aware upsampled output
    /// - Avoids ringing artifacts through clamped Lanczos-like kernel
    /// </summary>
    public const string EasuPS = @"
cbuffer EasuConstants : register(b0)
{
    float4 Const0; // x=inputW, y=inputH, z=1/inputW, w=1/inputH
    float4 Const1; // x=outputW, y=outputH, z=1/outputW, w=1/outputH
    float4 Const2; // x=inputW/outputW, y=inputH/outputH, z=0.5*inputW/outputW, w=0.5*inputH/outputH
    float4 Const3; // Reserved
};

Texture2D InputTexture : register(t0);
SamplerState LinearSampler : register(s0);

// Lanczos2 approximation kernel
float Lanczos2(float x)
{
    float x2 = x * x;
    // Fast approximation of Lanczos2
    // (25/16 * (2/5 * x^2 - 1))^2 * (25/16 * (1/2 * x^2 - 1) + 1)
    float a = saturate(1.0 - x2 * (1.0 / 4.0));
    return a * a;
}

struct VS_OUTPUT
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

float4 main(VS_OUTPUT input) : SV_Target
{
    float2 outputSize = Const1.xy;
    float2 inputSize = Const0.xy;
    float2 rcpInputSize = Const0.zw;
    
    // Map output pixel to input space
    float2 srcPos = input.TexCoord * inputSize;
    float2 srcCenter = floor(srcPos - 0.5) + 0.5;
    float2 fracCoord = srcPos - srcCenter;
    
    // Sample 4x4 neighborhood (12 taps for EASU, skip corners)
    //  . b c .
    //  e f g h
    //  i j k l
    //  . n o .
    float2 tc = srcCenter * rcpInputSize;
    
    float3 b = InputTexture.SampleLevel(LinearSampler, tc + float2( 0, -1) * rcpInputSize, 0).rgb;
    float3 c = InputTexture.SampleLevel(LinearSampler, tc + float2( 1, -1) * rcpInputSize, 0).rgb;
    float3 e = InputTexture.SampleLevel(LinearSampler, tc + float2(-1,  0) * rcpInputSize, 0).rgb;
    float3 f = InputTexture.SampleLevel(LinearSampler, tc + float2( 0,  0) * rcpInputSize, 0).rgb;
    float3 g = InputTexture.SampleLevel(LinearSampler, tc + float2( 1,  0) * rcpInputSize, 0).rgb;
    float3 h = InputTexture.SampleLevel(LinearSampler, tc + float2( 2,  0) * rcpInputSize, 0).rgb;
    float3 i = InputTexture.SampleLevel(LinearSampler, tc + float2(-1,  1) * rcpInputSize, 0).rgb;
    float3 j = InputTexture.SampleLevel(LinearSampler, tc + float2( 0,  1) * rcpInputSize, 0).rgb;
    float3 k = InputTexture.SampleLevel(LinearSampler, tc + float2( 1,  1) * rcpInputSize, 0).rgb;
    float3 l = InputTexture.SampleLevel(LinearSampler, tc + float2( 2,  1) * rcpInputSize, 0).rgb;
    float3 n = InputTexture.SampleLevel(LinearSampler, tc + float2( 0,  2) * rcpInputSize, 0).rgb;
    float3 o = InputTexture.SampleLevel(LinearSampler, tc + float2( 1,  2) * rcpInputSize, 0).rgb;
    
    // Luma for edge detection (perceptual: G-channel dominated)
    float lumB = dot(b, float3(0.299, 0.587, 0.114));
    float lumC = dot(c, float3(0.299, 0.587, 0.114));
    float lumE = dot(e, float3(0.299, 0.587, 0.114));
    float lumF = dot(f, float3(0.299, 0.587, 0.114));
    float lumG = dot(g, float3(0.299, 0.587, 0.114));
    float lumH = dot(h, float3(0.299, 0.587, 0.114));
    float lumI = dot(i, float3(0.299, 0.587, 0.114));
    float lumJ = dot(j, float3(0.299, 0.587, 0.114));
    float lumK = dot(k, float3(0.299, 0.587, 0.114));
    float lumL = dot(l, float3(0.299, 0.587, 0.114));
    float lumN = dot(n, float3(0.299, 0.587, 0.114));
    float lumO = dot(o, float3(0.299, 0.587, 0.114));
    
    // Edge detection: estimate gradient direction using Sobel-like kernel
    float edgeH = abs(lumE - lumF) + abs(lumF - lumG) + abs(lumG - lumH)
                + abs(lumI - lumJ) + abs(lumJ - lumK) + abs(lumK - lumL);
    float edgeV = abs(lumB - lumF) + abs(lumF - lumJ) + abs(lumJ - lumN)
                + abs(lumC - lumG) + abs(lumG - lumK) + abs(lumK - lumO);
    
    // Direction weights: larger = more horizontal edge, more vertical filtering
    float edgeTotal = edgeH + edgeV;
    float dirH = (edgeTotal > 0.001) ? edgeV / edgeTotal : 0.5;
    float dirV = (edgeTotal > 0.001) ? edgeH / edgeTotal : 0.5;
    
    // Stretch factor along edge direction (1.0 = isotropic, >1.0 = stretched)
    float stretch = max(dirH, dirV) / (min(dirH, dirV) + 0.001);
    stretch = min(stretch, 4.0); // Clamp to prevent extreme stretching
    
    // Weight calculation using Lanczos2-like kernel, biased by edge direction
    float2 stretchDir = float2(dirV, dirH); // Perpendicular to edge
    stretchDir = stretchDir / (length(stretchDir) + 0.001);
    
    // Compute weights for each tap based on distance, edge-adapted
    float wB = Lanczos2(length(float2(0, -1) - fracCoord) * lerp(1.0, 1.0/max(stretch, 1.0), abs(stretchDir.y)));
    float wC = Lanczos2(length(float2(1, -1) - fracCoord) * lerp(1.0, 1.0/max(stretch, 1.0), abs(stretchDir.y)));
    float wE = Lanczos2(length(float2(-1, 0) - fracCoord) * lerp(1.0, 1.0/max(stretch, 1.0), abs(stretchDir.x)));
    float wF = Lanczos2(length(float2(0,  0) - fracCoord));
    float wG = Lanczos2(length(float2(1,  0) - fracCoord));
    float wH = Lanczos2(length(float2(2,  0) - fracCoord) * lerp(1.0, 1.0/max(stretch, 1.0), abs(stretchDir.x)));
    float wI = Lanczos2(length(float2(-1, 1) - fracCoord) * lerp(1.0, 1.0/max(stretch, 1.0), abs(stretchDir.x)));
    float wJ = Lanczos2(length(float2(0,  1) - fracCoord));
    float wK = Lanczos2(length(float2(1,  1) - fracCoord));
    float wL = Lanczos2(length(float2(2,  1) - fracCoord) * lerp(1.0, 1.0/max(stretch, 1.0), abs(stretchDir.x)));
    float wN = Lanczos2(length(float2(0,  2) - fracCoord) * lerp(1.0, 1.0/max(stretch, 1.0), abs(stretchDir.y)));
    float wO = Lanczos2(length(float2(1,  2) - fracCoord) * lerp(1.0, 1.0/max(stretch, 1.0), abs(stretchDir.y)));
    
    float wSum = wB + wC + wE + wF + wG + wH + wI + wJ + wK + wL + wN + wO;
    
    // Anti-ringing clamp: determine min/max from the nearest 4 taps
    float3 minColor = min(min(f, g), min(j, k));
    float3 maxColor = max(max(f, g), max(j, k));
    
    float3 result = (b*wB + c*wC + e*wE + f*wF + g*wG + h*wH +
                     i*wI + j*wJ + k*wK + l*wL + n*wN + o*wO) / wSum;
    
    // Clamp to prevent ringing
    result = clamp(result, minColor, maxColor);
    
    return float4(result, 1.0);
}
";

    /// <summary>
    /// FSR 1.0 RCAS (Robust Contrast-Adaptive Sharpening) pixel shader.
    /// 
    /// Applies a contrast-adaptive sharpening filter:
    /// - Uses a diamond-shaped 5-tap pattern (N,S,E,W + center)
    /// - Computes local contrast (min/max luma)
    /// - Adapts sharpening strength based on contrast to avoid amplifying noise
    /// - Clamps output to prevent ringing artifacts
    /// </summary>
    public const string RcasPS = @"
cbuffer RcasConstants : register(b0)
{
    float4 RcasConst; // x=sharpness (0=max sharpening, 1=no sharpening), y=rcpOutputW, z=rcpOutputH, w=unused
};

Texture2D InputTexture : register(t0);
SamplerState PointSampler : register(s0);

struct VS_OUTPUT
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

float4 main(VS_OUTPUT input) : SV_Target
{
    float2 rcpSize = RcasConst.yz;
    float2 uv = input.TexCoord;
    
    // 5-tap diamond pattern
    float3 n = InputTexture.SampleLevel(PointSampler, uv + float2( 0, -1) * rcpSize, 0).rgb;
    float3 s = InputTexture.SampleLevel(PointSampler, uv + float2( 0,  1) * rcpSize, 0).rgb;
    float3 e = InputTexture.SampleLevel(PointSampler, uv + float2( 1,  0) * rcpSize, 0).rgb;
    float3 w = InputTexture.SampleLevel(PointSampler, uv + float2(-1,  0) * rcpSize, 0).rgb;
    float3 m = InputTexture.SampleLevel(PointSampler, uv, 0).rgb;
    
    // Luma
    float lumN = dot(n, float3(0.299, 0.587, 0.114));
    float lumS = dot(s, float3(0.299, 0.587, 0.114));
    float lumE = dot(e, float3(0.299, 0.587, 0.114));
    float lumW = dot(w, float3(0.299, 0.587, 0.114));
    float lumM = dot(m, float3(0.299, 0.587, 0.114));
    
    // Min/max luma for contrast detection
    float lumMin = min(lumM, min(min(lumN, lumS), min(lumE, lumW)));
    float lumMax = max(lumM, max(max(lumN, lumS), max(lumE, lumW)));
    
    // Local contrast ratio
    float contrast = lumMax - lumMin;
    
    // Adaptive sharpening weight based on contrast
    // Higher contrast -> less sharpening (avoid ringing)
    // Lower contrast -> more sharpening (enhance edges)
    float sharpnessParam = RcasConst.x; // 0 = max sharp, 1 = no sharp
    float peak = -1.0 / lerp(8.0, 5.0, saturate(sharpnessParam));
    
    // Reduce sharpening in high-contrast areas to prevent ringing
    float contrastFade = saturate(1.0 - contrast * 4.0);
    float w_sharp = peak * contrastFade;
    
    // Apply sharpening
    float3 result = (n * w_sharp + s * w_sharp + e * w_sharp + w * w_sharp + m) / (1.0 + 4.0 * w_sharp);
    
    // Anti-ringing: clamp to neighborhood min/max
    float3 minColor = min(m, min(min(n, s), min(e, w)));
    float3 maxColor = max(m, max(max(n, s), max(e, w)));
    result = clamp(result, minColor, maxColor);
    
    return float4(saturate(result), 1.0);
}
";

    /// <summary>
    /// Simple passthrough pixel shader for the final compositing step.
    /// Copies the FSR output to the overlay render target.
    /// </summary>
    public const string PassthroughPS = @"
Texture2D InputTexture : register(t0);
SamplerState LinearSampler : register(s0);

struct VS_OUTPUT
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

float4 main(VS_OUTPUT input) : SV_Target
{
    return InputTexture.Sample(LinearSampler, input.TexCoord);
}
";
}
