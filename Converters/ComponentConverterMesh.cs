using System.Collections.Generic;
using System.Linq;
using Speckle.Core.Models;
using UnityEngine;
using Mesh = Objects.Geometry.Mesh;

namespace Speckle.ConnectorUnity.Converter
{

	[CreateAssetMenu(fileName = nameof(ComponentConverterMesh), menuName = "Speckle/Converters/Create Mesh Converter")]
	public class ComponentConverterMesh : ComponentConverter<Mesh, MeshFilter>, ISpeckleMeshConverter
	{

		[SerializeField] private bool _addMeshCollider;
		[SerializeField] private bool _addRenderer = true;
		[SerializeField] private bool _recenterTransform = true;
		[SerializeField] private bool _useRenderMaterial;
		[SerializeField] private Material _defaultMaterial;

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

		protected override GameObject ConvertBase(Mesh @base)
		{
			// convert the mesh data
			return this.MeshToNative(new[] { @base }, BuildGo().gameObject);
		}

		// copied from repo
		//TODO: support multiple filters?
		protected override Base ConvertComponent(MeshFilter component)
		{
			return this.MeshToSpeckle(component);
		}
	}

}