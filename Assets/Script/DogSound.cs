using UnityEngine;
using UnityEngine.Audio;

// Controls Dog Sound & Movement
public class DogSound : MonoBehaviour
{
	[Header("Movement Settings")]
	public Vector3 center = Vector3.zero; // Center point for circular movement
	public float moveSpeed = 10.0f; // Speed of circular movement
	public float radius = 1.6f; // Radius of circular movement
	
	[Header("Audio Settings")]
	public AudioClip[] dogSounds; // Array of different dog sound clips

	private AudioSource audioSource;
	private float currentAngle;

	public bool debug = false;

	void Start()
	{
		audioSource = GetComponent<AudioSource>();
		if (audioSource == null)
		{
			audioSource = gameObject.AddComponent<AudioSource>();
		}
		// Set initial values
		currentAngle = Random.Range(0f, 360f);

		if (debug) { Bark(); }
	}

	void Update()
	{
		if (debug) 
		{
			MoveAroundCenter();
			Bark();
			return;
		}

		if (audioSource.isPlaying)
		{
			MoveAroundCenter();
		}
	}

	public void Bark()
	{
		// Play a random dog sound if available
		if (dogSounds != null && dogSounds.Length > 0 && !audioSource.isPlaying)
		{
			int randomIndex = Random.Range(0, dogSounds.Length);
			audioSource.PlayOneShot(dogSounds[randomIndex]);
		}
	}

	void MoveAroundCenter()
	{
		// Calculate new position around center
		currentAngle += moveSpeed * Time.deltaTime;
		if (currentAngle >= 360f)
		{
			currentAngle -= 360f;
		}

		// Convert angle to radians and calculate position
		float radians = currentAngle * Mathf.Deg2Rad;
		float x = center.x + radius * Mathf.Cos(radians);
		float z = center.z + radius * Mathf.Sin(radians);

		// Update position
		Vector3 newPosition = new Vector3(x, center.y, z);
		transform.position = newPosition;
	}
}
