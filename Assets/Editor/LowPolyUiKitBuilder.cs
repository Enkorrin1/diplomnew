using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RogueDrive.UI;

namespace RogueDrive.EditorScripts
{
    /// <summary>Authors original flat polygon sprites; no screenshot slicing or baked UI text.</summary>
    public static class LowPolyUiKitBuilder
    {
        const string Root = "Assets/Resources/UI/LowPoly";
        [MenuItem("RogueDrive/UI/Build Presentation Frames")]
        public static void BuildPresentation()
        {
            Directory.CreateDirectory(Root);
            Save("gauge_v2",256,()=> {
                Ring(128,128,123,0,C("111B20"),32);
                Ring(128,128,123,118,C("536369"),32);
                Ring(128,128,115,112,C("28393F"),32);
                for(int i=0;i<=30;i++) {
                    float a=(225-i*9)*Mathf.Deg2Rad;
                    float r=i%3==0?86:94;
                    Line(128+Mathf.Cos(a)*r,128-Mathf.Sin(a)*r,128+Mathf.Cos(a)*104,128-Mathf.Sin(a)*104,i%3==0?4:2,i>24?LowPolyUi.Danger:LowPolyUi.Paper);
                }
                Ring(128,128,8,0,C("627177"),12);
            });
            foreach (bool button in new[] { false, true })
            {
                const int n = 128;
                var t = new Texture2D(n,n,TextureFormat.RGBA32,false);
                var data = new Color32[n*n];
                for(int y=0;y<n;y++) for(int x=0;x<n;x++)
                {
                    int edge = Mathf.Min(x,y,n-1-x,n-1-y);
                    float diagonal = Mathf.Min(x+y, x+n-1-y, n-1-x+y, 2*n-2-x-y);
                    if (diagonal < 15) continue;
                    bool top = y > x && y > n-1-x;
                    Color c;
                    if(button)
                        c = edge < 2 || diagonal < 18 ? new Color(.95f,.95f,.95f) : edge < 6 || diagonal < 23 ? (top ? Color.white : new Color(.55f,.55f,.55f)) : Color.Lerp(new Color(.74f,.74f,.74f), new Color(.97f,.97f,.97f), y/(float)n);
                    else
                        c = edge < 1 || diagonal < 17 ? C("58676B") : edge < 8 || diagonal < 27 ? (top ? C("46565A") : C("29383D")) : edge < 10 || diagonal < 30 ? C("111B20") : Color.Lerp(C("131D22"),C("27353B"),y/(float)n);
                    data[y*n+x]=c;
                }
                t.SetPixels32(data);t.Apply();
                string path=Root+(button?"/button_v2.png":"/panel_v2.png");
                File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spriteBorder=new Vector4(32,32,32,32);importer.mipmapEnabled=false;
                importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }
        static Color32[] pixels;
        static int size;
        static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var color); return color; }

