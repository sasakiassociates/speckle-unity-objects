using System;
using System.Collections.Generic;
using System.Linq;
using Objects.Other;
using Objects.Utils;
using Speckle.ConnectorUnity.Converter;
using Speckle.Core.Logging;
using Speckle.Core.Models;
using UnityEngine;
using UnityEngine.Rendering;
using Mesh = Objects.Geometry.Mesh;

namespace Speckle.ConnectorUnity.Converter
{
  [CreateAssetMenu(fileName = "MeshComponentConverter", menuName = "Speckle/Mesh Converter Component")]
  public class ComponentConverterMesh : ComponentConverter<Mesh, MeshFilter>, IWantContextObj
  {

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int Metallic = Shader.PropertyToID("_Metallic");
    private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");

    public bool addMeshCollider = false;
    public bool addRender = true;
    public bool recenterTransform = true;
    public bool useRenderMaterial;
    public Material defaultMaterial;

    public List<ApplicationPlaceholderObject> contextObjects { get; set; }

    protected override HashSet<string> excludedProps
    {
      get
      {
        var res = base.excludedProps;
        res.Add("displayValue");
        res.Add("displayMesh");
        return res;
      }
    }

    protected override void OnEnable()
    {
      base.OnEnable();

      if (defaultMaterial == null)
        defaultMaterial = new Material(Shader.Find("Standard"));
    }

    protected override GameObject ConvertBase(Mesh @base)
    {
      // convert the mesh data
      MeshDataToNative(new[] { @base }, out var mesh, out var materials);

      // Setting mesh to filter once all mesh modifying is done
      var comp = BuildGo();

      // TODO: The mesh is already offset, so this is doubling the offset... 
      // if (recenterTransform)
      // {
      //   var verts = mesh.vertices;
      //   //  center transform pivot according to the bounds of the model
      //   Bounds meshBounds = new Bounds { center = verts[0] };
      //
      //   foreach (var vert in verts)
      //   {
      //     meshBounds.Encapsulate(vert);
      //   }
      //
      //   // offset mesh vertices
      //   for (int l = 0; l < verts.Length; l++)
      //   {
      //     verts[l] -= meshBounds.center;
      //   }
      //   
      //   comp.transform.position = meshBounds.center;
      // }

      if (IsRuntime)
        comp.mesh = mesh;
      else
        comp.sharedMesh = mesh;

      if (addMeshCollider)
        comp.gameObject.AddComponent<MeshCollider>().sharedMesh = IsRuntime ? comp.mesh : comp.sharedMesh;

      if (addRender)
        comp.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;


      return comp.gameObject;
    }

    // copied from repo
    //TODO: support multiple filters?
    protected override Base ConvertComponent(MeshFilter component)
    {

      var nativeMesh = IsRuntime ? component.mesh : component.sharedMesh;

      var nTriangles = nativeMesh.triangles;
      List<int> sFaces = new List<int>(nTriangles.Length * 4);
      for (int i = 2; i < nTriangles.Length; i += 3)
      {
        sFaces.Add(0); //Triangle cardinality indicator

        sFaces.Add(nTriangles[i]);
        sFaces.Add(nTriangles[i - 1]);
        sFaces.Add(nTriangles[i - 2]);
      }


      var nVertices = nativeMesh.vertices;
      List<double> sVertices = new List<double>(nVertices.Length * 3);

      foreach (var vertex in nVertices)
      {
        var p = component.gameObject.transform.TransformPoint(vertex);
        sVertices.Add(p.x);
        sVertices.Add(p.z); //z and y swapped
        sVertices.Add(p.y);
      }

      var nColors = nativeMesh.colors;
      List<int> sColors = new List<int>(nColors.Length);
      sColors.AddRange(nColors.Select(c => c.ToIntColor()));

      var nTexCoords = nativeMesh.uv;
      List<double> sTexCoords = new List<double>(nTexCoords.Length * 2);
      foreach (var uv in nTexCoords)
      {
        sTexCoords.Add(uv.x);
        sTexCoords.Add(uv.y);
      }

      // NOTE: this throws some exceptions with trying to set a method that isn't settable.
      // Looking at other converters it seems like the conversion code should be handling all the prop settings..
      
      //
      // // get the speckle data from the go here
      // // so that if the go comes from speckle, typed props will get overridden below
      // // TODO: Maybe handle a better way of overriding props? Or maybe this is just the typical logic for connectors 
      // if (convertProps)
      // {
      //   // Base behaviour is the standard unity mono type that stores the speckle props data
      //   var baseBehaviour = component.GetComponent(typeof(BaseBehaviour)) as BaseBehaviour;
      //   if (baseBehaviour != null && baseBehaviour.properties != null)
      //   {
      //     baseBehaviour.properties.AttachUnityProperties(mesh, excludedProps);
      //   }
      // }
      
      var mesh = new Mesh();
      mesh.vertices = sVertices;
      mesh.faces = sFaces;
      mesh.colors = sColors;
      mesh.textureCoordinates = sTexCoords;
      mesh.units = ModelUnits;

      return mesh;
    }

