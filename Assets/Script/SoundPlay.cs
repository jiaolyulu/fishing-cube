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
    private AudioSource radioSource; // on main camera
    public float maxRadioDistance = 1.8f;
    private AudioSource outwaterSource; // on pond object

    [Header("Other Settings")]
    private float lastCollisionTime = -999f;
    public float waterSurfaceSplashCooldown = 1f;
    private bool isUnderwater = false;
    private float underwaterDepth = 0f;
    private Transform waterSurfaceTransform;

    void Start()
    {
        SetUpAudioMixer();
        SetUpRadioSound();

        lastPosition = transform.position;

        waterSurfaceTransform = GameObject.FindWithTag("WaterSurface").transform;
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
        }
        else
        {
            Debug.LogError("Audio mixer underwater is not found. Did you change the name?");
        }
    }

    void SetUpRadioSound()
    {
        radioSource = Camera.main.GetComponent<AudioSource>();
        if (radioSource == null) { Debug.LogError("Radio sound is not attached to main camera!"); }

        outwaterSource = GameObject.Find("OutWaterSound").GetComponent<AudioSource>();
        if (outwaterSource == null)  { Debug.LogError("Out water sound is not attached. Did you change the name?"); }
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
        UpdateOutwaterSource();
    }

    void UpdateRadioSource()
    {
        // 如果在水上，则不播放 radio
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

    void UpdateOutwaterSource()
    {
        if (!outwaterSource.isPlaying) outwaterSource.Play();

        // 根据水面的距离调整音量
        if (isUnderwater) 
        {
            float dist = Vector3.Distance(transform.position, waterSurfaceTransform.position);
            float distRatio = Mathf.Clamp01(1f - (dist / 1.2f));
            outwaterSource.volume = 0.3f * distRatio;
            outwaterSource.pitch = 0.3f * distRatio;
        } else {
            outwaterSource.volume = 0.3f;
            outwaterSource.pitch = 1f;
        }
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
