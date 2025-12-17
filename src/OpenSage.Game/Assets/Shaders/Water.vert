#version 450
#extension GL_GOOGLE_include_directive : enable

#include "Common.h"
#include "ForwardPass.h"

layout(location = 0) in vec3 in_Position;

layout(location = 0) out vec3 out_WorldPosition;
layout(location = 1) out vec2 out_CloudUV;
layout(location = 2) out float out_ViewSpaceDepth;

// Wave animation constants - using a dedicated set to avoid conflicts
// with other pass constants (lighting, shadows, clouds, decals)
layout(set = WATER_ANIMATION_CONSTANTS_RESOURCE_SET, binding = 0) uniform WaveAnimationConstants
{
    vec4 ActiveWaves[32];  // x, y = position, z = width, w = alpha (up to 32 waves)
    uint WaveCount;
    float Time;
    float _pad0;
    float _pad1;
};

void main()
{
    vec3 worldPosition = in_Position;
    
    // Apply wave deformations based on active waves
    // Each wave creates a displacement based on distance to wave center
    for (uint i = 0; i < min(WaveCount, 32u); i++)
    {
        vec4 wave = ActiveWaves[i];
        vec2 waveCenter = wave.xy;
        float waveRadius = wave.z;
        float waveAlpha = wave.w;
        
        // Calculate distance from this vertex to the wave center
        vec2 toWave = worldPosition.xy - waveCenter;
        float distanceToWave = length(toWave);
        
        // Create wave displacement using a smooth falloff
        // Wave height is strongest at the edge (radius) and fades out
        float waveWidth = waveRadius * 0.2;  // Width of the wave band
        float distFromEdge = abs(distanceToWave - waveRadius);
        
        // Gaussian-like falloff from wave edge
        float waveStrength = exp(-(distFromEdge * distFromEdge) / (waveWidth * waveWidth));
        waveStrength *= waveAlpha;  // Fade out as wave dies
        
        // Displace vertex upward at wave location
        float waveHeight = 0.5 * waveStrength;
        worldPosition.z += waveHeight;
        
        // Optional: Add some horizontal displacement towards/away from wave center
        if (distanceToWave > 0.01)
        {
            vec2 waveDirection = normalize(toWave);
            float horizontalDisplacement = 0.1 * waveStrength;
            worldPosition.xy += waveDirection * horizontalDisplacement;
        }
    }

    gl_Position = _GlobalConstants.ViewProjection * vec4(worldPosition, 1);
    out_WorldPosition = worldPosition;

    out_CloudUV = GetCloudUV(out_WorldPosition);

    out_ViewSpaceDepth = gl_Position.z;
}