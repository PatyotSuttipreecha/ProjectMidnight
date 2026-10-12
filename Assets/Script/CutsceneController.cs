using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
[AddComponentMenu("Gameplay/Cutscene Controller")]
public class CutsceneController : MonoBehaviour
{
    [Tooltip("Optional. Empty finds the active PlayerController.")]
    public PlayerController player;
    public bool finishAndRelease = true;
    [Header("Screen Transition")]
    public bool useFade = true;
    [Min(0)] public float fadeToBlackDuration = .35f;
    [Min(0)] public float fadeFromBlackDuration = .35f;
    public Color fadeColor = Color.black;
    public UnityEvent onStarted = new UnityEvent();
    public UnityEvent onFinished = new UnityEvent();
    public bool IsPlaying { get; private set; }
    private PlayableDirector director;
    private PlayerController lockedPlayer;
    private CanvasGroup fadeOverlay;
    private Coroutine transition;
    private bool entering, finishing;

    private void OnEnable()
    {
        director = GetComponent<PlayableDirector>();
        director.played += OnPlayed; director.stopped += OnStopped;
        if (director.state == PlayState.Playing) OnPlayed(director);
    }
    public void Play()
    {
        if (!isActiveAndEnabled || IsPlaying) return;
        if (director == null) director = GetComponent<PlayableDirector>();
        if (director.playableAsset == null) { Debug.LogWarning("Assign a Timeline asset before playing the cutscene.", this); return; }
        var actor = ResolvePlayer();
        if (actor == null || actor.currentHealth <= 0 || actor.IsGameplayInputBlocked) return;
        if (finishAndRelease) director.extrapolationMode = DirectorWrapMode.None;
        Begin(actor);
        if (!isActiveAndEnabled || !IsPlaying || finishing) return;
        entering = true;
        transition = StartCoroutine(Enter());
    }
    private IEnumerator Enter()
    {
        yield return Fade(1, fadeToBlackDuration);
        director.time = 0; director.Play(); director.Evaluate();
        yield return Fade(0, fadeFromBlackDuration);
        entering = false; transition = null;
    }
    private void Update()
    {
        // Cover the last frame before Timeline releases camera/animation bindings.
        if (IsPlaying && !entering && !finishing && finishAndRelease && director.state == PlayState.Playing &&
            !double.IsInfinity(director.duration) && director.duration > 0 &&
            director.time >= director.duration - (useFade ? fadeToBlackDuration : 0)) Stop();
    }
    public void Stop()
    {
        if (!IsPlaying || finishing) return;
        finishing = true;
        if (transition != null) StopCoroutine(transition);
        entering = false;
        transition = StartCoroutine(Exit());
    }
    private IEnumerator Exit()
    {
        yield return Fade(1, fadeToBlackDuration);
        director.Stop();
        // Evaluate released camera bindings before revealing gameplay.
        yield return null;
        yield return Fade(0, fadeFromBlackDuration);
        transition = null; Finish();
    }
    private IEnumerator Fade(float alpha, float duration)
    {
        if (!useFade) { if (fadeOverlay != null) fadeOverlay.alpha = 0; yield break; }
        if (fadeOverlay == null)
        {
            var root = new GameObject("Cutscene Screen Fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            fadeOverlay = root.GetComponent<CanvasGroup>(); fadeOverlay.alpha = 0;
            fadeOverlay.blocksRaycasts = false; fadeOverlay.interactable = false;
            var image = new GameObject("Fade Color", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(root.transform, false);
            var rect = (RectTransform)image.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = fadeColor; image.GetComponent<Image>().raycastTarget = false;
        }
        float start = fadeOverlay.alpha, elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(start, alpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        fadeOverlay.alpha = alpha;
    }
    private PlayerController ResolvePlayer() => player != null && player.isActiveAndEnabled ? player : FindFirstObjectByType<PlayerController>();
    private void OnPlayed(PlayableDirector source)
    {
        if (finishAndRelease) source.extrapolationMode = DirectorWrapMode.None;
        if (!IsPlaying) Begin(ResolvePlayer());
    }
    private void Begin(PlayerController actor)
    {
        if (IsPlaying || actor == null || !actor.isActiveAndEnabled) return;
        lockedPlayer = actor; lockedPlayer.LockGameplayInput(this); IsPlaying = true;
        onStarted.Invoke();
    }
    private void OnStopped(PlayableDirector source) { if (!finishing) Stop(); }
    private void Finish()
    {
        if (!IsPlaying) return;
        IsPlaying = false; entering = finishing = false;
        if (lockedPlayer != null) lockedPlayer.UnlockGameplayInput(this);
        lockedPlayer = null; onFinished.Invoke();
    }
    private void OnDisable()
    {
        if (transition != null) { StopCoroutine(transition); transition = null; }
        if (director != null)
        {
            director.played -= OnPlayed; director.stopped -= OnStopped;
            if (IsPlaying) director.Stop();
        }
        if (fadeOverlay != null) { Destroy(fadeOverlay.gameObject); fadeOverlay = null; }
        Finish();
    }
}
