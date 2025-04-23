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
    public AudioClip morseCodeSound;
    public AudioClip longBeepSound;
    public AudioClip nosieSound;
    public AudioClip aboveWaterSound;

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
    private bool isAttached = false;
    private bool isReleased = false;

    private Vector3 velocity = Vector3.zero;
    private Vector3 camPos;
    private Vector3 waterSurfacePos;
    private SoundPlay tracker;


    public void Awake()
    {
        SetUpAudioMixer();

        camPos = Camera.main.transform.position;
        waterSurfacePos = GameObject.FindWithTag("WaterSurface").transform.position;
        tracker = GameObject.FindWithTag("Player").GetComponent<SoundPlay>();

        if (tracker == null) { Debug.LogError("Tracker is not tagged as player. Fix it in editor please."); }
    }

    void SetUpAudioMixer()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        audioSource.clip = morseCodeSound;

        AudioMixerGroup[] groups = underwaterEQ.FindMatchingGroups("EQ-Underwater");

        if (groups.Length > 0)
        {
            audioSource.outputAudioMixerGroup = groups[0];
            return;
        }
        Debug.LogError("Audio mixer underwater is not found. Did you change the name?");
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

        // @todo init audio source, lerp volume 0 to normal in one second.
        audioSource = gameObject.GetComponent<AudioSource>();
        audioSource.clip = morseCodeSound;

        audioSource.Stop();

        // reset all internal status
        isReleased = false;
        isAttached = false;
        nearbySince = float.PositiveInfinity;
        attachedSince = float.PositiveInfinity;
    }

    public void UpdatePosition()
    {
        if (isReleased) return;
        if (isAttached)
        {
            transform.position = tracker.transform.position;
            TryRelease();
            return;
        }

        // when not attracted by tracker, boid move around main cam
        // when tracker is close to the boid, move around tracker
        // when tracker is close to the boid for 2 secodns, attach to tracker

        if (tracker.isUnderwater)
        {
            if (!audioSource.isPlaying) audioSource.Play();
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
        transform.position += velocity * Time.deltaTime;

        if (velocity.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(velocity);
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
        audioSource.clip = longBeepSound;
        isAttached = true;

        // Debug.Log("Boid is attached: " + isAttached + ", is released: " + isReleased);
        // Debug.Break();

        if (!audioSource.isPlaying) audioSource.Play();
    }

    void ReleaseFromTracker()
    {
        isAttached = false;

        if (isReleased) return;

        isReleased = true;

        Debug.Log("Boid release from tracker");

        attachedSince = float.PositiveInfinity;
        audioSource.Stop();

        if (tracker.isUnderwater)
        {
            audioSource.PlayOneShot(nosieSound);
        }
        else 
        {
            audioSource.PlayOneShot(aboveWaterSound);
        }
        gameObject.SetActive(false);
    }

    float GetTrackerDistance()
    {
        return Vector3.Distance(transform.position, tracker.transform.position);
    }
}
