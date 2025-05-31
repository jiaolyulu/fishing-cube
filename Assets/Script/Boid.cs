using UnityEngine;
using UnityEngine.Audio;

// Controls morse code object Spawn & Movement
//
public class Boid : MonoBehaviour
{
    [Header("Spawn & Movement Settings")]
    public Vector2 spawnHorizontalRange = new Vector2(1f, 2f); // vertical range relative to main camera pos
    public float spawnVerticalMargin = 0.3f; // above main camera & below water surface
    public float attachToTrackerAfter = 2f; // if tracker is close, after 2 seconds, attach to tracker

    [Header("Sound Settings")]
    public AudioClip[] radioSound; // morse code / radio
    public AudioClip bellSound; // all env sound silents when tracker is caught
    public AudioClip noiseSound; // radio noise between channels
    public AudioClip releaseUnderwaterSound;

    public DogSound dog; // play when tracker is released above water

    private int radioIndex = 0;

    [Header("EQ Settings")]
    public AudioMixer underwaterEQ;
    private AudioSource audioSource;

    [Header("Boid Settings")]
    public float maxSpeed = 0.3f; // move slowly please!
    public float maxForce = 1f;
    // attraction / repulsion
    public float attractionStrength = 4f;
    public float repulsionDistance = 0.2f;
    public float repulsionStrength = 1f;
    public float tangentStrength = 1f; // move counter-clockwise

    [Header("Attraction Settings")]
    public float attractionDistance = 2f; // attract to tracker in the distance
    public float attachDistance = 0.3f; // attach to tracker in the distance
    public float attachDelay = 2f; // attach to tracker after the delay
    public float releaseDelay = 3f; // release after 3s if attached longer than it underwater
    public float respawnDelay = 3f; // respawn after 3s after release

    // 内部计时器：累计与 tracker 保持靠近的时间
    private float nearbySince = float.PositiveInfinity;
    private float attachedSince = float.PositiveInfinity;
    public bool isAttached { get; private set; }
    private bool isReleased = false;

    private Vector3 velocity = Vector3.zero;
    private Vector3 camPos;
    private Vector3 waterSurfacePos;
    private SoundPlay tracker;
    private AudioMixerGroup mixerGroup;


    public void Awake()
    {
        SetUpAudioMixer();

        camPos = Camera.main.transform.position;
        waterSurfacePos = GameObject.FindWithTag("WaterSurface").transform.position;
        tracker = GameObject.FindWithTag("Player").GetComponent<SoundPlay>();
        isAttached = false;

        if (tracker == null) { Debug.LogError("Tracker is not tagged as player. Fix it in editor please."); }
    }

    void SetUpAudioMixer()
    {
        PickRadioClip();

        AudioMixerGroup[] groups = underwaterEQ.FindMatchingGroups("EQ-Underwater");

        if (groups.Length > 0)
        {
            mixerGroup = groups[0];
            audioSource.outputAudioMixerGroup = mixerGroup;
            return;
        }
        Debug.LogError("Audio mixer underwater is not found. Did you change the name?");
    }

