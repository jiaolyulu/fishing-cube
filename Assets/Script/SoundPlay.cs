using UnityEngine;

public class SoundPlay : MonoBehaviour
{
    public AudioClip splashIn;

    public AudioClip splashInSlow; // todo: dynamic modify when underwater - low pass filter / EQ

    public AudioClip splashOut;

    public AudioClip splashOutSlow;

    public AudioClip waterFlow;

    public AudioClip baitSinking;

    public AudioClip poleClicking; // todo: fade out when sinking

    private AudioSource audioSource;

    private Vector3 lastPosition;

    private Vector3 velocity;

    private float lastCollisionTime = -999f;

    public float collisionCooldown = 1f;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        lastPosition = transform.position;
    }

    void Update()
    {
        // 計算物體的速度
        velocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
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
            (currentTime - lastCollisionTime) < collisionCooldown;

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
