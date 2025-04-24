using UnityEngine;
using UnityEngine.Audio;

public class SoundPlay : MonoBehaviour
{
    [Header("Vertical Move")]
    public AudioClip splashIn;
    public AudioClip splashInSlow;
    public AudioClip splashOut;
    public AudioClip splashOutSlow;
    public AudioClip baitSinking;

    public float surfaceSplashCooldown = 1f; // enter & exit water sound
    public float surfaceSplashSpeedCutoff = 1.5f;

    [Header("Horizontal Move")]
    public AudioClip[] hzUnderwater;
    public AudioClip hzWaterSurfaceSlow;
    public AudioClip hzWaterSurfaceFast;

    public float hzMoveSpeedCooldown = 2f; // in-water movement sound
    public float hzMoveSpeedCutoff = 1.0f;

    [Header("EQ Settings")]
    public AudioMixer underwaterEQ;
    protected AudioSource audioSource; // play trigger events sounds, e.g. enter water surface

    [Header("Env Settings")]
    // controls under/in-water sound sources
    // switch on & off, eq, effects.
    private AudioSource radioSource; // on main camera
    private AudioSource seafoamSource; // on env sound object
    private AudioSource heartbeatSource; // on env sound

    public float maxRadioDistance = 1.8f;
    public float boidRespawnCooldown = 10f; // reactivate morse code object after seconds

    private Transform waterSurfaceTransform;
    private Transform heartbeatTransform;
    private Transform morseTransform; // moving morse code source

    private float lastSplashTime = -999f; // enter & exit water sound
    private float lastHorizontalSplashTime = -999f; // in-water movement sound

    [Header("Tracker Settings")]
    // store underwater status
    public bool isUnderwater { get; private set; }
    public float underwaterDepth { get; private set; }

    private Vector3 lastPosition;
    private Vector3 velocity;
    private float boidReleaseSince = float.PositiveInfinity;

    void Start()
    {
        SetUpAudioMixer();
        SetUpEnvSound();
        SetupMorseCodeSound();

        lastPosition = transform.position;

        // @todo: if underwater, play bubble sound when xy movement > 0.2f; play soft splash when xy movement > 0.5f;
        // @todo: if at surface, play splash soound when xy movement > 0.3f, and < 0.5f;
    }

    // @todo: expose parameter, change audio mix dynamically
    // @param: pitch, fade, frequency (all based on depth)
    //
    void SetUpAudioMixer()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        AudioMixerGroup[] groups = underwaterEQ.FindMatchingGroups("EQ-Underwater");

