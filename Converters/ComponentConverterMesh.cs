using System.Collections.Generic;
using System.Linq;
using Objects;
using Speckle.Core.Models;
using UnityEngine;
using UnityEngine.Rendering;
using Mesh = Objects.Geometry.Mesh;

namespace Speckle.ConnectorUnity.Converter
{

	[CreateAssetMenu(fileName = nameof(ComponentConverterMesh), menuName = "Speckle/Converters/Create Mesh Converter")]
	public class ComponentConverterMesh : ComponentConverter<Mesh, MeshFilter>, ISpeckleMeshConverter
	{

		[SerializeField] private bool _addMeshCollider = false;
		[SerializeField] private bool _addRenderer = true;
		[SerializeField] private bool _recenterTransform = true;
		[SerializeField] private bool _useRenderMaterial;
		[SerializeField] private Material _defaultMaterial;

		public List<ApplicationPlaceholderObject> contextObjects { get; set; }

		public bool addMeshCollider
		{
			get => _addMeshCollider;
		}

		public bool addMeshRenderer
		{
			get => _addRenderer;
		}

		public bool recenterTransform
		{
			get => _recenterTransform;
		}

		public bool useRenderMaterial
		{
			get => _useRenderMaterial;
		}

		public Material defaultMaterial
		{
			get => _defaultMaterial;
		}

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

			if (_defaultMaterial == null)
				_defaultMaterial = new Material(Shader.Find("Standard"));
		}

		protected override GameObject ConvertBase(Mesh @base)
		{
			// convert the mesh data
			return this.MeshToNative(new[] { @base }, BuildGo().gameObject);
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

			var mesh = new Mesh
			{
				vertices = sVertices,
				faces = sFaces,
				colors = sColors,
				textureCoordinates = sTexCoords,
				units = ModelUnits
			};

			return mesh;
		}

	}

}