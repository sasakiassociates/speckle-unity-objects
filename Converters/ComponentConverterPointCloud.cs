﻿using System;
using Objects.Geometry;
using Speckle.ConnectorUnity.Converter;
using Speckle.Core.Models;
using UnityEngine;

namespace Speckle.ConnectorUnity.Converter
{
  [CreateAssetMenu(fileName = "PointCloudConverter", menuName = "Speckle/PointCloud Converter")]
  public class ComponentConverterPointCloud : ComponentConverter<Pointcloud, ParticleSystem>
  {

    protected override GameObject ConvertBase(Pointcloud @base) => throw new NotImplementedException();
    protected override Base ConvertComponent(ParticleSystem component) => throw new NotImplementedException();
  }

}