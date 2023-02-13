
#ifndef COLOR_PACKING_HELPERS_INCLUDED
#define COLOR_PACKING_HELPERS_INCLUDED

float GetRed(uint packedCol)
{
    return float((packedCol & 0x000000FFu)) / 255.0f;
}

float GetGreen(uint packedCol)
{
    return float((packedCol & 0x0000FF00u) >> 8) / 255.0f;
}

float GetBlue(uint packedCol)
{
    return float((packedCol & 0x00FF0000u) >> 16) / 255.0f;
}

float GetAlpha(uint packedCol)
{
    return float((packedCol & 0xFF000000u) >> 24) / 255.0f;
}

float4 UnpackColor(uint packedCol)
{
    return float4(GetRed(packedCol), GetGreen(packedCol), GetBlue(packedCol), GetAlpha(packedCol));
}

uint PackColor(float4 color)
{
    uint packedR = uint(color.r * 255);
    uint packedG = uint(color.g * 255) << 8; // shift bits over 8 places
    uint packedB = uint(color.b * 255) << 16; // shift bits over 16 places
    uint packedA = uint(color.a * 255) << 24; // shift bits over 24 places
    return packedR + packedG + packedB + packedA;
}

min16uint2 UIntToUShort2(uint uInt)
{
    return min16uint2((min16uint)(uInt >> 16), (min16uint) (uInt & 0x0000FFFFuL));
}

min16uint4 UInt2ToUShort4(uint2 uInt2)
{
    min16uint2 xy = UIntToUShort2(uInt2.x);
    min16uint2 zw = UIntToUShort2(uInt2.y);
    return min16uint4(xy, zw);
}

#endif
