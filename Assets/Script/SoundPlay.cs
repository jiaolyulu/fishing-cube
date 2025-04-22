using UnityEngine;
using UnityEngine.Audio;

public class SoundPlay : MonoBehaviour
{
    [Header("Sound Clips")]
    public AudioClip splashIn;
    public AudioClip splashInSlow;
    public AudioClip splashOut;
    public AudioClip splashOutSlow;
    public AudioClip waterFlow;
    public AudioClip baitSinking;
    public AudioClip poleClicking; // @todo: fade out when sinking

    [Header("EQ Settings")]
    public AudioMixer underwaterEQ;

    protected AudioSource audioSource;
    private Vector3 lastPosition;
    private Vector3 velocity;

    [Header("Underwater Radio")]
    // controls how under/in-water sound sources
    // switch on & off, eq, effects.
    private AudioSource radioSource; // on main camera
    public float maxRadioDistance = 1.8f;
    private AudioSource seafoamSource; // on env sound object
    private AudioSource heartbeatSource; // on env sound

    [Header("Other Settings")]
    // configs underwater status
    private float lastCollisionTime = -999f;
    public float waterSurfaceSplashCooldown = 1f;
    private bool isUnderwater = false;
    private float underwaterDepth = 0f;
    private Transform waterSurfaceTransform;
    private Transform heartbeatTransform;

    void Start()
    {
        SetUpAudioMixer();
        SetUpEnvSound();

        lastPosition = transform.position;
    }

    // @todo: Expose parameter, change audio mix dynamiclly
    //
    // @param: pitch (<depth), fade (<depth), frequency (<depth)
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

        seafoamSource = GameObject.Find("seafoam").GetComponent<AudioSource>();
        waterSurfaceTransform = GameObject.FindWithTag("WaterSurface").transform;
        if (seafoamSource == null) { Debug.LogError("Seafoam sound is not found. Did you change the name?"); }

        heartbeatSource = GameObject.Find("heartbeat").GetComponent<AudioSource>();
        heartbeatTransform = GameObject.Find("heartbeat").transform;
        if (heartbeatSource == null) { Debug.LogError("Heartbeat sound is not found. Did you change the name?"); }
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
        float distRatio = Mathf.Clamp01(1f - (dist / maxRadioDistance)) * 0.6f;
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

    void OnCollisionEnter(Collision collision)
    {
        AudioClip clipToPlay = null;

        Debug.Log("hit rigid object.");

        // if (collision.gameObject.CompareTag("WaterSurface"))
        // {
        // }
        if (clipToPlay != null)
        {
            audioSource.PlayOneShot (clipToPlay);
        }
    }

    void OnTriggerEnter(Collider collision)
    {
        AudioClip clipToPlay = null;

        Debug.Log("hit invisible object.");
        float currentTime = Time.time;
        bool collisionTooSoon =
            (currentTime - lastCollisionTime) < waterSurfaceSplashCooldown;

        if (collisionTooSoon)
        {
            Debug.Log("Collision flagged: happened too soon after the last one.");
            clipToPlay = waterFlow;
        }
        else
        {
            if (collision.gameObject.CompareTag("WaterSurface"))
            {
                // Debug.Log("hit water with vel: " + velocity.y);
                // 如果物體垂直速度為負，表示是從上往下進水
                if (velocity.y < 0)
                {
                    if (Mathf.Abs(velocity.y) < 1.5)
                    {
                        clipToPlay = splashInSlow;
                    }
                    else
                    {
                        clipToPlay = splashIn;
                    }
                    Debug.Log("Entering water: playing splashIn");
                } // 如果物體垂直速度為正，表示是從下往上出水
                else if (velocity.y > 0)
                {
                    if (Mathf.Abs(velocity.y) < 1.5)
                    {
                        clipToPlay = splashOutSlow;
                    }
                    else
                    {
                        clipToPlay = splashOut;
                    }
                    Debug.Log("Exiting water: playing splashOut");
                }
            }
        }
        if (clipToPlay != null)
        {
            Debug.Log("play clip: " + clipToPlay);
            audioSource.PlayOneShot (clipToPlay);
        }

        lastCollisionTime = currentTime;
    }

    void OnTriggerExit(Collider collision)
    {
        AudioClip clipToPlay = null;

        Debug.Log("exit invisible object.");

        if (collision.gameObject.CompareTag("WaterSurface"))
        {
            // Debug.Log("hit water with vel: " + velocity.y);
            // 如果物體垂直速度為負，表示是從上往下
            if (velocity.y < 0)
            {
                clipToPlay = baitSinking;
                Debug.Log("Sinking in water: playing sinking sound");
            } // 如果物體垂直速度為正，表示是從下往上出水
            else if (velocity.y > 0)
            {
            }
        }

        if (clipToPlay != null)
        {
            Debug.Log("play clip: " + clipToPlay);
            audioSource.PlayOneShot (clipToPlay);
        }
    }

    // todo: ontriggerstay, if move, play splash soft
}
