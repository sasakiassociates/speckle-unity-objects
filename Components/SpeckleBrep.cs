using Speckle.ConnectorUnity.Mono;
using UnityEngine;

namespace Speckle.ConnectorUnity
{
	public class SpeckleBrep : BaseBehaviour
	{
		public MeshFilter mesh => gameObject.GetComponent<MeshFilter>();
	}
}