        if (groups.Length > 0)
        {
            audioSource.outputAudioMixerGroup = groups[0];
            return;
        }
        Debug.LogError("Audio mixer underwater is not found. Did you change the name?");
    }

    void SetUpEnvSound()
    {
        radioSource = Camera.main.GetComponent<AudioSource>();
        if (radioSource == null) { Debug.LogError("Radio sound is not attached to main camera!"); }

        waterSurfaceTransform = GameObject.FindWithTag("WaterSurface").transform;
        if (waterSurfaceTransform == null) { Debug.LogError("waterSurfaceTransform is not found. Did you change the name?"); }

        seafoamSource = GameObject.Find("seafoam").GetComponent<AudioSource>();
        if (seafoamSource == null) { Debug.LogError("Seafoam sound is not found. Did you change the name?"); }

        heartbeatTransform = GameObject.Find("heartbeat").transform;
        if (heartbeatTransform == null) { Debug.LogError("Heartbeat transform is not found. Did you change the name?"); }

        heartbeatSource = heartbeatTransform.GetComponent<AudioSource>();
        if (heartbeatSource == null) { Debug.LogError("Heartbeat sound is not found. Did you change the name?"); }
    }

    void SetupMorseCodeSound()
    {
        morseTransform = GameObject.Find("morsecode").transform;
        if (morseTransform == null) { Debug.LogError("Morse code transform is not found. Did you change the name?"); }

        morseTransform.GetComponent<Boid>().InitializePosition();
    }

    void Update()
    {
        // 計算物體的速度
        velocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;

        // 计算是否在水下
        isUnderwater = transform.position.y < waterSurfaceTransform.position.y;
        underwaterDepth = Mathf.Clamp01(waterSurfaceTransform.position.y - transform.position.y);

        UpdateRadioSource();
        UpdateSeafoamSource();
        UpdateHeartbeatSource();
        UpdateMorseCodeSource();
        UpdateHorizontalMove();
    }

    void UpdateRadioSource()
    {
        // only plays underwater
        if (!isUnderwater) 
        {
            // Debug.Log("not underwater, not playing radio.");
            radioSource.volume = 0f;
            radioSource.Stop();
            return;
        }
        if (!radioSource.isPlaying) radioSource.Play();

        // 根据距离调整 radio 的音量
        float dist = Vector3.Distance(transform.position, Camera.main.transform.position);
        float distRatio = Mathf.Clamp01(1f - (dist / maxRadioDistance)) * 0.4f;
        radioSource.volume = distRatio;
        radioSource.pitch = 2f - Mathf.Clamp01(underwaterDepth / 2f);
        // Debug.Log("underwater, radio distance: " + dist + ", max: " + maxRadioDistance + ", volume: " + radioSource.volume);
    }

    void UpdateSeafoamSource()
    {
        // always playing, but adjust vol & pitch
        if (!seafoamSource.isPlaying) seafoamSource.Play();

        // 根据水面的距离调整音量
        if (isUnderwater) 
        {
            float dist = underwaterDepth;
            float distRatio = Mathf.Clamp01(1f - (dist / 1.2f));
            seafoamSource.volume = 0.3f * distRatio;
            seafoamSource.pitch = 0.3f * distRatio;
        } else {
            seafoamSource.volume = 0.3f;
            seafoamSource.pitch = 0.8f;
        }
    }

    void UpdateHeartbeatSource()
    {
        if (!isUnderwater)
        {
            heartbeatSource.volume = 0f;
            heartbeatSource.Stop();
            return;
        }
        if (!heartbeatSource.isPlaying) heartbeatSource.Play();

        // ???
        // 深度始终保持一致
        Vector3 pos = heartbeatTransform.position;
        pos.z = transform.position.z;
        heartbeatTransform.position = pos;
    }

    void UpdateMorseCodeSource()
    {
        // if boid is inactive, store its time for later reactivation
        if (!morseTransform.gameObject.activeSelf) 
        {
            if (boidReleaseSince == float.PositiveInfinity)
            {
                boidReleaseSince = Time.time;
            }
            else if (Time.time - boidReleaseSince >= boidRespawnCooldown) 
            {
                Debug.Log("Player: Reactivate boid object!");

                boidReleaseSince = float.PositiveInfinity;
                morseTransform.gameObject.SetActive(true);
                morseTransform.GetComponent<Boid>().InitializePosition();
            }
            return;
        }
        // otherwise, move it
        morseTransform.GetComponent<Boid>().UpdatePosition();
    }

    // 碰击墙壁
    void OnCollisionEnter(Collision collision)
    {
        AudioClip clipToPlay = null;

        // Debug.Log("hit rigid object.");

        // @todo:
        //  play metal sound

        if (clipToPlay != null)
        {
            audioSource.PlayOneShot (clipToPlay);
        }
    }

    // 进入水面
    void OnTriggerEnter(Collider collision)
    {
        AudioClip clipToPlay = null;

        // Debug.Log("hit invisible object.");
        float now = Time.time;

        if (collision.gameObject.CompareTag("WaterSurface"))
        {
            // Debug.Log("hit water with vel: " + velocity.y);

            // last splash too fast, do nothing
            if (now - lastSplashTime < surfaceSplashCooldown)
            {
                return;
            }
            // 如果物體垂直速度為負，表示是從上往下進水
            if (velocity.y < 0)
            {
                if (Mathf.Abs(velocity.y) < surfaceSplashSpeedCutoff)
                {
                    clipToPlay = splashInSlow;
                }
                else
                {
                    clipToPlay = splashIn;
                }
                // Debug.Log("Entering water: playing splashIn");
            }
            // 如果物體垂直速度為正，表示是從下往上出水
            else if (velocity.y > 0)
            {
                if (Mathf.Abs(velocity.y) < surfaceSplashSpeedCutoff)
                {
                    clipToPlay = splashOutSlow;
                }
                else
                {
                    clipToPlay = splashOut;
                }
                // Debug.Log("Exiting water: playing splashOut");
            }
        }

        if (clipToPlay != null)
        {
            Debug.Log("play clip: " + clipToPlay);
            audioSource.PlayOneShot (clipToPlay);
        }

        lastSplashTime = now;
    }

    // 离开水面
    void OnTriggerExit(Collider collision)
    {
        AudioClip clipToPlay = null;

        // Debug.Log("exit invisible object.");

        if (collision.gameObject.CompareTag("WaterSurface"))
        {
            // Debug.Log("hit water with vel: " + velocity.y);

            // 向下离开水面
            if (velocity.y < 0)
            {
                clipToPlay = baitSinking;
                // Debug.Log("Sinking in water: playing sinking sound");
            } 
        }

        if (clipToPlay != null)
        {
            Debug.Log("play clip: " + clipToPlay);
            audioSource.PlayOneShot (clipToPlay);
        }
    }

    // 待在水面上/水中时，横向移动会造成水体声音
    void UpdateHorizontalMove()
    {
        AudioClip clipToPlay = null;

        // last in-water horizontal splash too fast, do nothing
        if (Time.time - lastHorizontalSplashTime < hzMoveSpeedCooldown) return;

        // 横向移动速度
        float speed = velocity.magnitude;

        // 几乎没有移动时，忽略
        if (speed < hzMoveSpeedCutoff * 0.5f || speed < 0.1f) return;

        // 在水下
        if (isUnderwater)
        {
            clipToPlay = hzUnderwater[Random.Range(0, hzUnderwater.Length)];
        }
        // 在水面
        else if (Mathf.Abs(transform.position.y - waterSurfaceTransform.position.y) < 0.2f)
        {
            clipToPlay = speed > hzMoveSpeedCutoff ? hzWaterSurfaceFast : hzWaterSurfaceSlow;
        }

        lastHorizontalSplashTime = Time.time;

        if (clipToPlay != null)
        {
            Debug.Log("play clip: " + clipToPlay);
            audioSource.PlayOneShot (clipToPlay);
        }
    }

    public void PlayOneShot(AudioClip externalClip)
    {
        if (externalClip != null)
        {
            Debug.Log("play external clip: " + externalClip);
            audioSource.PlayOneShot(externalClip);
        }
    }

    // todo: ontriggerstay, if move, play splash soft
}
