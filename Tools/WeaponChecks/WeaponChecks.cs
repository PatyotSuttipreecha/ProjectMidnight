using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class WeaponChecks
{
    private static int checks;
    static WeaponChecks() { if (SessionState.GetBool("WeaponChecks", false)) EditorApplication.update += AwaitPlay; }
    public static void Run() { SessionState.SetBool("WeaponChecks", true); EditorApplication.EnterPlaymode(); }
    private static void AwaitPlay()
    {
        if (!EditorApplication.isPlaying) return;
        EditorApplication.update -= AwaitPlay; SessionState.SetBool("WeaponChecks", false); Validate();
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }
    private static void Validate()
    {
        try
        {
            var victim = new GameObject("Damage target");
            var hp = victim.AddComponent<HitBoxManager>();
            var enemy = victim.AddComponent<EnemyController>();
            hp.TakeDamage(HitBoxPart.Head, 0);
            hp.TakeDamage(HitBoxPart.Body, -10);
            hp.TakeDamage(HitBoxPart.Head, float.NaN);
            hp.TakeDamage(HitBoxPart.Head, float.PositiveInfinity);
            Check(hp.health == 100 && enemy.deaths == 0 && enemy.impacts == 0, "Zero/invalid damage never invents damage or triggers death/impact");
            hp.TakeDamage(HitBoxPart.Head, 20); Check(hp.health == 65, "Head multiplies configured damage by 1.75");
            hp.TakeDamage(HitBoxPart.Body, 20); Check(hp.health == 45, "Body uses configured damage");
            hp.TakeDamage(HitBoxPart.UpperArm, 20); Check(hp.health == 30, "Upper arm multiplier is preserved");
            var box = victim.AddComponent<BoxCollider>(); victim.AddComponent<HitBox>().hitBox = HitBoxPart.Body;
            var shot = new GameObject("Projectile").AddComponent<Bullet>(); shot.SetDamage(0);
            Check(shot.TryHit(box) && hp.health == 30, "Production Bullet with damage zero leaves health unchanged");
            var damaging = new GameObject("Damaging projectile").AddComponent<Bullet>(); damaging.SetDamage(20);
            int shotId = Bullet.AllocateShotId(); damaging.SetImpactShot(shotId, 40);
            Check(damaging.TryHit(box) && hp.health == 10 && !damaging.TryHit(box), "Production Bullet consumes a hit once");
            Check(enemy.lastShotId == shotId && enemy.lastImpactPower == 40, "Bullet carries shared volley identity and impact power through production HitBoxManager");
            hp.TakeDamage(HitBoxPart.Body, 10);
            Check(hp.health == 0 && enemy.deaths == 1, "Positive damage still kills exactly once");

            var camera = new GameObject("Projection camera").AddComponent<Camera>();
            camera.pixelRect = new Rect(0, 0, 1920, 1080); camera.aspect = 1920f / 1080;
            camera.fieldOfView = 60;
            Rect standard = Guns.ProjectSpreadBounds(camera, Vector3.zero, Vector3.forward, 10, 5, 0);
            float expected = camera.pixelWidth * .5f / camera.aspect * Mathf.Tan(5 * Mathf.Deg2Rad) / Mathf.Tan(30 * Mathf.Deg2Rad);
            Check(Mathf.Abs(standard.width * .5f - expected) < .1f, "Projected yaw extent agrees with perspective FOV mathematics: actual=" + standard + " expected=" + expected + " rect=" + camera.pixelRect + " aspect=" + camera.aspect);
            Check(Mathf.Abs(standard.center.x - camera.pixelRect.center.x) < .1f, "Centered spread is centered on aim");
            camera.fieldOfView = 30;
            Rect zoomed = Guns.ProjectSpreadBounds(camera, Vector3.zero, Vector3.forward, 10, 5, 0);
            Check(zoomed.width > standard.width * 2, "Zoom changes displayed spread instead of using a fixed maximum gap");
            var zero = Guns.ProjectSpreadBounds(camera, new Vector3(.5f, -.4f, 1), (Vector3.forward * 10 - new Vector3(.5f, -.4f, 1)).normalized,
                (Vector3.forward * 10 - new Vector3(.5f, -.4f, 1)).magnitude, 0, 0);
            Check(zero.size.sqrMagnitude < .001f && Vector2.Distance(zero.center, camera.pixelRect.center) < .1f, "Zero spread converges on the target despite muzzle parallax");
            UnityEngine.Random.InitState(918);
            foreach (float fov in new[] {30f, 60f, 85f})
            foreach (float pitch in new[] {-40f, 0f, 40f})
            {
                camera.fieldOfView = fov; camera.transform.rotation = Quaternion.Euler(pitch, 25, 0);
                Vector3 origin = camera.transform.TransformPoint(new Vector3(.5f, -.4f, 1));
                Vector3 target = camera.transform.position + camera.transform.forward * 15;
                Vector3 forward = (target - origin).normalized;
                float distance = Vector3.Distance(origin, target);
                Rect bounds = Guns.ProjectSpreadBounds(camera, origin, forward, distance, 4, 0);
                for (int i = 0; i < 500; i++)
                {
                    Vector3 direction = Guns.AccuracyDirection(forward, UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(-4f, 4f));
                    Vector3 screen = camera.WorldToScreenPoint(origin + direction * (distance / Vector3.Dot(direction, forward)));
                    Check(screen.x >= bounds.xMin - .5f && screen.x <= bounds.xMax + .5f && screen.y >= bounds.yMin - .5f && screen.y <= bounds.yMax + .5f,
                        "Actual shot scatter stays inside displayed projected envelope across pitch/FOV");
                }
                Rect pellets = Guns.ProjectSpreadBounds(camera, origin, forward, distance, 4, 5);
                Check(pellets.width > bounds.width && pellets.height > bounds.height, "Shotgun UI includes its additional pellet cone");
            }

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var root = new GameObject("Crosshair", typeof(RectTransform)); root.transform.SetParent(canvas.transform, false);
            var ui = root.AddComponent<Crosshair>();
            RectTransform Arm(string name) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(root.transform, false); var arm = (RectTransform)go.transform; arm.sizeDelta = new Vector2(10, 10); return arm; }
            ui.left = Arm("Left"); ui.right = Arm("Right"); ui.top = Arm("Top"); ui.bottom = Arm("Bottom");
            foreach (float scale in new[] { 1f, 2f })
            {
                canvas.scaleFactor = scale; Canvas.ForceUpdateCanvases();
                ui.SetProjectedSpread(new Vector2(300, 250), Rect.MinMaxRect(240, 210, 360, 290));
                typeof(Crosshair).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                Vector2 rightEdge = RectTransformUtility.WorldToScreenPoint(null, ui.right.TransformPoint(new Vector3(ui.right.rect.xMin, 0)));
                Vector2 topEdge = RectTransformUtility.WorldToScreenPoint(null, ui.top.TransformPoint(new Vector3(0, ui.top.rect.yMin)));
                Check(Mathf.Abs(rightEdge.x - 360) < .5f && Mathf.Abs(topEdge.y - 290) < .5f, "Crosshair inner edges match screen-space spread at canvas scale " + scale);
            }
            System.IO.File.WriteAllText("success.txt", checks + " weapon checks passed (production damage/Bullet/Crosshair and verbatim Guns projection methods; unrelated enemy/gun dependencies stubbed).");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
