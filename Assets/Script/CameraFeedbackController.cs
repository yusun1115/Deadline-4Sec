using UnityEngine;

namespace Deadline4Sec
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFeedbackController : MonoBehaviour
    {
        [System.Serializable]
        private struct ShakeSettings
        {
            [Min(0f)] public float intensity;
            [Min(0f)] public float duration;
        }

        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private RunManager runManager;
        [SerializeField] private GameFlowManager gameFlow;

        [Header("Hit stop (real-time seconds)")]
        [SerializeField, Range(0f, 0.1f)] private float normalKillHitStop = 0.04f;
        [SerializeField, Range(0f, 0.1f)] private float homingKillHitStop = 0.045f;
        [SerializeField, Range(0f, 0.12f)] private float stompHitStop = 0.06f;

        [Header("Ground Slam Impact")]
        [SerializeField, Range(0f, 0.15f)] private float groundSlamHitStop = 0.075f;
        [SerializeField] private ShakeSettings groundSlamShake =
            new ShakeSettings { intensity = 0.2f, duration = 0.22f };
        [SerializeField] private float groundSlamFovKick = -3f;
        [SerializeField, Min(0.01f)] private float groundSlamFovDuration = 0.16f;

        [Header("Camera shake")]
        [SerializeField] private ShakeSettings normalKillShake =
            new ShakeSettings { intensity = 0.055f, duration = 0.09f };
        [SerializeField] private ShakeSettings homingKillShake =
            new ShakeSettings { intensity = 0.075f, duration = 0.11f };
        [SerializeField] private ShakeSettings stompShake =
            new ShakeSettings { intensity = 0.14f, duration = 0.16f };
        [SerializeField] private ShakeSettings nearMissShake =
            new ShakeSettings { intensity = 0.025f, duration = 0.07f };
        [SerializeField, Min(1f)] private float shakeFrequency = 32f;

        [Header("Speed FOV")]
        [SerializeField, Range(30f, 100f)] private float baseSpeedFov = 60f;
        [SerializeField, Range(30f, 110f)] private float maxSpeedFov = 70f;
        [SerializeField, Min(0.1f)] private float fovSmoothSpeed = 5f;

        [Header("Homing start FOV")]
        [SerializeField, Min(0f)] private float homingFovBoost = 2f;
        [SerializeField, Min(0.01f)] private float homingFovDuration = 0.12f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private bool introActive;
        private float introElapsed;
        private float introDuration;
        private readonly Vector3 introSidePosition = new Vector3(6f, 2.4f, -0.5f);
        private float shakeIntensity;
        private float shakeTimeRemaining;
        private float shakeInitialDuration;
        private float noiseSeedX;
        private float noiseSeedY;
        private float temporaryFov;
        private float temporaryFovTimeRemaining;
        private float temporaryFovStart;
        private float temporaryFovDuration;
        private bool hitStopActive;
        private float hitStopUntil;
        private float timeScaleBeforeHitStop = 1f;
        private AudioSource feedbackAudio;
        private AudioClip movementClip;
        private AudioClip homingClip;
        private AudioClip killClip;
        private AudioClip stompClip;
        private AudioClip slamClip;
        private AudioClip nearMissClip;
        private AudioClip warningClip;
        private AudioClip gameOverClip;
        private AudioClip uiClip;
        private CombatVisualFeedback visualFeedback;

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();
            if (runManager == null)
                runManager = FindFirstObjectByType<RunManager>();
            if (gameFlow == null)
                gameFlow = FindFirstObjectByType<GameFlowManager>();

            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            noiseSeedX = 17.31f;
            noiseSeedY = 93.77f;
            if (targetCamera != null)
                targetCamera.fieldOfView = baseSpeedFov;
            SetupAudio();
            visualFeedback = GetComponent<CombatVisualFeedback>();
            if (visualFeedback == null)
                visualFeedback = gameObject.AddComponent<CombatVisualFeedback>();
        }

        private void Update()
        {
            if (hitStopActive && Time.realtimeSinceStartup >= hitStopUntil)
                EndHitStop();

            if (gameFlow == null || !gameFlow.IsPlaying)
            {
                if (hitStopActive || shakeTimeRemaining > 0f || temporaryFov > 0f)
                    StopAllFeedback();
                return;
            }

            float unscaledDelta = Time.unscaledDeltaTime;
            if (shakeTimeRemaining > 0f)
                shakeTimeRemaining = Mathf.Max(0f, shakeTimeRemaining - unscaledDelta);
            if (temporaryFovTimeRemaining > 0f)
            {
                temporaryFovTimeRemaining = Mathf.Max(0f,
                    temporaryFovTimeRemaining - unscaledDelta);
                temporaryFov = temporaryFovStart *
                    (temporaryFovTimeRemaining / Mathf.Max(0.001f, temporaryFovDuration));
            }
            else
            {
                temporaryFov = 0f;
            }

            UpdateFov(unscaledDelta);
        }

        private void LateUpdate()
        {
            if (introActive)
            {
                introElapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float blend = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(introElapsed / introDuration));
                Quaternion sideRotation = Quaternion.LookRotation(
                    new Vector3(0f, 1f, 0f) - introSidePosition, Vector3.up);
                transform.localPosition = Vector3.Lerp(introSidePosition, baseLocalPosition, blend);
                transform.localRotation = Quaternion.Slerp(sideRotation, baseLocalRotation, blend);
                return;
            }
            if (gameFlow == null || !gameFlow.IsPlaying || shakeTimeRemaining <= 0f)
            {
                transform.localPosition = baseLocalPosition;
                return;
            }

            float fade = shakeTimeRemaining / Mathf.Max(0.001f, shakeInitialDuration);
            float sampleTime = Time.unscaledTime * shakeFrequency;
            float x = Mathf.PerlinNoise(noiseSeedX, sampleTime) * 2f - 1f;
            float y = Mathf.PerlinNoise(noiseSeedY, sampleTime) * 2f - 1f;
            transform.localPosition = baseLocalPosition +
                new Vector3(x, y, 0f) * shakeIntensity * fade;
        }

        public void BeginRunIntro(float duration)
        {
            introActive = true;
            introElapsed = 0f;
            introDuration = Mathf.Max(0.1f, duration);
            transform.localPosition = introSidePosition;
            transform.localRotation = Quaternion.LookRotation(
                new Vector3(0f, 1f, 0f) - introSidePosition, Vector3.up);
        }

        public void CompleteRunIntro()
        {
            introActive = false;
            transform.localPosition = baseLocalPosition;
            transform.localRotation = baseLocalRotation;
        }

        public void PlayEnemyKill(string attackName, Vector3 impactPoint)
        {
            if (!CanPlayFeedback())
                return;

            visualFeedback.PlayKill(impactPoint, attackName == "Stomp Attack",
                attackName == "Homing Dash Attack");

            if (attackName == "Stomp Attack")
            {
                PlayShake(stompShake);
                PlayHitStop(stompHitStop);
                PlaySound(stompClip);
                Vibrate(55, 150);
            }
            else if (attackName == "Homing Dash Attack")
            {
                PlayShake(homingKillShake);
                PlayHitStop(homingKillHitStop);
                PlaySound(killClip);
                Vibrate(30, 100);
            }
            else
            {
                PlayShake(normalKillShake);
                PlayHitStop(normalKillHitStop);
                PlaySound(killClip);
                Vibrate(22, 70);
            }
        }

        public void PlayHomingStart(Vector3 start, Vector3 target)
        {
            if (!CanPlayFeedback())
                return;
            PlayFovPulse(homingFovBoost, homingFovDuration);
            visualFeedback.PlayHoming(start, target);
            PlaySound(homingClip);
        }

        public void PlayGroundSlamImpact(Vector3 point, float radius)
        {
            if (!CanPlayFeedback())
                return;

            PlayShake(groundSlamShake);
            visualFeedback.PlaySlam(point, radius);
            PlayHitStop(groundSlamHitStop);
            PlayFovPulse(groundSlamFovKick, groundSlamFovDuration);
            PlaySound(slamClip);
            Vibrate(90, 220);
        }

        public void PlayNearMiss(Vector3 point)
        {
            if (CanPlayFeedback())
            {
                PlayShake(nearMissShake);
                visualFeedback.PlayNearMiss(point);
                PlaySound(nearMissClip);
                Vibrate(15, 45);
            }
        }

        public void PlayMovement()
        {
            if (CanPlayFeedback())
                PlaySound(movementClip);
        }

        public void PlayTimerWarning()
        {
            if (CanPlayFeedback())
            {
                PlaySound(warningClip);
                Vibrate(35, 100);
            }
        }

        public void PlayGameOver()
        {
            PlaySound(gameOverClip);
            Vibrate(110, 200);
        }

        public void PlayUIClick()
        {
            PlaySound(uiClip);
        }

        private void SetupAudio()
        {
            feedbackAudio = GetComponent<AudioSource>();
            if (feedbackAudio == null)
                feedbackAudio = gameObject.AddComponent<AudioSource>();
            feedbackAudio.playOnAwake = false;
            feedbackAudio.spatialBlend = 0f;
            feedbackAudio.volume = 0.5f;
            movementClip = CreateTone("Move", 390f, 0.055f);
            homingClip = CreateTone("Homing", 780f, 0.1f);
            killClip = CreateTone("Kill", 620f, 0.085f);
            stompClip = CreateTone("Stomp", 180f, 0.13f);
            slamClip = CreateTone("Ground Slam", 100f, 0.24f);
            nearMissClip = CreateTone("Near Miss", 910f, 0.11f);
            warningClip = CreateTone("Timer Warning", 260f, 0.18f);
            gameOverClip = CreateTone("Game Over", 135f, 0.34f);
            uiClip = CreateTone("UI Click", 560f, 0.045f);
        }

        private static AudioClip CreateTone(string name, float frequency, float duration)
        {
            const int sampleRate = 22050;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float progress = i / (float)count;
                float envelope = Mathf.Min(1f, progress * 30f) *
                    Mathf.Min(1f, (1f - progress) * 12f);
                float phase = 2f * Mathf.PI * frequency * i / sampleRate;
                samples[i] = Mathf.Sin(phase) * envelope * 0.3f;
            }
            AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlaySound(AudioClip clip)
        {
            if (GamePreferences.SoundEnabled && feedbackAudio != null && clip != null)
                feedbackAudio.PlayOneShot(clip);
        }

        private static void Vibrate(long milliseconds, int amplitude)
        {
            if (!GamePreferences.VibrationEnabled)
                return;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass unityPlayer =
                new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>(
                "getSystemService", "vibrator"))
            {
                if (vibrator == null)
                    return;
                using (AndroidJavaClass version =
                    new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (version.GetStatic<int>("SDK_INT") < 26)
                    {
                        vibrator.Call("vibrate", milliseconds);
                        return;
                    }
                }
                using (AndroidJavaClass vibrationEffect =
                    new AndroidJavaClass("android.os.VibrationEffect"))
                using (AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>(
                    "createOneShot", milliseconds, amplitude))
                    vibrator.Call("vibrate", effect);
            }
