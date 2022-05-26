using System;
using System.Collections.Generic;
using System.Linq;
using Objects.Geometry;
using Objects.Other;
using Objects.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using Mesh = Objects.Geometry.Mesh;

namespace Speckle.ConnectorUnity.Converter
{
	public static class ComponentConverterHelper
	{

		private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
		private static readonly int Metallic = Shader.PropertyToID("_Metallic");
		private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");

		public static bool IsRuntime
		{
			get => Application.isPlaying;
		}

		public static void AddMesh(this MeshData data, Mesh speckleMesh)
		{
			speckleMesh.AlignVerticesWithTexCoordsByIndex();
			speckleMesh.TriangulateMesh();

			int indexOffset = data.vertices.Count;

			// Convert Vertices
			data.vertices.AddRange(speckleMesh.vertices.ArrayToPoints(speckleMesh.units));

			// Convert texture coordinates
			bool hasValidUVs = speckleMesh.TextureCoordinatesCount == speckleMesh.VerticesCount;
			if (speckleMesh.textureCoordinates.Count > 0 && !hasValidUVs)
				Debug.LogWarning(
					$"Expected number of UV coordinates to equal vertices. Got {speckleMesh.TextureCoordinatesCount} expected {speckleMesh.VerticesCount}. \nID = {speckleMesh.id}");

			if (hasValidUVs)
			{
				data.uvs.Capacity += speckleMesh.TextureCoordinatesCount;
				for (int j = 0; j < speckleMesh.TextureCoordinatesCount; j++)
				{
					var (u, v) = speckleMesh.GetTextureCoordinate(j);
					data.uvs.Add(new Vector2((float)u, (float)v));
				}
			}
			else if (speckleMesh.bbox != null)
			{
				//Attempt to generate some crude UV coordinates using bbox
				////TODO this will be broken for submeshes
				data.uvs.AddRange(speckleMesh.bbox.GenerateUV(data.vertices));
			}

			// Convert vertex colors
			if (speckleMesh.colors != null)
			{
				if (speckleMesh.colors.Count == speckleMesh.VerticesCount)
				{
					data.vertexColors.AddRange(speckleMesh.colors.Select(c => c.ToUnityColor()));
				}
				else if (speckleMesh.colors.Count != 0)
				{
					//TODO what if only some submeshes have colors?
					Debug.LogWarning(
						$"{typeof(Mesh)} {speckleMesh.id} has invalid number of vertex {nameof(Mesh.colors)}. Expected 0 or {speckleMesh.VerticesCount}, got {speckleMesh.colors.Count}");
				}
			}

			var tris = new List<int>();

			// Convert faces
			tris.Capacity += (int)(speckleMesh.faces.Count / 4f) * 3;

			for (int i = 0; i < speckleMesh.faces.Count; i += 4)
			{
				//We can safely assume all faces are triangles since we called TriangulateMesh
				tris.Add(speckleMesh.faces[i + 1] + indexOffset);
				tris.Add(speckleMesh.faces[i + 3] + indexOffset);
				tris.Add(speckleMesh.faces[i + 2] + indexOffset);
			}

			data.subMeshes.Add(tris);
		}

		public static GameObject MeshToNative(this ISpeckleMeshConverter converter, IReadOnlyCollection<Mesh> meshes, GameObject obj)
		{
			var materials = new List<Material>(meshes.Count);

			var data = new MeshData()
			{
				uvs = new List<Vector2>(),
				vertices = new List<Vector3>(),
				subMeshes = new List<List<int>>(),
				vertexColors = new List<Color>()
			};

			foreach (Mesh speckleMesh in meshes)
			{
				if (speckleMesh.vertices.Count == 0 || speckleMesh.faces.Count == 0) continue;

				data.AddMesh(speckleMesh);
				// Convert RenderMaterial
				materials.Add(converter.useRenderMaterial ?
					              GetMaterial(converter, speckleMesh["renderMaterial"] as RenderMaterial) :
					              converter.defaultMaterial
				);
			}

			var nativeMaterials = materials.ToArray();

			var nativeMesh = new UnityEngine.Mesh
			{
				subMeshCount = data.subMeshes.Count
			};

			nativeMesh.SetVertices(data.vertices);
			nativeMesh.SetUVs(0, data.uvs);
			nativeMesh.SetColors(data.vertexColors);

			int j = 0;
			foreach (var subMeshTriangles in data.subMeshes)
			{
				nativeMesh.SetTriangles(subMeshTriangles, j);
				j++;
			}

			if (nativeMesh.vertices.Length >= ushort.MaxValue)
				nativeMesh.indexFormat = IndexFormat.UInt32;

			nativeMesh.Optimize();
			nativeMesh.RecalculateBounds();
			nativeMesh.RecalculateNormals();
			nativeMesh.RecalculateTangents();

			var filter = obj.GetComponent<MeshFilter>();
			
			if (filter == null)
				filter = obj.AddComponent<MeshFilter>();
			
			if (IsRuntime)
				filter.mesh = nativeMesh;
			else
				filter.sharedMesh = nativeMesh;

			if (converter.addMeshCollider)
				filter.gameObject.AddComponent<MeshCollider>().sharedMesh = IsRuntime ? filter.mesh : filter.sharedMesh;

			if (converter.addMeshRenderer)
				filter.gameObject.AddComponent<MeshRenderer>().sharedMaterials = nativeMaterials;

			return obj;
		}

		public static IEnumerable<Vector2> GenerateUV(this Box bbox, IReadOnlyList<Vector3> verts)
		{
			var uv = new Vector2[verts.Count];
			var xSize = (float)bbox.xSize.Length;
			var ySize = (float)bbox.ySize.Length;

			for (var i = 0; i < verts.Count; i++)
			{
				var vert = verts[i];
				uv[i] = new Vector2(vert.x / xSize, vert.y / ySize);
			}
			return uv;
		}

		// Copied from main repo
		public static Material GetMaterial(this ISpeckleMeshConverter converter, RenderMaterial renderMaterial)
		{
			//if a renderMaterial is passed use that, otherwise try get it from the mesh itself
			if (renderMaterial != null)
			{
				// 1. match material by name, if any
				Material matByName = null;

				foreach (var _mat in converter.contextObjects)
				{
					if (((Material)_mat.NativeObject).name == renderMaterial.name)
					{
						if (matByName == null) matByName = (Material)_mat.NativeObject;
						else Debug.LogWarning("There is more than one Material with the name \'" + renderMaterial.name + "\'!", (Material)_mat.NativeObject);
					}
				}
				if (matByName != null) return matByName;

				// 2. re-create material by setting diffuse color and transparency on standard shaders
				Material mat;
				if (renderMaterial.opacity < 1)
				{
					var shader = Shader.Find("Transparent/Diffuse");
					mat = new Material(shader);
				}
				else
				{
					mat = converter.defaultMaterial;
				}

				var c = renderMaterial.diffuse.ToUnityColor();
				mat.color = new Color(c.r, c.g, c.b, (float)renderMaterial.opacity);
				mat.name = renderMaterial.name ?? "material-" + Guid.NewGuid().ToString().Substring(0, 8);

				mat.SetFloat(Metallic, (float)renderMaterial.metalness);
				mat.SetFloat(Glossiness, 1 - (float)renderMaterial.roughness);

				if (renderMaterial.emissive != System.Drawing.Color.Black.ToArgb()) mat.EnableKeyword("_EMISSION");
				mat.SetColor(EmissionColor, renderMaterial.emissive.ToUnityColor());

				return mat;
			}

			// 3. if not renderMaterial was passed, the default shader will be used 
			return converter.defaultMaterial;
		}

	}
}