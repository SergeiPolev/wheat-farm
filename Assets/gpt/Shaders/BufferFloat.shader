#ifndef _PlantBuffer
#define _PlantBuffer

StructuredBuffer<float4> _PlantBuffer;

float4 SampleFromBuffer(uint index)
{
    return _PlantBuffer[index];
}

#endif
