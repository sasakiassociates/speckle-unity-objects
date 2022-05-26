using System.Collections.Generic;
using Speckle.ConnectorUnity.Mono;
using Speckle.Core.Models;
using UnityEngine;

namespace Speckle.ConnectorUnity.Converter
{
	public class ComponentConverterBase : ComponentConverter<Base, BaseBehaviour>
	{

		protected override GameObject ConvertBase(Base @base)
		{
			// if (@base["displayValue"] is Mesh mesh)
			// {
			//   Debug.Log("Handling Singluar Display Value");
			//   return meshConverter.ToNative(mesh);
			// }
			//
			// if (@base["displayValue"] is IEnumerable<Base> bs)
			// {
			//   Debug.Log("Handling List of Display Value");
			//   return RecurseTreeToNative(bs);
			// }

			SpeckleUnity.Console.Log(name + "does not support converting yet");
			return null;
		}
		protected override Base ConvertComponent(BaseBehaviour component) => throw new System.NotImplementedException();
	}
}