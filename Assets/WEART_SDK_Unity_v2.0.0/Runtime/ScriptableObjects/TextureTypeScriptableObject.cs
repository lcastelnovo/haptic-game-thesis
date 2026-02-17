using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TextureTypeScriptableObject", menuName = "TextureType")]
public class TextureTypeScriptableObject : ScriptableObject
{
    public enum TextureType : int
    {
        ClickNormal = 0, ClickSoft = 1, DoubleClick = 2,
        AluminiumFineMeshSlow = 3, AluminiumFineMeshFast = 4,
        PlasticMeshSlow = 5, ProfiledAluminiumMeshMedium = 6, ProfiledAluminiumMeshFast = 7,
        RhombAluminiumMeshMedium = 8,
        TextileMeshMedium = 9,
        CrushedRock = 10,
        VenetianGranite = 11,
        SilverOak = 12,
        LaminatedWood = 13,
        ProfiledRubberSlow = 14,
        VelcroHooks = 15,
        VelcroLoops = 16,
        PlasticFoil = 17,
        Leather = 18,
        Cotton = 19,
        Aluminium = 20,
        DoubleSidedTape = 21
    }

    public TextureType textureType;
}
