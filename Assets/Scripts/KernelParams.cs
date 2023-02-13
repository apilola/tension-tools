using UnityEngine;

public struct KernelParams
{
    public KernelParams(string name, ComputeShader shader, int groupX, int groupY = 1, int groupZ = 1)
    {
        computeShader = shader;
        Name = name;
        Debug.Log($"Shader Has Kernel [{name}]: {shader.HasKernel(name)}");
        Index = shader.FindKernel(name);
        shader.GetKernelThreadGroupSizes(Index, out uint threadGroupSizeX, out uint threadGroupSizeY, out uint threadGroupSizeZ);
        GroupX = (int)((groupX + (threadGroupSizeX - 1)) / threadGroupSizeX);
        GroupY = (int)((groupY + (threadGroupSizeY - 1)) / threadGroupSizeY);
        GroupZ = (int)((groupZ + (threadGroupSizeZ - 1)) / threadGroupSizeZ);
    }
    public readonly ComputeShader computeShader;
    public readonly string Name;
    public readonly int Index;
    public readonly int GroupX;
    public readonly int GroupY;
    public readonly int GroupZ;


    public void SetBuffer(string name, ComputeBuffer buffer)
    {
        computeShader.SetBuffer(Index, name, buffer);
    }

    public void SetBuffer(string name, GraphicsBuffer buffer)
    {
        computeShader.SetBuffer(Index, name, buffer);
    }

    public void SetTexture(string name, Texture texture)
    {
        computeShader.SetTexture(Index, name, texture);
    }

    public void Dispatch(int x = 0, int y = 0, int z = 0)
    {
        if (x == 0)
            x = GroupX;
        if (y == 0)
            y = GroupY;
        if (z == 0)
            z = GroupZ;
        computeShader.Dispatch(Index, x, y, z);
    }
}