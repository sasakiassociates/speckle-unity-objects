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

		private void OnEnable()
		{
			if (!converters.Valid())
				converters = new List<ComponentConverter>
				{
					CreateInstance<ComponentConverterMesh>(),
					CreateInstance<ComponentConverterPolyline>(),
					CreateInstance<ComponentConverterPoint>(),
					CreateInstance<ComponentConverterPointCloud>(),
					CreateInstance<ComponentConverterView3D>()
				};
		}

		public override Base ConvertToSpeckle(object @object)
		{
			// convert for unity types
			// we have to figure out what is being passed into there.. it could be a single component that we want to convert
			// or it could be root game object with children we want to handle... for now we will assume this is handled in the loop checks from the client objs
			// or it can be a game object with multiple components that we want to convert
			List<Component> comps = new List<Component>();
			switch (@object)
			{
				case GameObject o:
					comps = o.GetComponents(typeof(Component)).ToList();
					break;
				case Component o:
					comps = new List<Component>() { o };
					break;
				case null:
					Debug.LogWarning("Trying to convert null object to speckle");
					break;
				default:
					Debug.LogException(new SpeckleException($"Native unity object {@object.GetType()} is not supported"));
					break;
			}

			if (!comps.Any())
			{
				Debug.LogWarning("No comps were found in the object trying to be covnerted :(");
				return null;
			}

			// TODO : handle when there is multiple convertable object types on game object
			foreach (var comp in comps)
			{
				var type = comp.GetType().ToString();

				foreach (var converter in converters)
				{
					if (converter.unity_type.Equals(type))
						return converter.ToSpeckle(comp);
				}
			}

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
				{
					if (b is Mesh displayMesh)
					{
						var obj = ConvertToNative(displayMesh) as GameObject;
						if (obj != null)
							obj.transform.SetParent(displayValues.transform);
					}
				}
				return go;
			}

			Debug.LogWarning($"Skipping {@base.GetType()} {@base.id} - Not supported type");
			return null;
		}

	}

}