#endif
        }

        public void StopAllFeedback()
        {
            if (visualFeedback != null)
                visualFeedback.Clear();
            CompleteRunIntro();
            shakeTimeRemaining = 0f;
            shakeIntensity = 0f;
            shakeInitialDuration = 0f;
            temporaryFov = 0f;
            temporaryFovTimeRemaining = 0f;
            temporaryFovStart = 0f;
            temporaryFovDuration = 0f;
            transform.localPosition = baseLocalPosition;
            if (targetCamera != null)
                targetCamera.fieldOfView = baseSpeedFov;
            EndHitStop(true);
        }

        private bool CanPlayFeedback()
        {
            return gameFlow != null && gameFlow.IsPlaying;
        }

        private void PlayShake(ShakeSettings settings)
        {
            if (shakeTimeRemaining <= 0f)
                shakeInitialDuration = settings.duration;
            else
                shakeInitialDuration = Mathf.Max(shakeInitialDuration, settings.duration);
            shakeIntensity = Mathf.Max(shakeIntensity, settings.intensity);
            shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, settings.duration);
        }

        private void PlayHitStop(float duration)
        {
            if (duration <= 0f)
                return;
            if (!hitStopActive)
            {
                timeScaleBeforeHitStop = Time.timeScale > 0f ? Time.timeScale : 1f;
                hitStopActive = true;
                Time.timeScale = 0f;
            }
            hitStopUntil = Mathf.Max(hitStopUntil, Time.realtimeSinceStartup + duration);
        }

        private void PlayFovPulse(float amount, float duration)
        {
            temporaryFov = amount;
            temporaryFovStart = amount;
            temporaryFovDuration = Mathf.Max(0.001f, duration);
            temporaryFovTimeRemaining = temporaryFovDuration;
        }

        private void EndHitStop(bool forceNormalTime = false)
        {
            if (!hitStopActive && !forceNormalTime)
                return;
            hitStopActive = false;
            hitStopUntil = 0f;
            Time.timeScale = forceNormalTime ? 1f : timeScaleBeforeHitStop;
        }

        private void UpdateFov(float unscaledDelta)
        {
            if (targetCamera == null)
                return;

            float speedT = 0f;
            if (runManager != null)
            {
                float range = Mathf.Max(0.01f,
                    runManager.MaxForwardSpeed - runManager.BaseForwardSpeed);
                speedT = Mathf.Clamp01((runManager.CurrentForwardSpeed -
                    runManager.BaseForwardSpeed) / range);
            }
            float speedFov = Mathf.Lerp(baseSpeedFov, maxSpeedFov, speedT);
            float targetFov = speedFov + temporaryFov;
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView,
                targetFov, 1f - Mathf.Exp(-fovSmoothSpeed * unscaledDelta));
        }

        private void OnDisable()
        {
            StopAllFeedback();
        }

        private void OnDestroy()
        {
            Destroy(movementClip);
            Destroy(homingClip);
            Destroy(killClip);
            Destroy(stompClip);
            Destroy(slamClip);
            Destroy(nearMissClip);
            Destroy(warningClip);
            Destroy(gameOverClip);
            Destroy(uiClip);
        }

        private void OnValidate()
        {
            maxSpeedFov = Mathf.Max(baseSpeedFov, maxSpeedFov);
        }
    }
}
