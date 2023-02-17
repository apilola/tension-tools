
#ifndef TENSION_HELPERS_INCLUDED
#define TENSION_HELPERS_INCLUDED

StructuredBuffer<int> _EdgeBuffer;
StructuredBuffer<float3> _RawEdgeDeltaBuffer;
ByteAddressBuffer _VertexBuffer;
int _VertexBufferStride;
int _VertexCount;
float _SquashIntensity;
float _SquashLimit;
float _SquashPower;
float _StretchIntensity;
float _StretchLimit;
float _StretchPower;
float3 _Scale;

float3 GetPosition(uint vertexID)
{
	return asfloat(_VertexBuffer.Load3((vertexID * _VertexBufferStride) << 2));// [vertexID * _VertexBufferStride] ;
}

float3 GetRawEdgeDelta(uint edgeID)
{
	return _RawEdgeDeltaBuffer[edgeID];
}

int2 GetRange(uint vertexID)
{
	int2 range;
	range.y = _EdgeBuffer[vertexID];
	if (vertexID == 0)
		range.x = 0;
	else
		range.x = _EdgeBuffer[vertexID - 1];
	return range;
}

void SampleTension(uint vertexID, float3 position, out float2 tension)
{
	int2 range = GetRange(vertexID);
	tension = float2(0, 0);
	for (int i = range.x; i < range.y; i++)
	{
		float3 rawEdgeDelta = GetRawEdgeDelta(i) * _Scale;
		float d0 = length(rawEdgeDelta);
		float3 neighborPos = GetPosition(_EdgeBuffer[i + _VertexCount]);
		float d1 = length((neighborPos - position));
		float delta = (d0 - d1);
		if (delta > 0)
		{
			tension.r += abs(delta) / d0;
		}
		else
		{
		    tension.g += (abs(delta) / d0);
		}
	}
	tension.r = min(pow(tension.r, _SquashPower) * _SquashIntensity, _SquashLimit);
	tension.g = min(pow(tension.g, _StretchPower) * _StretchIntensity, _StretchLimit);
}

void SampleTension_float(uint vertexID, float3 position, out float2 tension)
{
	SampleTension(vertexID, position, tension);
}

void SampleTension_half(uint vertexID, float3 position, out float2 tension)
{
	SampleTension(vertexID, position, tension);
}

#endif
