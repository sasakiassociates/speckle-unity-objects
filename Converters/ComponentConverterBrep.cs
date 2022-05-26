using System.Collections.Generic;
using Objects.Geometry;
using Speckle.Core.Models;
using UnityEngine;

namespace Speckle.ConnectorUnity.Converter
{
	[CreateAssetMenu(fileName = nameof(ComponentConverterBrep), menuName = "Speckle/Converters/Create Brep Converter")]
	public class ComponentConverterBrep : ComponentConverter<Brep, SpeckleBrep>, ISpeckleMeshConverter
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

		protected override Base ConvertComponent(SpeckleBrep component) => throw new System.NotImplementedException();

		protected override GameObject ConvertBase(Brep @base)
		{
			return this.MeshToNative(@base.displayValue, BuildGo().gameObject);
		}

	}
}