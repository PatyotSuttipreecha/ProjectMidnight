using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Test doubles live only in the isolated project's runtime assembly.
public static class TriggerValidation
{
    static EnemyController Enemy() => new GameObject("TestEnemy").AddComponent<EnemyController>();
    static EnemyDefeatedTrigger Trigger(EnemyDefeatedTrigger.CompletionMode mode,params EnemyController[] enemies)
    {
        var go=new GameObject("TestTrigger");go.SetActive(false);
        var t=go.AddComponent<EnemyDefeatedTrigger>();t.mode=mode;
        if(mode==EnemyDefeatedTrigger.CompletionMode.SpecificEnemy)t.specificEnemy=enemies.Length>0?enemies[0]:null;
        else t.enemies.AddRange(enemies);
        return t;
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        try
        {
            var a=Enemy();var b=Enemy();var single=Trigger(EnemyDefeatedTrigger.CompletionMode.SpecificEnemy,a);int hits=0;
            single.onTriggered.AddListener(()=>{hits++;single.Arm();});single.gameObject.SetActive(true);single.SendMessage("OnEnable");
            b.Kill();Check(hits==0,"Untracked enemy fired trigger");a.Kill();a.Kill();Check(hits==1,"Single/reentrant death fired twice");
            var x=Enemy();var y=Enemy();var all=Trigger(EnemyDefeatedTrigger.CompletionMode.AllTrackedEnemies,x,y,x);all.gameObject.SetActive(true);all.SendMessage("OnEnable");
            Check(all.TrackedCount==2,"Duplicates counted");x.Kill();Check(!all.HasTriggered,"Group fired early");
            all.enabled=false;y.Kill();Check(!all.HasTriggered,"Disabled trigger fired");all.enabled=true;all.SendMessage("OnEnable");Check(all.HasTriggered,"Disabled death was lost");
            var empty=Trigger(EnemyDefeatedTrigger.CompletionMode.AllTrackedEnemies);empty.gameObject.SetActive(true);empty.SendMessage("OnEnable");Check(!empty.HasTriggered,"Empty group fired");
            var wave=Trigger(EnemyDefeatedTrigger.CompletionMode.AllTrackedEnemies);wave.groupSealedOnStart=false;wave.gameObject.SetActive(true);wave.SendMessage("OnEnable");var w=Enemy();wave.RegisterEnemy(w);w.Kill();Check(!wave.HasTriggered,"Unsealed wave fired");wave.SealGroup();Check(wave.HasTriggered,"Sealed wave did not fire");
            var live=Enemy();var destroyed=Trigger(EnemyDefeatedTrigger.CompletionMode.SpecificEnemy,live);destroyed.gameObject.SetActive(true);destroyed.SendMessage("OnEnable");UnityEngine.Object.DestroyImmediate(live.gameObject);destroyed.Arm();Check(!destroyed.HasTriggered,"Destroy counted as death");
            var dead=Enemy();dead.Kill();var disarmed=Trigger(EnemyDefeatedTrigger.CompletionMode.SpecificEnemy,dead);disarmed.armed=false;disarmed.gameObject.SetActive(true);disarmed.SendMessage("OnEnable");Check(!disarmed.HasTriggered,"Disarmed fired");disarmed.Arm();Check(disarmed.HasTriggered,"Existing death did not fire on Arm");
            File.WriteAllText("trigger-validation-success.txt","PASS: single, unrelated enemy, duplicate death, reentrant callback, group, duplicate membership, disabled death, empty group, wave seal, destroyed living enemy, pre-dead and Arm. Production trigger compiled against test doubles; gameplay death/Timeline requires scene Play Mode verification.");
            EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}