    void PickRadioClip()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        audioSource.clip = radioSound[radioIndex % radioSound.Length];
        radioIndex++;
    }

    float GetDepthUnderwater()
    {
        float depth = waterSurfacePos.y-transform.position.y;
        return depth < 0f ? 0f : depth;
    }

    public void InitializePosition()
    {
        // random spawn out of box, under water depth -0.2f, above bottom 0.2f;
        // 0.2f = spawnVerticalMargin;
        Vector3 pos = new Vector3(
            Random.Range(-spawnHorizontalRange.x, spawnHorizontalRange.x), 
            Random.Range(camPos.y+spawnVerticalMargin, waterSurfacePos.y-spawnVerticalMargin),
            Random.Range(-spawnHorizontalRange.y, spawnHorizontalRange.y));
        transform.position = pos;

        Debug.Log("Boid init position: " + pos);

        // random initialize velocity
        velocity = new Vector3(Random.value * 2f, Random.value * 2f, Random.value * 0.5f);

        PickRadioClip();

        audioSource.Stop();

        // reset all internal status
        isReleased = false;
        isAttached = false;
        nearbySince = float.PositiveInfinity;
        attachedSince = float.PositiveInfinity;
    }

    public void UpdatePosition()
    {
        // 1. release mode
        // 2. attach mode
        // 3. tracker mode (boid)
        // 4. main camera mode (boid)
        // 
        if (isReleased) return;
        if (isAttached)
        {
            // Debug.Break();

            transform.position = tracker.transform.position;
            TryRelease();
            return;
        }

        // when not attracted by tracker, boid move around main cam
        // when tracker is close to the boid, move around tracker
        // when tracker is close to the boid for 2 secodns, attach to tracker

        if (tracker.isUnderwater)
        {
            if (!audioSource.isPlaying) 
            {
                PickRadioClip();
                audioSource.Play();
            }
        } else {
            if (!isAttached) audioSource.Stop();
        }

        float distTracker = GetTrackerDistance();        
        if (distTracker <= attractionDistance && tracker.isUnderwater)
        {
            // 当 tracker 足够近时，围绕 tracker 运动, tracker 需要在水下
            Vector3 trackerPos = tracker.transform.position;
            UpdateBoidPosition(trackerPos);

            // attach to tracker when close enough for long
            if (nearbySince == float.PositiveInfinity)
            {
                Debug.Log("Boid detects player nearby.");
                nearbySince = Time.time;
            }

            if (ShouldAttach())
            {
                Debug.Log("Boid attach to tracker..now: " + Time.time);
                AttachToTracker();
            }
        }
        else
        {
            // Debug.Log("Boid attract by main camera.");

            // if far from tracker, move around main camera, reset timer
            UpdateBoidPosition(camPos);

            nearbySince = float.PositiveInfinity;
            attachedSince = float.PositiveInfinity;
        }
    }

    void UpdateBoidPosition(Vector3 center)
    {
        // attracted to center by force, when close enough, add repulsive force
        // steer the direction, highlight attraction center by a debug gameobject / point

        // draw attraction center!
        Debug.DrawLine(transform.position, center, Color.yellow);

        Vector3 toCenter = center - transform.position;
        Vector3 desired = toCenter.normalized * maxSpeed * attractionStrength;

        // when too close, repulse
        float dist = toCenter.magnitude;
        if (dist < repulsionDistance)
        {
            desired = (-toCenter).normalized * maxSpeed * repulsionStrength;
        }

        // update velocity, steer, position
        Vector3 steer = desired - velocity;
        Vector3 tangent = Vector3.Cross(Vector3.up, toCenter).normalized * tangentStrength; // 逆时针围绕 center 旋转

        steer = Vector3.ClampMagnitude(steer, maxForce) + tangent;
        velocity = Vector3.ClampMagnitude(velocity + steer * Time.deltaTime, maxSpeed);

        // add velocity to current pos, but
        // don't go too low / above water surface in boid mode
        Vector3 nextPos = transform.position + velocity * Time.deltaTime;
        nextPos.y = Mathf.Clamp(nextPos.y, camPos.y+spawnVerticalMargin, waterSurfacePos.y-spawnVerticalMargin);
        transform.position = nextPos;

        UpdateMixerPitch();
    }

    void UpdateMixerPitch()
    {
        audioSource.outputAudioMixerGroup = mixerGroup;

        if (tracker.isUnderwater)
        {
            // between 0.8~0.9,
            float ratio = Mathf.Clamp01(GetDepthUnderwater() / (waterSurfacePos.y - camPos.y)); // [0,1]
            mixerGroup.audioMixer.SetFloat("Pitch", Mathf.Lerp(0.8f, 0.9f, ratio));
        } else {
            audioSource.outputAudioMixerGroup = null; // disable underwater mixer
        }
    }

    void TryRelease()
    {
        // if not in water, release
        if (!tracker.isUnderwater)
        {
            ReleaseFromTracker();
            return;
        }
        // if stay underwater > delay, release
        if (tracker.isUnderwater && Time.time - attachedSince >= releaseDelay)
        {
            ReleaseFromTracker();
            return;
        }
    }

    bool ShouldAttach()
    {
        if (isReleased || isAttached)
        {
            return false;
        }
        return Time.time - nearbySince >= attachDelay || GetTrackerDistance() < attachDistance;
    }

    void AttachToTracker()
    {
        Debug.Log("Boid is attached: " + isAttached + ", is released: " + isReleased);

        if (isReleased || isAttached) return;

        if (attachedSince == float.PositiveInfinity)
        {
            attachedSince = Time.time;
        }

        // attach to tracker
        transform.position = tracker.transform.position;
        audioSource.clip = bellSound;        
        isAttached = true;

        // Debug.Log("Boid is attached: " + isAttached + ", is released: " + isReleased);
        // Debug.Break();

        if (!audioSource.isPlaying) audioSource.Play();
    }

    void ReleaseFromTracker()
    {
        if (isReleased) return;

        Debug.Log("Boid release from tracker");

        if (tracker.isUnderwater)
        {
            tracker.PlayOneShot(releaseUnderwaterSound);
        }
        else 
        {
            dog.Bark();
        }

        attachedSince = float.PositiveInfinity;
        isAttached = false;
        isReleased = true;

        audioSource.Stop();
        gameObject.SetActive(false);
    }

    float GetTrackerDistance()
    {
        return Vector3.Distance(transform.position, tracker.transform.position);
    }
}