    private void MeshDataToNative(IReadOnlyCollection<Mesh> meshes, out UnityEngine.Mesh nativeMesh, out Material[] nativeMaterials)
    {
      var verts = new List<Vector3>();

      var uvs = new List<Vector2>();
      var vertexColors = new List<Color>();

      var materials = new List<Material>(meshes.Count);
      var subMeshes = new List<List<int>>(meshes.Count);

      foreach (Mesh m in meshes)
      {
        if (m.vertices.Count == 0 || m.faces.Count == 0) continue;

        List<int> tris = new List<int>();
        SubmeshToNative(m, verts, tris, uvs, vertexColors, materials);
        subMeshes.Add(tris);
      }
      nativeMaterials = materials.ToArray();

      nativeMesh = new UnityEngine.Mesh
      {
        subMeshCount = subMeshes.Count
      };

      nativeMesh.SetVertices(verts);
      nativeMesh.SetUVs(0, uvs);
      nativeMesh.SetColors(vertexColors);


      int j = 0;
      foreach (var subMeshTriangles in subMeshes)
      {
        nativeMesh.SetTriangles(subMeshTriangles, j);
        j++;
      }

      if (nativeMesh.vertices.Length >= UInt16.MaxValue)
        nativeMesh.indexFormat = IndexFormat.UInt32;

      nativeMesh.Optimize();
      nativeMesh.RecalculateBounds();
      nativeMesh.RecalculateNormals();
      nativeMesh.RecalculateTangents();
    }

    private void SubmeshToNative(Mesh speckleMesh, List<Vector3> verts, List<int> tris, List<Vector2> texCoords, List<Color> vertexColors, List<Material> materials)
    {
      speckleMesh.AlignVerticesWithTexCoordsByIndex();
      speckleMesh.TriangulateMesh();

      int indexOffset = verts.Count;

      // Convert Vertices
      verts.AddRange(speckleMesh.vertices.ArrayToPoints(speckleMesh.units));

      // Convert texture coordinates
      bool hasValidUVs = speckleMesh.TextureCoordinatesCount == speckleMesh.VerticesCount;
      if (speckleMesh.textureCoordinates.Count > 0 && !hasValidUVs)
        Debug.LogWarning(
          $"Expected number of UV coordinates to equal vertices. Got {speckleMesh.TextureCoordinatesCount} expected {speckleMesh.VerticesCount}. \nID = {speckleMesh.id}");

      if (hasValidUVs)
      {
        texCoords.Capacity += speckleMesh.TextureCoordinatesCount;
        for (int j = 0; j < speckleMesh.TextureCoordinatesCount; j++)
        {
          var (u, v) = speckleMesh.GetTextureCoordinate(j);
          texCoords.Add(new Vector2((float)u, (float)v));
        }
      }
      else if (speckleMesh.bbox != null)
      {
        //Attempt to generate some crude UV coordinates using bbox //TODO this will be broken for submeshes
        texCoords.AddRange(GenerateUV(verts, (float)speckleMesh.bbox.xSize.Length, (float)speckleMesh.bbox.ySize.Length));
      }

      // Convert vertex colors
      if (speckleMesh.colors != null)
      {
        if (speckleMesh.colors.Count == speckleMesh.VerticesCount)
        {
          vertexColors.AddRange(speckleMesh.colors.Select(c => c.ToUnityColor()));
        }
        else if (speckleMesh.colors.Count != 0)
        {
          //TODO what if only some submeshes have colors?
          Debug.LogWarning(
            $"{typeof(Mesh)} {speckleMesh.id} has invalid number of vertex {nameof(Mesh.colors)}. Expected 0 or {speckleMesh.VerticesCount}, got {speckleMesh.colors.Count}");
        }
      }

      // Convert faces
      tris.Capacity += (int)(speckleMesh.faces.Count / 4f) * 3;

      for (int i = 0; i < speckleMesh.faces.Count; i += 4)
      {
        //We can safely assume all faces are triangles since we called TriangulateMesh
        tris.Add(speckleMesh.faces[i + 1] + indexOffset);
        tris.Add(speckleMesh.faces[i + 3] + indexOffset);
        tris.Add(speckleMesh.faces[i + 2] + indexOffset);
      }


      // Convert RenderMaterial
      materials.Add(GetMaterial(speckleMesh["renderMaterial"] as RenderMaterial));
    }

    // Copied from main repo
    public Material GetMaterial(RenderMaterial renderMaterial)
    {
      if (!useRenderMaterial)
        return defaultMaterial;

      //if a renderMaterial is passed use that, otherwise try get it from the mesh itself
      if (renderMaterial != null)
      {
        // 1. match material by name, if any
        Material matByName = null;

        foreach (var _mat in contextObjects)
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
          mat = defaultMaterial;
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
      return defaultMaterial;

    }

    private static IEnumerable<Vector2> GenerateUV(IReadOnlyList<Vector3> verts, float xSize, float ySize)
    {
      var uv = new Vector2[verts.Count];
      for (var i = 0; i < verts.Count; i++)
      {

        var vert = verts[i];
        uv[i] = new Vector2(vert.x / xSize, vert.y / ySize);
      }
      return uv;
    }
  }

}