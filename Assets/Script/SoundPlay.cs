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
    public float maxRadioDistance = 1.5f;

    [Header("Other Settings")]
    private float lastCollisionTime = -999f;
    public float waterSurfaceSplashCooldown = 1f;
    private bool isUnderwater = false;
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
        if (radioSource == null) 
        {
            Debug.LogError("Radio sound is not attached to main camera!");
        }
    }

    void Update()
    {
        // 計算物體的速度
        velocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;

        // 计算是否在水下
        isUnderwater = transform.position.y < waterSurfaceTransform.position.y;

        UpdateRadioSource();
    }

    void UpdateRadioSource()
    {
        // 如果在水上，则不播放 radio
        if (!isUnderwater) 
        {
            radioSource.volume = 0f;
            return;
        }

        // 根据距离调整 radio 的音量。
        float dist = Vector3.Distance(transform.position, Camera.main.transform.position);
        radioSource.volume = Mathf.Clamp01(1f - (dist / maxRadioDistance));
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
            Debug
                .Log("Collision flagged: happened too soon after the last one.");
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