        [MenuItem("RogueDrive/UI/Build Low Poly Kit")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);
            Save("bar",64,()=>Box(0,0,64,64,Color.white));
            Save("panel", 64, () => { Poly(C("A6B2B4"), 0,12,12,0,52,0,64,12,64,52,52,64,12,64,0,52); Poly(Color.white, 3,13,13,3,51,3,61,13,61,51,51,61,13,61,3,51); }, true);
            Save("button", 64, () => { Poly(C("B9C3C2"), 0,10,10,0,54,0,64,10,64,54,54,64,10,64,0,54); Poly(Color.white, 2,11,11,2,53,2,62,11,62,53,53,62,11,62,2,53); }, true);
            Save("icon_fuel_canister",256,()=>Can(C("DDB14C"),C("98752F"),false));
            Save("icon_water_canister",256,()=>Can(C("77A9C4"),C("456A84"),true));
            Save("icon_battery",256,Battery);
            Save("icon_wheel",256,Wheel);
            Save("icon_axe",256,()=> { Poly(C("937451"),98,230,115,235,164,50,148,46); Poly(C("9BA8AD"),87,32,152,27,208,57,193,100,137,74,88,70); Poly(C("DBDFDA"),87,32,88,70,66,78,65,37); });
            Save("icon_engine",256,Engine);
            Save("icon_radiator",256,Radiator);
            Save("icon_ammo",256,()=> { for(int i=0;i<3;i++){int x=48+i*54;Poly(C("DDB14C"),x,214,x+34,214,x+34,83,x,83);Poly(C("98752F"),x+24,214,x+34,214,x+34,83,x+24,83);Poly(C("DF6C5A"),x,83,x+17,34,x+34,83);} });
            Save("icon_scrap",256,()=> { Poly(C("74858B"),28,161,79,59,121,145,226,81,183,215,77,207);Poly(C("A3ADB0"),79,59,130,40,151,121,121,145);Poly(C("49565C"),121,145,183,215,77,207); });
            Save("icon_repair",256,()=> { Box(35,76,181,132,C("AE6656"));Box(61,44,128,36,C("45545B"));Box(94,116,65,20,LowPolyUi.Paper);Box(117,92,20,68,LowPolyUi.Paper); });
            Save("icon_car",256,()=> { Poly(C("6B7E86"),83,21,174,21,195,64,190,222,69,222,61,64);Poly(C("293942"),86,75,171,75,178,127,79,127);Poly(C("96A5A6"),82,139,174,139,177,195,79,195); });
            Save("gauge",256,()=> { Ring(128,128,117,108,C("647982"),32); for(int i=0;i<=10;i++){float a=(225-i*27)*Mathf.Deg2Rad;float r=i%5==0?82:91;Line(128+Mathf.Cos(a)*r,128-Mathf.Sin(a)*r,128+Mathf.Cos(a)*103,128-Mathf.Sin(a)*103,3,LowPolyUi.Paper);} });
            Save("needle",256,()=> { Poly(LowPolyUi.Amber,122,130,128,31,134,130,128,142); });
            AssetDatabase.Refresh();
            Debug.Log("Low-poly UI kit: 16 separate sprites imported, panels use 9-slice.");
        }

        [MenuItem("RogueDrive/UI/Apply Low Poly Kit To Build Scenes")]
        public static void ApplyToBuildScenes()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring scenes.");
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled || !File.Exists(entry.path)) continue;
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(entry.path);
                bool alreadyOpen = scene.IsValid() && scene.isLoaded;
                if (!alreadyOpen) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(entry.path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    {
                        if (!canvas.isRootCanvas || canvas.name.StartsWith("LowPoly_")) continue;
                        LowPolyUi.Apply(canvas.transform);
                        if (canvas.GetComponent<LowPolyCanvasTheme>() == null) canvas.gameObject.AddComponent<LowPolyCanvasTheme>();
                    }
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                if (!alreadyOpen) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("Low-poly theme applied to enabled build scenes; existing UI event bindings preserved.");
        }

        static void Can(Color front, Color side, bool water)
        {
            Poly(side,53,79,160,57,205,80,205,221,72,237,43,211,43,100);
            Poly(front,53,87,165,76,179,96,179,217,63,224,53,209);
            Poly(front*1.15f,53,79,79,54,160,57,205,80,165,94);
            Poly(side,87,61,87,28,160,28,178,60,156,64,147,47,105,47,105,62);
            Poly(C("3E4D53"),54,60,80,55,91,76,62,82);
            if(water) Poly(C("E7E8D8"),116,117,94,152,98,168,115,176,133,168,137,152);
            else {Line(80,113,155,193,9,side);Line(154,112,81,194,9,side);}
        }
        static void Battery()
        {
            Poly(C("6C764D"),31,80,185,58,225,83,225,204,69,232,31,210);
            Poly(C("A4AF75"),31,80,185,58,225,83,69,110);
            Poly(C("8D995F"),69,110,225,83,225,204,69,232);
            Box(53,53,31,30,C("D7BE76"));Box(170,43,30,27,C("DF6C5A"));
            Poly(LowPolyUi.Paper,142,122,110,169,140,164,131,204,176,148,147,153);
        }
        static void Wheel()
        {
            Ring(141,132,100,52,C("313B41"),12);Ring(117,119,100,51,C("536168"),12);
            Ring(117,119,57,41,C("9BA7AB"),12);
            for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Line(117+Mathf.Cos(a)*81,119+Mathf.Sin(a)*81,117+Mathf.Cos(a+.08f)*98,119+Mathf.Sin(a+.08f)*98,7,C("39474E"));}
        }
        static void Engine()
        {
            Poly(C("374D5E"),28,83,192,60,228,89,224,202,58,230,26,202);
            Poly(C("829BAB"),28,83,61,48,188,38,211,68,191,90,56,111);
            Poly(C("607E93"),56,111,191,90,224,115,224,202,58,230);
            for(int i=0;i<4;i++){int x=57+i*36;Poly(C("9AABB5"),x,109,x+24,106,x+30,160,x+4,168,x-4,154);Poly(C("456176"),x+24,106,x+34,116,x+39,165,x+30,160);}
            Box(72,185,101,19,C("354852"));
        }
        static void Radiator()
        {
            Poly(C("8C9465"),30,48,204,37,227,58,225,212,45,226,26,205);
            Box(45,62,159,140,C("45513E"));for(int i=0;i<7;i++) Box(49,66+i*19,151,8,C("7C875A"));
            Box(52,33,26,28,C("A4AF75"));Box(170,26,27,29,C("A4AF75"));
        }
        static void Box(float x,float y,float w,float h,Color c)=>Poly(c,x,y,x+w,y,x+w,y+h,x,y+h);
        static void Line(float x,float y,float u,float v,float width,Color c)
        {
            var d=new Vector2(u-x,v-y).normalized;var p=new Vector2(-d.y,d.x)*width*.5f;
            Poly(c,x+p.x,y+p.y,u+p.x,v+p.y,u-p.x,v-p.y,x-p.x,y-p.y);
        }
        static void Ring(float x,float y,float outer,float inner,Color color,int segments)
        {
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                Poly(color,x+Mathf.Cos(a)*outer,y+Mathf.Sin(a)*outer,x+Mathf.Cos(b)*outer,y+Mathf.Sin(b)*outer,x+Mathf.Cos(b)*inner,y+Mathf.Sin(b)*inner,x+Mathf.Cos(a)*inner,y+Mathf.Sin(a)*inner);}
        }
        static void Poly(Color color,params float[] p)
        {
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){
                float px=(x+.5f)*256/size,py=(y+.5f)*256/size;
                // Frame coordinates use a 64px design grid.
                if(size==64){px*=.25f;py*=.25f;}
                bool inside=false;int n=p.Length/2;
                for(int i=0,j=n-1;i<n;j=i++) if((p[2*i+1]>py)!=(p[2*j+1]>py) && px<(p[2*j]-p[2*i])*(py-p[2*i+1])/(p[2*j+1]-p[2*i+1])+p[2*i])inside=!inside;
                if(inside)pixels[(size-1-y)*size+x]=color;
            }
        }
        static void Save(string name,int resolution,Action draw,bool sliced=false)
        {
            size=resolution;pixels=new Color32[size*size];draw();
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
            string path=Root+"/"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=512;
            importer.spriteBorder=sliced?new Vector4(14,14,14,14):Vector4.zero;
            importer.SaveAndReimport();
        }
    }
}
