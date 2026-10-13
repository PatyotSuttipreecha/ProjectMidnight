using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Rigid per-part skinning is intentional for this joint-gap prototype.
public static class TheLostRigBuilder
{
    const string Source="Assets/Prefab/Character/TheLostLowPoly/";
    const string Folder="Assets/Prefab/Character/TheLostRigPrototype/";
    static Dictionary<string,Transform> bones;
    static List<Transform> boneList;
    public static void Run()
    {
        try
        {
            AssetDatabase.DeleteAsset(Folder.TrimEnd('/')); // Disposable validation project, not the delivered assets.
            foreach(var p in new[]{Folder.TrimEnd('/'),Folder+"Meshes",Folder+"Animations"})if(!AssetDatabase.IsValidFolder(p))AssetDatabase.CreateFolder(Path.GetDirectoryName(p).Replace('\\','/'),Path.GetFileName(p));
            GameObject example=null;
            foreach(var type in new[]{"Unarmed","Machete","WoodClub","HeavyAxe","Stone","Torch"})
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"Prefab_Character_TheLost_"+type+"_LowPoly.prefab");
                if(source==null)throw new Exception("Missing visual prefab "+type);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(source);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);go.name="Prefab_Character_TheLost_"+type+"_RigPrototype";
                bones=new Dictionary<string,Transform>();boneList=new List<Transform>();var arm=Child(go.transform,"Armature",Vector3.zero);
                Bone(arm,"Hips",new Vector3(0,.91f,0));Bone(bones["Hips"],"Spine",new Vector3(0,1.10f,0));Bone(bones["Spine"],"Chest",new Vector3(0,1.35f,0));Bone(bones["Chest"],"Neck",new Vector3(0,1.48f,0));Bone(bones["Neck"],"Head",new Vector3(0,1.55f,0));
                foreach(var side in new[]{"Left","Right"})
                {
                    float s=side=="Left"?-1:1;
                    Bone(bones["Chest"],side+"Shoulder",new Vector3(s*.16f,1.40f,0));Bone(bones[side+"Shoulder"],side+"UpperArm",new Vector3(s*.20f,1.40f,0));Bone(bones[side+"UpperArm"],side+"LowerArm",new Vector3(s*.32f,1.16f,.015f));Bone(bones[side+"LowerArm"],side+"Hand",new Vector3(s*.40f,.96f,.065f));
                    Bone(bones["Hips"],side+"UpperLeg",new Vector3(s*.095f,.9f,0));Bone(bones[side+"UpperLeg"],side+"LowerLeg",new Vector3(s*.12f,.49f,.035f));Bone(bones[side+"LowerLeg"],side+"Foot",new Vector3(s*.13f,.11f,0));
                }
                var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var weights=new List<BoneWeight>();var materials=new List<Material>();var submeshes=new List<int[]>();
                foreach(var filter in go.GetComponentsInChildren<MeshFilter>())
                {
                    if(filter.transform.parent.name=="Weapon")continue;
                    var mesh=filter.sharedMesh;var matrix=go.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;int offset=vertices.Count,index=boneList.IndexOf(bones[PartBone(filter.name)]);
                    foreach(var v in mesh.vertices)vertices.Add(matrix.MultiplyPoint3x4(v));foreach(var n in mesh.normals)normals.Add(matrix.MultiplyVector(n).normalized);uv.AddRange(mesh.uv);
                    for(int j=0;j<mesh.vertexCount;j++)weights.Add(new BoneWeight{boneIndex0=index,weight0=1});
                    var indices=mesh.triangles;for(int j=0;j<indices.Length;j++)indices[j]+=offset;submeshes.Add(indices);materials.Add(filter.GetComponent<MeshRenderer>().sharedMaterial);
                }
                var skinMesh=new Mesh{name=type+"_Skinned"};skinMesh.SetVertices(vertices);skinMesh.SetNormals(normals);skinMesh.SetUVs(0,uv);skinMesh.boneWeights=weights.ToArray();skinMesh.subMeshCount=submeshes.Count;for(int i=0;i<submeshes.Count;i++)skinMesh.SetTriangles(submeshes[i],i);
                var bind=new Matrix4x4[boneList.Count];for(int i=0;i<bind.Length;i++)bind[i]=boneList[i].worldToLocalMatrix*go.transform.localToWorldMatrix;skinMesh.bindposes=bind;skinMesh.RecalculateBounds();AssetDatabase.CreateAsset(skinMesh,Folder+"Meshes/"+type+"_Skinned.asset");
                var weapon=go.transform.Find("Visual/Weapon");if(weapon!=null){weapon.SetParent(bones["RightHand"],true);weapon.name="Weapon_Removable";}
                var grip=go.transform.Find("WeaponGripPoint");if(grip!=null){grip.SetParent(bones["RightHand"],true);}
                UnityEngine.Object.DestroyImmediate(go.transform.Find("Visual").gameObject);
                var renderer=Child(go.transform,"SkinnedVisual",Vector3.zero).gameObject.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=skinMesh;renderer.bones=boneList.ToArray();renderer.rootBone=bones["Hips"];renderer.sharedMaterials=materials.ToArray();renderer.localBounds=new Bounds(new Vector3(0,.85f,0),new Vector3(3,3,3));renderer.updateWhenOffscreen=true;
                var animator=go.AddComponent<Animator>();var avatar=AvatarBuilder.BuildGenericAvatar(go,"");avatar.name=type+"_GenericAvatar";if(!avatar.isValid)throw new Exception("Invalid Generic Avatar");AssetDatabase.CreateAsset(avatar,Folder+type+"_GenericAvatar.asset");animator.avatar=avatar;animator.applyRootMotion=false;
                if(example==null)BuildClips(go);
                animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"TheLost_Prototype.controller");
                var collider=go.AddComponent<CapsuleCollider>();collider.height=1.78f;collider.radius=.24f;collider.center=new Vector3(0,.89f,0);
                PrefabUtility.SaveAsPrefabAsset(go,Folder+go.name+".prefab");if(example==null)example=go;else UnityEngine.Object.DestroyImmediate(go);
            }
            ValidateAndPreview(example);AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(Application.dataPath,"../rig-success.txt"),"PASS: six Generic skinned prefabs, valid Avatars, seven animation clips; sampled walk changes foot and baked mesh positions; attack changes hand; all baked vertices finite. Unity "+Application.unityVersion);EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static Transform Child(Transform p,string n,Vector3 v){var t=new GameObject(n).transform;t.SetParent(p,false);t.localPosition=v;return t;}
    static void Bone(Transform p,string n,Vector3 world){var t=Child(p,n,Vector3.zero);t.position=world;bones[n]=t;boneList.Add(t);}
    static string PartBone(string name)
    {
        string side=name.EndsWith("_-1")?"Left":"Right";
        if(name.StartsWith("UpperArm"))return side+"UpperArm";if(name.StartsWith("Forearm"))return side+"LowerArm";if(name.StartsWith("Hand"))return side+"Hand";if(name.StartsWith("UpperLeg"))return side+"UpperLeg";if(name.StartsWith("LowerLeg"))return side+"LowerLeg";if(name.StartsWith("Foot"))return side+"Foot";
        if(name=="Neck")return "Neck";if(name.StartsWith("Torso")||name.StartsWith("Shirt"))return "Chest";if(name.StartsWith("Hips"))return "Hips";return "Head";
    }
    static void Rotation(AnimationClip clip,GameObject go,string bone,string axis,float[] times,float[] values)
    {
        var keys=new Keyframe[times.Length];for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(times[i],values[i]);clip.SetCurve(AnimationUtility.CalculateTransformPath(bones[bone],go.transform),typeof(Transform),"localEulerAnglesRaw."+axis,new AnimationCurve(keys));
    }
    static void BuildClips(GameObject go)
    {
        var controller=AnimatorController.CreateAnimatorControllerAtPath(Folder+"TheLost_Prototype.controller");
        foreach(var kind in new[]{"Idle","Walk","Run","Attack","Reach","Hit","Death"})
        {
            var clip=new AnimationClip{name=kind,frameRate=30};float duration=kind=="Run"?.6f:kind=="Attack"?.9f:kind=="Death"?1.3f:1.2f;var t=new[]{0,duration*.25f,duration*.5f,duration*.75f,duration};
            // Every clip keys each bone, so switching states restores a complete pose.
            foreach(var bone in bones.Keys)foreach(var axis in new[]{"x","y","z"})Rotation(clip,go,bone,axis,t,new float[5]);
            if(kind=="Walk"||kind=="Run")foreach(var side in new[]{"Left","Right"})
            {float sign=side=="Left"?1:-1,amount=kind=="Run"?40:23;Rotation(clip,go,side+"UpperLeg","x",t,new[]{0,amount*sign,0,-amount*sign,0});Rotation(clip,go,side+"LowerLeg","x",t,side=="Left"?new[]{0f,8,0,kind=="Run"?70:35,0}:new[]{0f,kind=="Run"?70:35,0,8,0});Rotation(clip,go,side+"UpperArm","x",t,new[]{0,-amount*sign,0,amount*sign,0});}
            if(kind=="Idle")Rotation(clip,go,"Chest","x",t,new[]{0f,1.5f,0,-1.5f,0});
            if(kind=="Attack"){Rotation(clip,go,"RightUpperArm","x",t,new[]{0f,35,-80,-30,0});Rotation(clip,go,"RightLowerArm","x",t,new[]{0f,-65,-15,-25,0});Rotation(clip,go,"Chest","y",t,new[]{0f,-15,25,10,0});}
            if(kind=="Reach")foreach(var side in new[]{"Left","Right"})Rotation(clip,go,side+"UpperArm","x",t,new[]{0f,-45,-75,-65,0});
            if(kind=="Hit")Rotation(clip,go,"Chest","x",t,new[]{0f,-25,-12,-3,0});
            if(kind=="Death")Rotation(clip,go,"Hips","x",t,new[]{0f,-15,-45,-80,-88});
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=kind=="Idle"||kind=="Walk"||kind=="Run";AnimationUtility.SetAnimationClipSettings(clip,settings);AssetDatabase.CreateAsset(clip,Folder+"Animations/"+kind+".anim");
            string state=kind=="Idle"?"zombie_idle1":kind=="Walk"?"zombie_walk2":kind=="Attack"?"zombie_attack2_1":kind=="Death"?"zombie_dead2":kind;var node=controller.layers[0].stateMachine.AddState(state);node.motion=clip;if(kind=="Idle")controller.layers[0].stateMachine.defaultState=node;
        }
    }
    static void ValidateAndPreview(GameObject actor)
    {
        var animator=actor.GetComponent<Animator>();animator.enabled=false;var foot=actor.transform.Find("Armature/Hips/LeftUpperLeg/LeftLowerLeg/LeftFoot");var hand=actor.transform.Find("Armature/Hips/Spine/Chest/RightShoulder/RightUpperArm/RightLowerArm/RightHand");
        var renderer=actor.GetComponentInChildren<SkinnedMeshRenderer>();var before=new Mesh();renderer.BakeMesh(before);var v=before.vertices;var initial=foot.position;var walk=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"Animations/Walk.anim");walk.SampleAnimation(actor,.3f);if(Vector3.Distance(initial,foot.position)<.01f)throw new Exception("Walk does not move foot");
        var after=new Mesh();renderer.BakeMesh(after);bool changed=false;for(int i=0;i<v.Length;i++){if(Vector3.Distance(v[i],after.vertices[i])>.01f)changed=true;var p=after.vertices[i];if(float.IsNaN(p.x)||float.IsInfinity(p.x))throw new Exception("Invalid baked mesh");}if(!changed)throw new Exception("Skin did not deform");
        var handBefore=hand.position;AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"Animations/Attack.anim").SampleAnimation(actor,.45f);if(Vector3.Distance(handBefore,hand.position)<.01f)throw new Exception("Attack does not move hand");
        var rd=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(rd,Folder+"PreviewRenderer.asset");var pipeline=UniversalRenderPipelineAsset.Create(rd);AssetDatabase.CreateAsset(pipeline,Folder+"PreviewPipeline.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.62f,.65f);
        var light=new GameObject("PreviewLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(30,160,0);
        var c=new GameObject("PreviewCamera").AddComponent<Camera>();c.orthographic=true;c.orthographicSize=1.05f;c.nearClipPlane=.01f;c.farClipPlane=20;c.backgroundColor=new Color(.08f,.1f,.12f);c.clearFlags=CameraClearFlags.SolidColor;c.gameObject.AddComponent<UniversalAdditionalCameraData>();c.transform.position=new Vector3(2,1.5f,4);c.transform.LookAt(new Vector3(0,.85f,0));
        var rt=new RenderTexture(950,1100,24);rt.Create();c.targetTexture=rt;RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(950,1100,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,950,1100),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Application.dataPath,"../TheLost-attack-test.png"),tex.EncodeToPNG());RenderTexture.active=old;c.targetTexture=null;rt.Release();
        AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"Animations/Idle.anim").SampleAnimation(actor,0);
        AssetDatabase.CopyAsset(Folder+"TheLost_Prototype.controller",Folder+"TheLost_AnimationTest.controller");
        var test=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"TheLost_AnimationTest.controller");
        test.AddParameter(new AnimatorControllerParameter{name="PreviewState",type=AnimatorControllerParameterType.Int,defaultInt=1});
        int stateId=1;foreach(var state in test.layers[0].stateMachine.states){var transition=test.layers[0].stateMachine.AddAnyStateTransition(state.state);transition.hasExitTime=false;transition.duration=.1f;transition.canTransitionToSelf=false;transition.AddCondition(AnimatorConditionMode.Equals,stateId++,"PreviewState");}
        animator.runtimeAnimatorController=test;animator.enabled=true;EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Folder+"TheLost_RigPreview.unity");
    }
}
