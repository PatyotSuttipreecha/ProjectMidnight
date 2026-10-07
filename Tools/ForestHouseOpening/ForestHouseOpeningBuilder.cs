using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Offline editor generator. The delivered prefab contains no generator or runtime scripts.
public static class ForestHouseOpeningBuilder
{
    const string Folder = "Assets/Prefab/Environment/ForestHouseOpening/";
    static Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Transform root, house;
    static int count;
    public static void Run()
    {
        try
        {
            foreach (var p in new[]{"Assets/Prefab", "Assets/Prefab/Environment", Folder.TrimEnd('/'), Folder+"Materials"})
                if(!AssetDatabase.IsValidFolder(p)) AssetDatabase.CreateFolder(Path.GetDirectoryName(p).Replace('\\','/'),Path.GetFileName(p));
            Mat("Ground",new Color(.19f,.24f,.17f));Mat("Path",new Color(.40f,.33f,.23f));Mat("Bark",new Color(.22f,.16f,.11f));Mat("Foliage",new Color(.12f,.23f,.15f));
            Mat("Stone",new Color(.34f,.37f,.35f));Mat("Wall",new Color(.49f,.47f,.40f));Mat("Wood",new Color(.29f,.22f,.16f));Mat("Floor",new Color(.37f,.32f,.25f));Mat("Roof",new Color(.20f,.23f,.24f));Mat("Metal",new Color(.30f,.34f,.37f));Mat("Marker",new Color(.76f,.58f,.24f));
            root=Group(null,"ForestHouseOpening_Blockout");
            var ground=Group(root,"01_Ground");Box(ground,"ForestGround",new Vector3(0,-.5f,48),new Vector3(110,1,165),"Ground");
            var trail=Group(root,"02_ForestTrail");
            var points=new[]{new Vector3(-26,0,-27),new Vector3(-26,0,-10),new Vector3(-16,0,6),new Vector3(-23,0,23),new Vector3(-12,0,40),new Vector3(-17,0,54),new Vector3(0,0,69)};
            for(int i=1;i<points.Length;i++)Segment(trail,"Trail_"+i,points[i-1],points[i],3.8f,"Path");
            Segment(trail,"ExploreBranch_A",points[2],new Vector3(0,0,20),2.6f,"Path");Segment(trail,"ExploreBranch_B",new Vector3(0,0,20),points[4],2.6f,"Path");
            var forest=Group(root,"03_Forest");var rng=new System.Random(731);
            for(int i=0;i<230;i++)
            {
                var p=new Vector3((float)rng.NextDouble()*100-50,0,(float)rng.NextDouble()*150-32);
                if(p.z>61&&Mathf.Abs(p.x)<23)continue;
                bool near=false;for(int j=1;j<points.Length;j++)if(Distance(p,points[j-1],points[j])<4.5f)near=true;
                if(Distance(p,points[2],new Vector3(0,0,20))<3.5f||Distance(p,new Vector3(0,0,20),points[4])<3.5f)near=true;
                if(near)continue;Tree(forest,p,5.5f+(float)rng.NextDouble()*3);
            }
            var obstacles=Group(root,"04_TrailLandmarks");
            foreach(var p in new[]{new Vector3(-29,0,-4),new Vector3(-19,0,16),new Vector3(-26,0,32),new Vector3(-8,0,45)})
                Shape(obstacles,"Rock",PrimitiveType.Sphere,p+Vector3.up*.7f,new Vector3(2.8f,1.4f,2),"Stone");
            Box(obstacles,"FallenLogBesideTrail",new Vector3(-9,.4f,25),new Vector3(5,.8f,.8f),"Bark").localRotation=Quaternion.Euler(0,28,0);
            Fence(obstacles,new Vector3(-5,0,64),new Vector3(-5,0,71));Fence(obstacles,new Vector3(5,0,64),new Vector3(5,0,71));
            house=Group(root,"05_AbandonedHouse");house.localPosition=new Vector3(0,0,78);
            var structure=Group(house,"Structure");Box(structure,"Foundation",new Vector3(0,.1f,0),new Vector3(14.5f,.2f,12.5f),"Stone");Box(structure,"Floor",new Vector3(0,.25f,0),new Vector3(14,.1f,12),"Floor");
            // Traversable 1.8 m front and rear doorways. No door slab blocks the opening.
            DoorWall(structure,"FrontWall",-6,14,0);DoorWall(structure,"RearWall",6,14,0);
            SideWall(structure,"LeftWall",-7);SideWall(structure,"RightWall",7);
            // Central corridor x = -1.2 .. 1.2; room openings centered at z +/- 2.8.
            foreach(float x in new[]{-1.35f,1.35f})
            {
                foreach(var span in new[]{new Vector2(-6,-3.7f),new Vector2(-1.9f,1.9f),new Vector2(3.7f,6)})
                    Box(structure,"CorridorWall",new Vector3(x,1.8f,(span.x+span.y)/2),new Vector3(.18f,3,span.y-span.x),"Wall");
                foreach(float z in new[]{-2.8f,2.8f})Box(structure,"InteriorDoorLintel",new Vector3(x,2.95f,z),new Vector3(.18f,.7f,1.8f),"Wall");
            }
            foreach(float x in new[]{-4.175f,4.175f})Box(structure,"RoomDivider",new Vector3(x,1.8f,0),new Vector3(5.65f,3,.18f),"Wall");
            var roof=Group(house,"Roof_ToggleForEditing");
            for(int side=-1;side<=1;side+=2){var r=Box(roof,"SlopedRoof",new Vector3(side*3.6f,4.0f,0),new Vector3(7.8f,.18f,13),"Roof");r.localRotation=Quaternion.Euler(0,0,-side*12);}
            var porch=Group(house,"Porch");Box(porch,"Deck",new Vector3(0,.1f,-7.4f),new Vector3(5.2f,.2f,2.6f),"Wood");Box(porch,"ApproachStep",new Vector3(0,.05f,-9),new Vector3(2.4f,.1f,.6f),"Stone");
            foreach(float x in new[]{-2.4f,2.4f})Box(porch,"Post",new Vector3(x,1.6f,-8.4f),new Vector3(.16f,3,.16f),"Wood");Box(porch,"Canopy",new Vector3(0,3.15f,-7.4f),new Vector3(5.4f,.16f,3),"Roof");
            var rooms=Group(house,"FurnitureAndLootPlaceholders");
            Table(rooms,new Vector3(3.8f,0,-4.5f),"LivingRoom_FirstPickup");Box(rooms,"Sofa",new Vector3(5.8f,.7f,-2.7f),new Vector3(1.2f,.8f,2),"Wood");
            Table(rooms,new Vector3(-5.5f,0,-3.7f),"Kitchen_Equipment");Box(rooms,"KitchenCounter",new Vector3(-6.2f,.8f,-1.2f),new Vector3(1,1,1.8f),"Stone");
            Table(rooms,new Vector3(-5.2f,0,3.7f),"Storage_WeaponAndAmmo");Box(rooms,"StorageShelf",new Vector3(-6.4f,1.2f,1.4f),new Vector3(.65f,1.8f,2),"Wood");
            Box(rooms,"Bed",new Vector3(5.2f,.55f,3),new Vector3(2.3f,.5f,3.3f),"Wood");
            var yard=Group(root,"06_BackyardShootingArea");Box(yard,"YardSurface",new Vector3(0,.015f,101),new Vector3(22,.03f,24),"Path");Box(yard,"BackstopEarthBerm",new Vector3(0,1.7f,113),new Vector3(18,3.4f,3.6f),"Ground");
            foreach(var p in new[]{new Vector3(-4,0,99),new Vector3(0,0,104),new Vector3(4,0,109)})
            {Box(yard,"TargetPost",p+Vector3.up*.9f,new Vector3(.16f,1.8f,.16f),"Wood");Box(yard,"TargetBoard_StaticPlaceholder",p+Vector3.up*1.6f,new Vector3(.8f,.8f,.12f),"Marker");}
            Fence(yard,new Vector3(-11,0,89),new Vector3(-11,0,110));Fence(yard,new Vector3(11,0,89),new Vector3(11,0,110));
            var markers=Group(root,"07_GameplayPlacementMarkers_NoLogic");
            Mark(markers,"PlayerSpawn",points[0]+Vector3.up*.1f);Mark(markers,"HouseReveal",new Vector3(0,.1f,61));Mark(markers,"FirstPickup",new Vector3(3.8f,1.15f,73.5f));Mark(markers,"EquipmentPickup",new Vector3(-5.5f,1.15f,74.3f));Mark(markers,"WeaponPickup",new Vector3(-5.2f,1.15f,81.7f));Mark(markers,"AmmoPickup",new Vector3(-4.6f,1.15f,81.7f));Mark(markers,"ShootingStart",new Vector3(0,.1f,91));Mark(markers,"NextAreaExit",new Vector3(14,.1f,108));
            Physics.SyncTransforms();
            foreach(float z in new[]{72,75,78,81,84}) if(Physics.CheckCapsule(new Vector3(0,.65f,z),new Vector3(0,1.8f,z),.32f))throw new Exception("Blocked central route at "+z);
            foreach(var p in points)if(!Physics.Raycast(p+Vector3.up*2,Vector3.down,3))throw new Exception("Unsupported forest route");
            var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+"ForestHouseOpening_Blockout.prefab");
            if(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)throw new Exception("Unexpected script dependency");
            Preview();AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Application.dataPath,"../forest-house-success.txt"),"Unity "+Application.unityVersion+": prefab saved; "+count+" primitive mesh objects; no runtime scripts; forest ground support and central doorway/corridor clearance passed.");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Mat(string n,Color c){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=n};m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(m,Folder+"Materials/"+n+".mat");mats[n]=m;}
    static Transform Group(Transform p,string n){var g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
    static Transform Box(Transform p,string n,Vector3 v,Vector3 s,string m){return Shape(p,n,PrimitiveType.Cube,v,s,m);}
    static Transform Shape(Transform p,string n,PrimitiveType t,Vector3 v,Vector3 s,string m){var g=GameObject.CreatePrimitive(t);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=v;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=mats[m];count++;return g.transform;}
    static void Segment(Transform p,string n,Vector3 a,Vector3 b,float w,string m){var d=b-a;var g=Box(p,n,(a+b)/2+Vector3.up*.015f,new Vector3(w,.03f,d.magnitude+.2f),m);g.rotation=Quaternion.LookRotation(d);UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}
    static float Distance(Vector3 p,Vector3 a,Vector3 b){var d=b-a;return Vector3.Distance(p,a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude));}
    static void Tree(Transform p,Vector3 v,float h){var t=Group(p,"Tree");t.localPosition=v;Shape(t,"Trunk",PrimitiveType.Cylinder,new Vector3(0,h*.3f,0),new Vector3(.55f,h*.3f,.55f),"Bark");var c=Shape(t,"Canopy",PrimitiveType.Sphere,new Vector3(0,h*.75f,0),new Vector3(3.5f,h*.6f,3.5f),"Foliage");UnityEngine.Object.DestroyImmediate(c.GetComponent<Collider>());}
    static void Fence(Transform p,Vector3 a,Vector3 b){int n=Mathf.CeilToInt(Vector3.Distance(a,b)/2);for(int i=0;i<=n;i++)Box(p,"FencePost",Vector3.Lerp(a,b,(float)i/n)+Vector3.up*.65f,new Vector3(.14f,1.3f,.14f),"Wood");for(float y=.45f;y<1.1f;y+=.5f){var d=b-a;var t=Box(p,"FenceRail",(a+b)/2+Vector3.up*y,new Vector3(.12f,.14f,d.magnitude),"Wood");t.rotation=Quaternion.LookRotation(d);}}
    static void DoorWall(Transform p,string n,float z,float width,float x){float side=(width-1.8f)/2;foreach(int s in new[]{-1,1})Box(p,n,new Vector3(x+s*(.9f+side/2),1.8f,z),new Vector3(side,3,.22f),"Wall");Box(p,n+"_Lintel",new Vector3(x,2.95f,z),new Vector3(1.8f,.7f,.22f),"Wall");}
    static void SideWall(Transform p,string n,float x){foreach(var span in new[]{new Vector2(-6,-4.6f),new Vector2(-3,-.8f),new Vector2(.8f,3),new Vector2(4.6f,6)})Box(p,n,new Vector3(x,1.8f,(span.x+span.y)/2),new Vector3(.22f,3,span.y-span.x),"Wall");foreach(float z in new[]{-3.8f,0,3.8f}){Box(p,n+"_WindowSill",new Vector3(x,.8f,z),new Vector3(.22f,1,1.6f),"Wall");Box(p,n+"_WindowHeader",new Vector3(x,2.95f,z),new Vector3(.22f,.7f,1.6f),"Wall");}}
    static void Table(Transform p,Vector3 v,string n){var t=Group(p,n);t.localPosition=v;Box(t,"TableTop",new Vector3(0,1,0),new Vector3(1.8f,.12f,1),"Wood");foreach(float x in new[]{-.75f,.75f})foreach(float z in new[]{-.35f,.35f})Box(t,"Leg",new Vector3(x,.6f,z),new Vector3(.1f,.8f,.1f),"Wood");Box(t,"LootPlaceholder_ReplaceWithInteractable",new Vector3(0,1.14f,0),new Vector3(.28f,.16f,.22f),"Marker");}
    static void Mark(Transform p,string n,Vector3 v){Group(p,n).localPosition=v;}
    static void Preview()
    {
        var rd=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(rd,Folder+"PreviewRenderer.asset");var pipeline=UniversalRenderPipelineAsset.Create(rd);AssetDatabase.CreateAsset(pipeline,Folder+"PreviewPipeline.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.67f,.64f);
        var l=new GameObject("PreviewSun").AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.6f;l.transform.rotation=Quaternion.Euler(45,-35,0);
        var c=new GameObject("PreviewCamera").AddComponent<Camera>();c.orthographic=true;c.nearClipPlane=.1f;c.farClipPlane=500;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.14f,.18f,.19f);c.gameObject.AddComponent<UniversalAdditionalCameraData>();
        c.orthographicSize=83;c.transform.position=new Vector3(110,165,-80);c.transform.LookAt(new Vector3(0,0,43));Render(c,"ForestHouse-overview.png");
        house.Find("Roof_ToggleForEditing").gameObject.SetActive(false);c.orthographicSize=12;c.transform.position=new Vector3(14,24,57);c.transform.LookAt(new Vector3(0,0,78));Render(c,"ForestHouse-house-cutaway.png");house.Find("Roof_ToggleForEditing").gameObject.SetActive(true);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Folder+"ForestHouseOpening_Preview.unity");
    }
    static void Render(Camera c,string name){var rt=new RenderTexture(1400,1100,24);rt.Create();c.targetTexture=rt;RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1400,1100,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1400,1100),0,0);t.Apply();File.WriteAllBytes(Path.Combine(Application.dataPath,"../"+name),t.EncodeToPNG());RenderTexture.active=old;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(t);}
}
