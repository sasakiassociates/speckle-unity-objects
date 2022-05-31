using System.Collections.Generic;
using System.Linq;
using Speckle.ConnectorUnity.Mono;
using Speckle.Core.Logging;
using Speckle.Core.Models;
using UnityEngine;
using Mesh = Objects.Geometry.Mesh;

namespace Speckle.ConnectorUnity.Converter
{
	[CreateAssetMenu(fileName = "UnityConverter", menuName = "Speckle/Speckle Unity Converter", order = -1)]
	public class ConverterUnity : ScriptableSpeckleConverter
	{

		[SerializeField] private ComponentConverterBase defaultConverter;

		private void OnEnable()
		{
			if (defaultConverter == null)
				defaultConverter = CreateInstance<ComponentConverterBase>();

			if (!converters.Valid())
				converters = new List<ComponentConverter>
				{
					CreateInstance<ComponentConverterMesh>(),
					CreateInstance<ComponentConverterPolyline>(),
					CreateInstance<ComponentConverterPoint>(),
					CreateInstance<ComponentConverterPointCloud>(),
					CreateInstance<ComponentConverterView3D>(),
					CreateInstance<ComponentConverterBrep>()
				};
		}

		public override Base ConvertToSpeckle(object @object)
		{
			if (TryGetConverter(@object, out var comp, out var converter))
				return converter.ToSpeckle(comp);

			
			Debug.LogWarning("No components found for converting to speckle");

			return null;
		}

		public override object ConvertToNative(Base @base)
		{
			if (@base == null)
			{
				Debug.LogWarning("Trying to convert a null object! Beep Beep! I don't like that");
				return null;
			}

			foreach (var converter in converters)
				if (converter.speckle_type.Equals(@base.speckle_type))
					return converter.ToNative(@base);

			Debug.Log($"No Converters were found to handle {@base.speckle_type} trying for display value");

			return TryConvertDefault(@base);
		}

		private GameObject TryConvertDefault(Base @base)
		{
			if (@base["displayValue"] is Mesh mesh)
			{
				Debug.Log("Handling Singluar Display Value");

				var go = new GameObject(@base.speckle_type);
				go.AddComponent<BaseBehaviour>().properties = new SpeckleProperties
					{ Data = @base.FetchProps() };

				var res = ConvertToNative(mesh) as Component;
				res.transform.SetParent(go.transform);
				return res.gameObject;
			}

			if (@base["displayValue"] is IEnumerable<Base> bs)
			{
				Debug.Log("Handling List of Display Value");

				var go = new GameObject(@base.speckle_type);
				go.AddComponent<BaseBehaviour>().properties = new SpeckleProperties
					{ Data = @base.FetchProps() };

				var displayValues = new GameObject("DisplayValues");
				displayValues.transform.SetParent(go.transform);

				foreach (var b in bs)
					if (b is Mesh displayMesh)
					{
						var obj = ConvertToNative(displayMesh) as GameObject;
						if (obj != null)
							obj.transform.SetParent(displayValues.transform);
					}
				return go;
			}

			Debug.LogWarning($"Skipping {@base.GetType()} {@base.id} - Not supported type");
			return null;
		}
	}

}