using UnityEngine;

namespace Speckle.ConnectorUnity.Converter
{
	public interface ISpeckleMeshConverter : IWantContextObj
	{
		public bool addMeshCollider { get; }
		public bool addMeshRenderer { get; }
		public bool recenterTransform { get; }
		public bool useRenderMaterial { get; }
		public Material defaultMaterial { get; }

	}
}