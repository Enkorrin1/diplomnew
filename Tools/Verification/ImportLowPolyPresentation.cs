UnityEditor.AssetDatabase.Refresh();
foreach (var name in new[] { "engine_bay", "inventory_atlas", "menu_backdrop" })
{
    var path = "Assets/Resources/UI/LowPoly/" + name + ".png";
    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
    importer.textureType = UnityEditor.TextureImporterType.Sprite;
    importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
    importer.alphaIsTransparency = true;
    importer.mipmapEnabled = false;
    importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    importer.filterMode = UnityEngine.FilterMode.Bilinear;
    importer.maxTextureSize = 2048;
    importer.npotScale = UnityEditor.TextureImporterNPOTScale.None;
    importer.textureCompression = UnityEditor.TextureImporterCompression.CompressedHQ;
    if(name == "inventory_atlas")
    {
        importer.spriteImportMode=UnityEditor.SpriteImportMode.Multiple;
        importer.GetSourceTextureWidthAndHeight(out int width,out int height);
        string[] names={"icon_fuel_canister","icon_water_canister","icon_wheel","icon_battery","icon_axe","icon_repair","icon_engine","icon_radiator","icon_scrap"};
        var slices=new UnityEditor.SpriteMetaData[9];
        for(int i=0;i<9;i++) slices[i]=new UnityEditor.SpriteMetaData { name=names[i], rect=new UnityEngine.Rect(i%3*width/3f,(2-i/3)*height/3f,width/3f,height/3f), pivot=new UnityEngine.Vector2(.5f,.5f), alignment=0 };
        importer.spritesheet=slices;
    }
    importer.SaveAndReimport();
}
UnityEditor.AssetDatabase.SaveAssets();
