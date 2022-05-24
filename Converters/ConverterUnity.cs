using System;
using System.Collections.Generic;
using System.Linq;
using Objects;
using Speckle.ConnectorUnity.Converter;
using Speckle.ConnectorUnity.Mono;
using Speckle.Core.Kits;
using Speckle.Core.Logging;
using Speckle.Core.Models;
using UnityEngine;
using Mesh = Objects.Geometry.Mesh;

namespace Speckle.ConnectorUnity.Converter
{
  [CreateAssetMenu(fileName = "UnityConverter", menuName = "Speckle/Speckle Unity Converter", order = -1)]
  public class ConverterUnity : ScriptableObject, ISpeckleConverter
  {
    [Header("Speckle Converter Informations")]
    [SerializeField] protected string description;
    [SerializeField] protected string author;
    [SerializeField] protected string websiteOrEmail;

    [Space]
    [SerializeField] private List<ComponentConverter> otherConverters;

    private Dictionary<string, ComponentConverter> converters;

    public HashSet<Exception> ConversionErrors { get; } = new HashSet<Exception>();

    public List<ApplicationPlaceholderObject> ContextObjects { get; set; } = new List<ApplicationPlaceholderObject>();

    private void OnEnable()
    {
      if (meshConverter == null) meshConverter = CreateInstance<ComponentConverterMesh>();
      if (polylineConverter == null) polylineConverter = CreateInstance<ComponentConverterPolyline>();
      if (pointConverter == null) pointConverter = CreateInstance<ComponentConverterPoint>();
      if (cloudConverter == null) cloudConverter = CreateInstance<ComponentConverterPointCloud>();
      if (view3DConverter == null) view3DConverter = CreateInstance<ComponentConverterView3D>();
    }

    public ProgressReport Report { get; }

    public IEnumerable<string> GetServicedApplications() => new[] { HostApplications.Unity.Name };

    public virtual void SetContextObjects(List<ApplicationPlaceholderObject> objects) => ContextObjects = objects;

    public virtual void SetContextDocument(object doc)
    {
      Debug.Log("Empty call from SetContextDocument");
    }

    public virtual void SetPreviousContextObjects(List<ApplicationPlaceholderObject> objects)
    {
      Debug.Log("Empty call from SetPreviousContextObjects");
    }

    public virtual void SetConverterSettings(object settings)
    {
      Debug.Log($"Converter Settings being set with {settings}");
    }

    public virtual Base ConvertToSpeckle(object @object)
    {

      if (converters == null || !converters.Any())
        CompileConverters(false);

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

        foreach (var pair in converters)
        {
          if (pair.Key.Equals(type))
            return pair.Value.ToSpeckle(comp);
        }
      }

      Debug.LogWarning("No components found for converting to speckle");
      return null;

    }

    public virtual object ConvertToNative(Base @base)
    {
      if (@base == null)
      {
        Debug.LogWarning("Trying to convert a null object! Beep Beep! I don't like that");
        return null;
      }

      if (converters == null || !converters.Any())
        CompileConverters();


      foreach (var pair in converters)
      {
        if (pair.Key.Equals(@base.speckle_type))
          return pair.Value.ToNative(@base);
      }

      Debug.Log($"No Converters were found to handle {@base.speckle_type} trying for display value");

      return TryConvertDefault(@base);
    }

    public List<Base> ConvertToSpeckle(List<object> objects) => objects.Select(ConvertToSpeckle).ToList();

    public List<object> ConvertToNative(List<Base> objects) => objects.Select(ConvertToNative).ToList();

    public virtual bool CanConvertToSpeckle(object @object)
    {
      switch (@object)
      {
        case GameObject o:
          return o.GetComponent<MeshFilter>() != null;
        default:
          return false;
      }
    }

    public virtual bool CanConvertToNative(Base @object)
    {
      switch (@object)
      {
        // case Point _:
        //   return true;
        // case Line _:
        //   return true;
        // case Polyline _:
        //   return true;
        // case Curve _:
        //   return true;
        // case View3D _:
        //   return true;
        // case View2D _:
        //   return false;
        case IDisplayValue<Mesh> _:
          return true;
        case Mesh _:
          return true;
        default:
          return @object["displayMesh"] is Mesh;
      }
    }

    private GameObject TryConvertDefault(Base @base)
    {
      if (@base["displayValue"] is Mesh mesh)
      {
        Debug.Log("Handling Singluar Display Value");

        var go = new GameObject(@base.speckle_type);
        go.AddComponent<BaseBehaviour>().properties = new SpeckleProperties
          { Data = @base.FetchProps() };

        var res =  meshConverter.ToNative(mesh);
        res.transform.SetParent(go.transform);
        return res;
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

    private void CompileConverters(bool toUnity = true)
    {
      converters = new Dictionary<string, ComponentConverter>()
      {
        { meshConverter.targetType(toUnity), meshConverter },
        { polylineConverter.targetType(toUnity), polylineConverter },
        { cloudConverter.targetType(toUnity), cloudConverter },
        { pointConverter.targetType(toUnity), pointConverter },
        { view3DConverter.targetType(toUnity), view3DConverter }
      };

      if (otherConverters != null && otherConverters.Any())
        foreach (var c in otherConverters)
          converters.Add(c.targetType(toUnity), c);

      foreach (var c in converters.Values)
      {
        if (c is IWantContextObj wanter)
          wanter.contextObjects = ContextObjects;
      }
    }

    #region converters
    [Space]
    [Header("Component Converters")]
    // [SerializeField] protected ComponentConverterBase defaultConverter;
    [SerializeField] protected ComponentConverterMesh meshConverter;
    [SerializeField] protected ComponentConverterPolyline polylineConverter;
    [SerializeField] protected ComponentConverterPoint pointConverter;
    [SerializeField] protected ComponentConverterPointCloud cloudConverter;
    [SerializeField] protected ComponentConverterView3D view3DConverter;
    #endregion

    #region converter properties
    public string Name => name;

    public string Description => description;

    public string Author => author;

    public string WebsiteOrEmail => websiteOrEmail;
    #endregion converter properties

  }

}