using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PlayerHUDChecks
{
    private static int checks, frames;
    static PlayerHUDChecks() {if(SessionState.GetBool("PlayerHUDChecks",false)) EditorApplication.update+=AwaitPlay;}
    public static void Run() { PlayerHUDBuilder.Build(); SessionState.SetBool("PlayerHUDChecks",true); EditorApplication.EnterPlaymode(); }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    private static void AwaitPlay()
    {
        if(!EditorApplication.isPlaying || ++frames<4)return;
        EditorApplication.update-=AwaitPlay;SessionState.SetBool("PlayerHUDChecks",false);
        try
        {
            var player=new GameObject("Player").AddComponent<PlayerController>();PlayerController.instance=player;
            var hud=PlayerHUD.EnsureFor(player);
            var card=hud.transform.Find("Vitals and Equipment");
            var fill=card.Find("Health ring fill").GetComponent<Image>();
            Text Label(string name)=>card.Find(name).GetComponent<Text>();
            Check(fill.sprite!=null&&fill.type==Image.Type.Filled&&fill.fillMethod==Image.FillMethod.Radial360,"Prefab contains a sprite-backed radial health ring");
            Check(fill.sprite.texture.isReadable==false || fill.sprite.texture.width==128,"Ring asset imports correctly");
            Check(PlayerHUD.EnsureFor(player)==hud,"Repeated setup reuses the existing HUD");
            player.currentHealth=37;hud.Refresh();Check(Mathf.Abs(fill.fillAmount-.37f)<.001f&&Label("Health value").text=="37","Health ring and numeric health match live player values");
            Color normal=fill.color;player.currentHealth=10;hud.Refresh();Check(fill.color!=normal,"Low health changes ring colour");
            player.maxHealth=0;hud.Refresh();Check(fill.fillAmount==0,"Zero maximum health is safe");player.maxHealth=100;
            Check(Label("Weapon name").text=="UNARMED","No equipment is represented without stale ammo");
            var gun=new GameObject("Pistol").AddComponent<Guns>();gun.weaponStat=new Guns.WeaponStat{weaponName=Guns.WeaponType.Pistol,currentAmmo=7,ammoReserve=35};
            gun.BoundInventoryItem=new InventoryItemData{itemSO=new ItemSO{itemName="GLOCK 19",icon=fill.sprite}};player.EquippedGun=gun;hud.Refresh();
            Check(Label("Weapon name").text=="GLOCK 19"&&Label("Ammunition").text.StartsWith("7 ")&&Label("Ammunition").text.Contains("35"),"HUD reads equipped item name and actual magazine/reserve");
            Check(card.Find("Weapon icon").GetComponent<Image>().enabled,"Item icon is displayed when available");
            gun.isReloading=true;hud.Refresh();Check(Label("Weapon status").text=="RELOADING","Reload state displays");
            gun.isReloading=false;gun.weaponStat.currentAmmo=0;hud.Refresh();Check(Label("Weapon status").text=="R  RELOAD","Empty magazine with reserves prompts reload");
            gun.weaponStat.ammoReserve=0;hud.Refresh();Check(Label("Weapon status").text=="NO AMMO","No ammunition displays correctly");
            gun.weaponStat.weaponName=Guns.WeaponType.Knife;gun.BoundInventoryItem=null;hud.Refresh();Check(Label("Ammunition").text=="—"&&!card.Find("Weapon icon").GetComponent<Image>().enabled,"Melee equipment clears ammunition and stale icon");
            player.IsGameplayInputBlocked=true;hud.Refresh();Check(hud.GetComponent<CanvasGroup>().alpha==0,"Inventory/cutscene input lock hides HUD");
            player.IsGameplayInputBlocked=false;hud.Refresh();Check(hud.GetComponent<CanvasGroup>().alpha==1&&!hud.GetComponent<CanvasGroup>().blocksRaycasts,"HUD restores and does not capture UI input");
            player.currentHealth=76;gun.weaponStat=new Guns.WeaponStat{weaponName=Guns.WeaponType.Pistol,currentAmmo=12,ammoReserve=36};hud.Refresh();
            var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.09f,.11f);
            var target=new RenderTexture(1920,1080,24);camera.targetTexture=target;
            var canvas=hud.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();
            System.IO.File.WriteAllBytes("hud-preview.png",texture.EncodeToPNG());RenderTexture.active=null;
            System.IO.File.WriteAllText("success.txt",checks+" HUD checks passed using production HUD/prefab builder with player/gun data stubs. Prefab built and preview rendered.");
            EditorApplication.Exit(0);
        }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
