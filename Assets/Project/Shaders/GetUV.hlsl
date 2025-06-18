#ifndef MY_CUSTOM_FUNCTION_INCLUDED
#define MY_CUSTOM_FUNCTION_INCLUDED
#pragma instance
#pragma instancing_options assumeuniformscaling

float4 _UVFrozen[1089];

void GetUV_float(float instanceID, out float4 newUV)
{
    uint id = (uint)instanceID;
    newUV = _UVFrozen[id];
}

#endif