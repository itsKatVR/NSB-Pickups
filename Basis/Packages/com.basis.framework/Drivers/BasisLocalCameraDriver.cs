using Basis.Scripts.BasisSdk.Helpers;
using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Device_Management;
using Basis.Scripts.TransformBinders;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Basis.Scripts.Drivers
{
    public class BasisLocalCameraDriver : MonoBehaviour
    {
        public static bool HasInstance;
        public static BasisLocalCameraDriver Instance;
        public Camera Camera;
        public static int CameraInstanceID;
        public AudioListener Listener;
        public UniversalAdditionalCameraData CameraData;
        public SteamAudio.SteamAudioListener SteamAudioListener;
        public BasisLocalPlayer LocalPlayer;
        public int DefaultCameraFov = 90;
        // Static event to notify when the instance exists
        public static event System.Action InstanceExists;
        public BasisLockToInput BasisLockToInput;
        public bool HasEvents = false;
        public Canvas MicrophoneCanvas;
        public RawImage MicrophoneMutedIcon;
        public RawImage MicrophoneUnMutedIcon;
        public Transform MicrophoneUnMutedIconTransform;

        public Vector3 DesktopMicrophoneOffset = new Vector3(-0.001f, -0.0015f, 2f); // Adjust as needed for canvas position and depth
        public Vector3 VRMicrophoneOffset = new Vector3(-0.0004f, -0.0015f, 2f);

        public AudioClip MuteSound;
        public AudioClip UnMuteSound;
        public AudioSource AudioSource;
        public float NearClip = 0.001f;
        private Coroutine scaleCoroutine;

        public Vector3 StartingScale = Vector3.zero;
        public float duration = 0.35f;
        public float halfDuration;
        public Vector3 largerScale;
        public static Vector3 LeftEye;
        public static Vector3 RightEye;
        
        public Color UnMutedMutedIconColorActive = Color.white;
        public Color UnMutedMutedIconColorInactive = Color.grey;

        public void OnEnable()
        {
            if (BasisHelpers.CheckInstance(Instance))
            {
                Instance = this;
                HasInstance = true;
            }
            LocalPlayer = BasisLocalPlayer.Instance;
            Camera.nearClipPlane = NearClip;
            Camera.farClipPlane = 1500;
            CameraInstanceID = Camera.GetInstanceID();
            //fire static event that says the instance exists
            OnHeightChanged();
            if (HasEvents == false)
            {
                MicrophoneRecorder.OnPausedAction += OnPausedEvent;
                MicrophoneRecorder.MainThreadOnHasAudio += MicrophoneTransmitting;
                MicrophoneRecorder.MainThreadOnHasSilence += MicrophoneNotTransmitting;
                RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
                BasisDeviceManagement.Instance.OnBootModeChanged += OnModeSwitch;
                BasisLocalPlayer.Instance.OnPlayersHeightChanged += OnHeightChanged;
                InstanceExists?.Invoke();
                HasEvents = true;
            }
            halfDuration = duration / 2f; // Time to scale up and down
            StartingScale = MicrophoneMutedIcon.transform.localScale;
            // Target scale for the "bounce" effect (e.g., 1.2 times larger)
            largerScale = StartingScale * 1.2f;
            UpdateMicrophoneVisuals(MicrophoneRecorder.isPaused, false);
        }
        public void MicrophoneTransmitting()
        {
            MicrophoneUnMutedIcon.color = UnMutedMutedIconColorActive;
            MicrophoneUnMutedIconTransform.localScale = largerScale;
        }
        public void MicrophoneNotTransmitting()
        {
            MicrophoneUnMutedIcon.color = UnMutedMutedIconColorInactive;
            MicrophoneUnMutedIconTransform.localScale = StartingScale;
        }

        private void OnPausedEvent(bool IsMuted)
        {
            UpdateMicrophoneVisuals(IsMuted,true);
        }
        public void UpdateMicrophoneVisuals(bool IsMuted, bool PlaySound)
        {
           // BasisDebug.Log(nameof(UpdateMicrophoneVisuals));
            // Cancel the current coroutine if it's running
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }
            if (IsMuted)
            {
               // BasisDebug.Log(nameof(UpdateMicrophoneVisuals) + IsMuted);

                MicrophoneMutedIcon.gameObject.SetActive(true);
                MicrophoneUnMutedIcon.gameObject.SetActive(false);
                if (PlaySound)
                {
                    AudioSource.PlayOneShot(MuteSound);

                }
                // Start a new coroutine for the scale animation
                scaleCoroutine = StartCoroutine(ScaleIcons(MicrophoneMutedIcon.gameObject));
            }
            else
            {
               // BasisDebug.Log(nameof(UpdateMicrophoneVisuals) + IsMuted);

                MicrophoneMutedIcon.gameObject.SetActive(false);
                MicrophoneUnMutedIcon.gameObject.SetActive(true);
                if (PlaySound)
                {
                    AudioSource.PlayOneShot(UnMuteSound);
                }
                // Start a new coroutine for the scale animation
                scaleCoroutine = StartCoroutine(ScaleIcons(MicrophoneUnMutedIcon.gameObject));
            }
        }
        private IEnumerator ScaleIcons(GameObject iconToScale)
        {
            float time = 0f;

            // Phase 1: Scale up
            while (time < halfDuration)
            {
                time += Time.deltaTime;
                float t = time / halfDuration;

                // Scale the icon up
                iconToScale.transform.localScale = Vector3.Lerp(StartingScale, largerScale, t);
                yield return null; // Wait for the next frame
            }

            // Ensure the final scale at the end of phase 1 is set to largerScale
            iconToScale.transform.localScale = largerScale;

            // Reset time for the second phase
            time = 0f;

            // Phase 2: Scale down
            while (time < halfDuration)
            {
                time += Time.deltaTime;
                float t = time / halfDuration;

                // Scale the icon down back to the original scale
                iconToScale.transform.localScale = Vector3.Lerp(largerScale, StartingScale, t);
                yield return null; // Wait for the next frame
            }

            // Ensure the final scale at the end of phase 2 is set to originalScale
            iconToScale.transform.localScale = StartingScale;
        }
        public void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            BasisDeviceManagement.Instance.OnBootModeChanged -= OnModeSwitch;
            BasisLocalPlayer.Instance.OnPlayersHeightChanged -= OnHeightChanged;
            MicrophoneRecorder.OnPausedAction -= OnPausedEvent;
            HasEvents = false;
            HasInstance = false;
        }
        private void OnModeSwitch(string mode)
        {
            if (mode == BasisDeviceManagement.Desktop)
            {
                Camera.fieldOfView = DefaultCameraFov;
            }
            OnHeightChanged();
        }
        public static Vector3 Forward()
        {
            if (HasInstance)
            {
                return Instance.transform.forward;
            }
            else
            {
                return Vector3.zero;
            }
        }
        public static Vector3 Up()
        {
            if (HasInstance)
            {
                return Instance.transform.up;
            }
            else
            {
                return Vector3.zero;
            }
        }
        public static Vector3 Right()
        {
            if (HasInstance)
            {
                return Instance.transform.right;
            }
            else
            {
                return Vector3.zero;
            }
        }
        public static Vector3 Position()
        {
            if (HasInstance)
            {
                return Instance.transform.position;
            }
            else
            {
                return Vector3.zero;
            }
        }
        public static Vector3 LeftEyePosition()
        {
            if (BasisDeviceManagement.IsUserInDesktop())
            {
                return Instance.transform.position;
            }
            else
            {
                return LeftEye;
            }
        }
        public static Vector3 RightEyePosition()
        {
            if (BasisDeviceManagement.IsUserInDesktop())
            {
                return Instance.transform.position;
            }
            else
            {
                return RightEye;
            }
        }
        public static Quaternion Rotation()
        {
            if (HasInstance)
            {
                return Instance.transform.rotation;
            }
            else
            {
                return Quaternion.identity;
            }
        }
        public static void GetPositionAndRotation(out Vector3 Position,out Quaternion Rotation)
        {
            if (HasInstance)
            {
                 Instance.transform.GetPositionAndRotation(out Position,out Rotation);
            }
            else
            {
                Position = Vector3.zero;
                Rotation = Quaternion.identity;
            }
        }
        public void OnHeightChanged()
        {
            this.gameObject.transform.localScale = Vector3.one * LocalPlayer.EyeRatioAvatarToAvatarDefaultScale;
        }
        public void OnDisable()
        {
            if (LocalPlayer.AvatarDriver && LocalPlayer.AvatarDriver.References != null && LocalPlayer.AvatarDriver.References.head != null)
            {
                LocalPlayer.AvatarDriver.References.head.localScale = LocalPlayer.AvatarDriver.HeadScale;
            }
            if (HasEvents)
            {
                RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
                BasisDeviceManagement.Instance.OnBootModeChanged -= OnModeSwitch;
                MicrophoneRecorder.MainThreadOnHasAudio -= MicrophoneTransmitting;
                MicrophoneRecorder.MainThreadOnHasSilence -= MicrophoneNotTransmitting;
                HasEvents = false;
            }
        }
        public void BeginCameraRendering(ScriptableRenderContext context, Camera Camera)
        {
            if (LocalPlayer.HasAvatarDriver && LocalPlayer.AvatarDriver.References.Hashead)
            {
                if (Camera.GetInstanceID() == CameraInstanceID)
                {
                    ScaleheadToZero();
                    if (CameraData.allowXRRendering)
                    {
                        Vector2 EyeTextureSize = new Vector2(XRSettings.eyeTextureWidth, XRSettings.eyeTextureHeight);
                        MicrophoneCanvas.transform.localPosition = CalculatePosition(EyeTextureSize, VRMicrophoneOffset);
                    }
                    else
                    {
                        MicrophoneCanvas.transform.localPosition = Camera.ViewportToScreenPoint(DesktopMicrophoneOffset);
                    }
                }
                else
                {
                    ScaleHeadToNormal();
                }
            }
        }
        public void ScaleHeadToNormal()
        {
            if (LocalPlayer.AvatarDriver.References.head.localScale != LocalPlayer.AvatarDriver.HeadScale)
            {
                LocalPlayer.AvatarDriver.References.head.localScale = LocalPlayer.AvatarDriver.HeadScale;
            }
        }
        public void ScaleheadToZero()
        {
            if (LocalPlayer.AvatarDriver.References.head.localScale != LocalPlayer.AvatarDriver.HeadScaledDown)
            {
                LocalPlayer.AvatarDriver.References.head.localScale = LocalPlayer.AvatarDriver.HeadScaledDown;
            }
        }
        // Function to calculate the position
        Vector3 CalculatePosition(Vector2 size, Vector3 percentage)
        {
            // The center of the object is assumed to be at (0, 0, 0) for simplicity
            Vector3 center = size/2;

            // Calculate position relative to the center based on the percentage and size
            Vector3 offset = new Vector3((percentage.x - 0.5f) * size.x,(percentage.y - 0.5f) * size.y, percentage.z);

            // The position is the center plus the offset
            return offset + center;
        }
    }
}