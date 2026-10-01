using System;
using System.Linq;
using ithappy.City_Characters_Mini.CharacterCustomizationTool.Editor.Extensions;
using UnityEngine;

namespace ithappy.City_Characters_Mini.CharacterCustomizationTool.FaceManagement
{
    [Serializable]
    public class FaceMesh
    {
        public FaceType Type;
        public Mesh Mesh;

        public FaceMesh(Mesh mesh)
        {
            Type = Parse(mesh);
            Mesh = mesh;
        }

        public static FaceType Parse(Mesh mesh) => Enum.Parse<FaceType>(mesh.name.Split("_").Last().ToCapital());
    }
}