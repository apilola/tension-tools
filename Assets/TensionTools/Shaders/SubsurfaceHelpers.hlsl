
#ifndef SUBSURFACE_HELPERS_INCLUDED
#define SUBSURFACE_HELPERS_INCLUDED

void GetSubsurfaceScatteringLight(bool lightExists, float3 lightColor, float3 lightDirection, float3 normalDirection, float3 viewDirection,
	float attenuation, float3 thickness, float3 indirectLight, float3 subsurfaceColour, float distortion, float power, float intensity, float ambient, out float3 light)
{
	float3 vLTLight = lightDirection + normalDirection * distortion; // Distortion
	float3 fLTDot = pow(saturate(dot(viewDirection, -vLTLight)), power)
		* intensity * 1.0 / 3.14159265359f;

	light =  lerp(1, attenuation, float(lightExists))
		* (fLTDot + ambient) * thickness
		* (lightColor + indirectLight) * subsurfaceColour;
}

void GetSubsurfaceScatteringLight_float(bool lightExists, float3 lightColor, float3 lightDirection, float3 normalDirection, float3 viewDirection,
	float attenuation, float3 thickness, float3 indirectLight, float3 subsurfaceColour, float distortion, float power, float intensity, float ambient, out float3 light)
{
	float3 vLTLight = lightDirection + normalDirection * distortion; // Distortion
	float3 fLTDot = pow(saturate(dot(viewDirection, -vLTLight)), power)
		* intensity * 1.0 / 3.14159265359f;

	light = lerp(1, attenuation, float(lightExists))
		* (fLTDot + ambient) * thickness
		* (lightColor + indirectLight) * subsurfaceColour;
}

void GetSubsurfaceScatteringLight_half(bool lightExists, float3 lightColor, float3 lightDirection, float3 normalDirection, float3 viewDirection,
	float attenuation, float3 thickness, float3 indirectLight, float3 subsurfaceColour, float distortion, float power, float intensity, float ambient, out half3 light)
{
	float3 vLTLight = lightDirection + normalDirection * distortion; // Distortion
	float3 fLTDot = pow(saturate(dot(viewDirection, -vLTLight)), power)
		* intensity * 1.0 / 3.14159265359f;

	light = lerp(1, attenuation, float(lightExists))
		* (fLTDot + ambient) * thickness
		* (lightColor + indirectLight) * subsurfaceColour;
}
#endif
