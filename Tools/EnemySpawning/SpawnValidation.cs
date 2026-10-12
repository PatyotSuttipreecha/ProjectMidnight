using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class SpawnValidation
{
    static EnemyController prefab;
    static void Check(bool value,string label){if(!value)throw new Exception(label);}
    static EnemySpawnPoint Make(EnemySpawnPoint.SpawnMode mode,int batch,int alive,int total=10)
    {
        var go=new GameObject("Spawner");go.SetActive(false);var s=go.AddComponent<EnemySpawnPoint>();
        s.enemyPrefab=prefab;s.spawnMode=mode;s.initialCount=batch;s.maxAlive=alive;s.enemyCount=total;s.spawnRadius=1;
        go.SetActive(true);return s;
    }
    static void Tick(EnemySpawnPoint s)
    {
        typeof(EnemySpawnPoint).GetField("nextSpawnTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,Time.time-1);
        typeof(EnemySpawnPoint).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);
    }
    public static void Run()
    {
        EditorSettings.enterPlayModeOptionsEnabled=true;
        EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
        EditorApplication.playModeStateChanged += Entered;
        EditorApplication.EnterPlaymode();
    }
    static void Entered(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode)return;
        try
        {
            var source=new NavMeshBuildSource {shape=NavMeshBuildSourceShape.Box,size=new Vector3(60,.2f,60),transform=Matrix4x4.TRS(new Vector3(0,-.1f,0),Quaternion.identity,Vector3.one),area=0};
            var data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),new System.Collections.Generic.List<NavMeshBuildSource>{source},new Bounds(Vector3.zero,new Vector3(70,10,70)),Vector3.zero,Quaternion.identity);
            Check(data!=null,"NavMesh build");NavMesh.AddNavMeshData(data);
            var template=new GameObject("EnemyPrefab");template.SetActive(false);prefab=template.AddComponent<EnemyController>();prefab.enemyData=ScriptableObject.CreateInstance<EnemyDataSO>();
            var once=Make(EnemySpawnPoint.SpawnMode.Once,2,5);once.StartSpawning();Check(once.SpawnedCount==2&&!once.IsSpawning&&once.HasFinishedSpawning,"Once batch");once.StartSpawning();Tick(once);Check(once.SpawnedCount==2,"Once repeated");
            var continuous=Make(EnemySpawnPoint.SpawnMode.Continuous,1,2);continuous.totalSpawnLimit=1;continuous.StartSpawning();Tick(continuous);Tick(continuous);Check(continuous.SpawnedCount==2&&continuous.IsSpawning,"Continuous cap or legacy limit");
            var living=(System.Collections.Generic.List<EnemyController>)typeof(EnemySpawnPoint).GetField("living",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(continuous);living[0].IsDead=true;Tick(continuous);Check(continuous.SpawnedCount==3&&continuous.AliveCount==2,"Continuous refill");
            int finished=0;continuous.onSpawningFinished.AddListener(()=>finished++);continuous.PauseSpawning();Tick(continuous);Check(continuous.SpawnedCount==3&&finished==0,"Pause");continuous.StartSpawning();continuous.StopSpawning();continuous.StopSpawning();Tick(continuous);Check(finished==1&&continuous.SpawnedCount==3,"Stop event");
            var finite=Make(EnemySpawnPoint.SpawnMode.FixedCount,2,5,3);int arrivals=0;finite.onEnemySpawned.AddListener(e=>arrivals++);finite.StartSpawning();Tick(finite);Tick(finite);Check(finite.SpawnedCount==3&&arrivals==3&&finite.HasFinishedSpawning&&!finite.IsSpawning,"Finite budget");
            var cap=Make(EnemySpawnPoint.SpawnMode.Once,10,2);cap.StartSpawning();Check(cap.SpawnedCount==2&&!cap.IsSpawning,"Once capacity");
            var legacy=Make(EnemySpawnPoint.SpawnMode.ExistingSettings,1,4);legacy.respawn=true;legacy.spawnOnStart=false;legacy.totalSpawnLimit=2;typeof(EnemySpawnPoint).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(legacy,null);Tick(legacy);Tick(legacy);Tick(legacy);Check(legacy.SpawnedCount==2&&!legacy.IsSpawning,"Legacy schedule");
            finite.minimumInterval=2;finite.maximumInterval=4;for(int i=0;i<200;i++){var n=finite.SampleInterval();Check(n>=2&&n<=4,"Random interval bounds");}finite.randomizeInterval=false;Check(finite.SampleInterval()==2,"Fixed interval");finite.minimumInterval=-1;finite.maximumInterval=-4;Check(finite.SampleInterval()==.1f,"Invalid interval clamp");
            File.WriteAllText("spawn-validation-success.txt","PASS in Unity Play Mode: Once, once capacity, Continuous cap and refill, ignores legacy limit, FixedCount budget and events, pause/resume/stop, legacy compatibility, randomized/fixed/clamped intervals. Actual EnemySpawnPoint tested with built NavMesh and enemy test doubles.");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
