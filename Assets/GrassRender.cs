// https://twitter.com/Cyanilux/status/1396848736022802435

// Requires a specific shader to read the _PerInstanceData buffer at SV_InstanceID
// I use a shader made in Shader Graph, See : https://gist.github.com/Cyanilux/4046e7bf3725b8f64761bf6cf54a16eb
// Also note, there's no frustum culling involved in this example. Typically a compute shader is used for this.

using System.Collections.Generic;
using UnityEngine;

public class GrassRender : MonoBehaviour
{
	[System.Serializable]
	public class CropEntry
	{
		public int cropID;
		public Mesh mesh;
		public Material material;
	}
	
	public List<CropEntry> crops;

	public Vector3 size;
	public Mesh mesh;
	public Material material;
	private MaterialPropertyBlock MPB;
	private UnityEngine.Rendering.ShadowCastingMode shadowCasting = UnityEngine.Rendering.ShadowCastingMode.Off;
	private bool receiveShadows = true;

	private Bounds bounds;
	private ComputeBuffer instancesBuffer;
	private ComputeBuffer argsBuffer;

	private GrassChunk _chunk;
	private InstanceData[] _instances;

	private struct InstanceData {
		public Texture map;

		public int Size() {
			return sizeof(float) * map.height * map.width;
		}
	}

	public void InitializeGrass(GrassChunk chunk)
	{
		_chunk = chunk;
		MPB = new MaterialPropertyBlock();
		// If using DrawMeshInstancedIndirect,
		bounds = new Bounds(transform.position, size);
		InitializeBuffers();
	}

	private void InitializeBuffers() {
		// Args
		uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
		args[0] = (uint)mesh.GetIndexCount(0);
		args[1] = 0;
		args[2] = (uint)mesh.GetIndexStart(0);
		args[3] = (uint)mesh.GetBaseVertex(0);
		argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
		argsBuffer.SetData(args);

		_instances = new InstanceData[crops.Count];
		for (int i = 0; i < crops.Count; i++) {
			InstanceData data = new InstanceData
			{
				map = _chunk.CurrentTexture
			};
			_instances[i] = data;
		}

		instancesBuffer = new ComputeBuffer(1, sizeof(float) * _chunk.CurrentTexture.height * _chunk.CurrentTexture.width);
		instancesBuffer.SetData(_instances);
		material.SetBuffer("_PerInstanceData", instancesBuffer);
	}

	public void Update() {
		//Graphics.DrawMeshInstanced(mesh, 0, material, matrices, count, MPB, shadowCasting, receiveShadows);

		//bounds = new Bounds(transform.position, new Vector3(101, 1, 101));
		Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer, 0, MPB, shadowCasting, receiveShadows);
	}

	private void OnDisable() {
		if (instancesBuffer != null) {
			instancesBuffer.Release();
			instancesBuffer = null;
		}
		if (argsBuffer != null) {
			argsBuffer.Release();
			argsBuffer = null;
		}
	}
}