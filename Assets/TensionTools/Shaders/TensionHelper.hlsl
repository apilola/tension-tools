
#ifndef TENSION_HELPERS_INCLUDED
#define TENSION_HELPERS_INCLUDED

StructuredBuffer<int> _EdgeBuffer;
StructuredBuffer<float3> _BaseEdgeBuffer;
ByteAddressBuffer _VertexBuffer;
int _VertexBufferStride;
int _VertexCount;
float _SquashIntensity;
float _SquashLimit;
float _StretchIntensity;
float _StretchLimit;
float3 _Scale;

float3 GetPosition(uint vertexID)
{
	return asfloat(_VertexBuffer.Load3((vertexID * _VertexBufferStride) << 2));// [vertexID * _VertexBufferStride] ;
}

float3 GetBaseEdge(uint edgeID)
{
	return _BaseEdgeBuffer[edgeID];
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
		float3 neighborBaseEdge = GetBaseEdge(i) * _Scale;
		float d0 = length(neighborBaseEdge);
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
	tension.r = min(tension.r * _SquashIntensity, _SquashLimit);
	tension.g = min(tension.g * _StretchIntensity, _StretchLimit);
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
