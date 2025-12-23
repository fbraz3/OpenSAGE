#version 450
#extension GL_GOOGLE_include_directive : enable

#include "Common.h"
#include "Mesh.h"

// Dummy pass constants layout to satisfy Metal backend requirements
// Metal requires all resource sets defined in the pipeline to be bound
// The depth shader doesn't actually use any pass data
layout(set = PASS_CONSTANTS_RESOURCE_SET, binding = 0) uniform DummyPassConstantsVS
{
    vec4 DummyPass0;
};

layout(set = PASS_CONSTANTS_RESOURCE_SET, binding = 1) uniform DummyPassConstantsPS
{
    vec4 DummyPass1;
};

// Dummy material constants layout to satisfy Metal backend requirements
// Metal requires all resource sets defined in the pipeline to be bound
// The depth shader doesn't actually use any material data
layout(set = MATERIAL_CONSTANTS_RESOURCE_SET, binding = 0) uniform MaterialConstants
{
    vec4 Dummy;
};

layout(set = MATERIAL_CONSTANTS_RESOURCE_SET, binding = 1) uniform texture2D DummyTexture0;
layout(set = MATERIAL_CONSTANTS_RESOURCE_SET, binding = 2) uniform texture2D DummyTexture1;
layout(set = MATERIAL_CONSTANTS_RESOURCE_SET, binding = 3) uniform sampler DummySampler;

void main()
{
    // TODO: Alpha testing
}