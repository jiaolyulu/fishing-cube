using System.Collections.Generic;
using UnityEngine;

public class ColorAreaTracker : MonoBehaviour
{
    [Header("Camera Settings")]
    public WebCamTexture webCamTexture;

    public string preferredDevice = "";

    public int requestedWidth = 640;

    public int requestedHeight = 480;

    public int requestedFPS = 30;

    public bool hasTarget = true;

    [Header("Detection Settings")]
    public int minAreaSize = 9; // Minimum number of colored pixels in area

    public int clusterSize = 3; // Size of cluster (3x3)

    [Range(0, 255)]
    public int colorThreshold = 100; // How strong a color needs to be

    [Range(0, 255)]
    public int colorDominanceThreshold = 50; // How much stronger than other channels

    [Range(0, 255)]
    public int blackThreshold = 30; // How dark pixels need to be to be considered black

    public enum TrackingColor
    {
        Red,
        Green,
        Blue,
        Auto
    }

    public TrackingColor trackingColor = TrackingColor.Auto;

    [Header("Object To Move")]
    public Transform targetObject;

    [Header("Movement Settings")]
    public float movementSpeed = 5.0f;

    public Vector2 screenBounds = new Vector2(5, 5);

    public float minY = 0f; // Minimum Y position

    public float surfaceY = 1.5f;

    public float maxY = 5f; // Maximum Y position

    public int minAreaPixels = 9; // Area size that maps to minY

    public int surfaceAreaPixels = 40; // Area size that maps to water surface

    public int maxAreaPixels = 100; // Area size that maps to maxY

    public int areaUnit = 1000;

    [Header("Debounce Settings")]
    public float positionUpdateInterval = 0.05f; // Time between position updates in seconds

    public float minMovementThreshold = 0.01f; // Minimum position change to trigger an update

    public float positionSmoothing = 0.5f; // 0 = no smoothing, 1 = max smoothing

    public float areaSizeSmoothing = 0.5f; // Smoothing for area size changes

    public float yOffsetCompensation = 0.1f;

    public float xzOffsetCompensation = 0.4f;

    [Header("Debug")]
    public bool showDebugInfo = true;

    // The detected position of the most significant color area
    private Vector2 colorAreaPosition;

    private Vector2 smoothedPosition;

    private Vector2 lastUpdatedPosition;

    private float distanceToCenter;

    private TrackingColor detectedColor = TrackingColor.Red;

    // Store the size of the detected area
    private int detectedAreaSize = 0;

    private int smoothedAreaSize = 0;

    // Store analyzed pixel data
    private Color32[] pixelData;

    private bool isProcessing = false;

    // Debounce timer
    private float lastUpdateTime = 0f;

    void Start()
    {
        // List available webcams
        Debug.Log("Available webcams:");
        foreach (WebCamDevice device in WebCamTexture.devices)
        {
            Debug.Log("- " + device.name);
        }

        // If no specific device requested, use the first available
        if (string.IsNullOrEmpty(preferredDevice))
        {
            if (WebCamTexture.devices.Length > 0)
            {
                preferredDevice = WebCamTexture.devices[0].name;
                Debug.Log("Using webcam: " + preferredDevice);
            }
            else
            {
                Debug.LogError("No webcam found!");
                return;
            }
        }

        // Initialize webcam texture
        webCamTexture = new WebCamTexture(preferredDevice, requestedWidth, requestedHeight, requestedFPS);
        webCamTexture.Play();

        // Wait for webcam to start
        if (!webCamTexture.isPlaying)
        {
            Debug.LogError("Failed to start webcam!");
            return;
        }

        // Initialize pixel data array
        pixelData = new Color32[webCamTexture.width * webCamTexture.height];

        // Initialize positions
        colorAreaPosition = new Vector2(0.5f, 0.5f);
        smoothedPosition = colorAreaPosition;

        lastUpdatedPosition = colorAreaPosition;
        detectedAreaSize = minAreaSize;
        smoothedAreaSize = minAreaSize;

        // Log webcam details
        Debug
            .Log("Webcam initialized: " +
            $"{webCamTexture.width}x{webCamTexture.height}" +
            $" at {webCamTexture.requestedFPS}fps");
    }

    void Update()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            // Process image data if not already processing
            if (!isProcessing)
            {
                StartCoroutine(ProcessImageData());
            }

            // Apply smoothing to the color area position and size
            ApplySmoothing();

            // Move the target object based on the smoothed position and area size
            MoveTargetObject();
        }
    }

    System.Collections.IEnumerator ProcessImageData()
    {
        isProcessing = true;

        // Get the current frame's pixel data
        webCamTexture.GetPixels32 (pixelData);

        // Wait for the end of frame to free up main thread
        yield return new WaitForEndOfFrame();

        // Find the most significant color area
        FindColorArea();

        // Mark processing as done
        isProcessing = false;
    }

    bool IsColoredPixel(Color32 pixel, TrackingColor color)
    {
        bool isDarkRed = pixel.g < blackThreshold && pixel.b < blackThreshold && pixel.r > pixel.g && pixel.r > pixel.b;
        bool isDarkGreen =
            pixel.r < blackThreshold && pixel.b < blackThreshold && pixel.g > pixel.r && pixel.g > pixel.b;
        bool isDarkBlue =
            pixel.r < blackThreshold && pixel.g < blackThreshold && pixel.b > pixel.r && pixel.b > pixel.g;
        switch (color)
        {
            case TrackingColor.Red:
                return isDarkRed ||
                (
                pixel.r > colorThreshold &&
                pixel.r > pixel.g + colorDominanceThreshold &&
                pixel.r > pixel.b + colorDominanceThreshold
                );
            case TrackingColor.Green:
                return isDarkGreen ||
                (
                pixel.g > colorThreshold &&
                pixel.g > pixel.r + colorDominanceThreshold &&
                pixel.g > pixel.b + colorDominanceThreshold
                );
            case TrackingColor.Blue:
                return isDarkBlue ||
                (
                pixel.b > colorThreshold &&
                pixel.b > pixel.r + colorDominanceThreshold &&
                pixel.b > pixel.g + colorDominanceThreshold
                );
            default:
                return false;
        }
    }

    Vector2
    FloodFill(int startX, int startY, int width, int height, TrackingColor color, bool[] visited, ref int areaSize)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        List<Vector2Int> pixels = new List<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startY * width + startX] = true;

        int sumX = 0;
        int sumY = 0;
        areaSize = 0;

        while (queue.Count > 0)
        {
            Vector2Int pixel = queue.Dequeue();
            int x = pixel.x;
            int y = pixel.y;

            pixels.Add (pixel);
            sumX += x;
            sumY += y;
            areaSize++;

            // Check 4 neighboring pixels (up, down, left, right)
            Vector2Int[] neighbors =
                new Vector2Int[] {
                    new Vector2Int(x, y - 1),
                    new Vector2Int(x, y + 1),
                    new Vector2Int(x - 1, y),
                    new Vector2Int(x + 1, y)
                };

            foreach (var neighbor in neighbors)
            {
                int nx = neighbor.x;
                int ny = neighbor.y;
                int nIndex = ny * width + nx;

                if (
                    nx >= 0 &&
                    ny >= 0 &&
                    nx < width &&
                    ny < height &&
                    !visited[nIndex] &&
                    IsColoredPixel(pixelData[nIndex], color)
                )
                {
                    queue.Enqueue (neighbor);
                    visited[nIndex] = true;
                }
            }
        }

        if (areaSize > 0)
        {
            return new Vector2(sumX / (float) areaSize, sumY / (float) areaSize);
        }
        return Vector2.zero;
    }

    void FindColorArea()
    {
        int width = webCamTexture.width;
        int height = webCamTexture.height;

        TrackingColor bestColor = detectedColor;
        TrackingColor[] colorsToCheck =
            (trackingColor == TrackingColor.Auto)
                ? new TrackingColor[] { TrackingColor.Red, TrackingColor.Green, TrackingColor.Blue }
                : new TrackingColor[] { trackingColor };

        bool[] visited = new bool[pixelData.Length]; // Track visited pixels
        int maxSize = 0;
        Vector2 maxAreaPosition = Vector2.zero;
        TrackingColor maxAreaColor = detectedColor;

        foreach (TrackingColor currentColor in colorsToCheck)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;

                    if (!visited[index] && IsColoredPixel(pixelData[index], currentColor))
                    {
                        // Perform flood-fill to find the full area size
                        int areaSize = 0;
                        Vector2 centroid = FloodFill(x, y, width, height, currentColor, visited, ref areaSize);

                        // Update the largest area found
                        if (areaSize > maxSize)
                        {
                            maxSize = areaSize;
                            maxAreaPosition = centroid;
                            maxAreaColor = currentColor;
                        }
                    }
                }
            }
        }

        // If we found a valid colored area
        if (maxSize >= minAreaSize)
        {
            Vector2 newPosition = new Vector2(maxAreaPosition.x / width, maxAreaPosition.y / height);

            if (ShouldUpdatePosition(newPosition))
            {
                colorAreaPosition = newPosition;

                lastUpdatedPosition = colorAreaPosition;
                lastUpdateTime = Time.time;
                detectedAreaSize = maxSize;
            }

            detectedColor = maxAreaColor;
            hasTarget = true;
            if (showDebugInfo)
            {
                Debug
                    .Log($"{maxAreaColor} area found at: " +
                    $"({maxAreaPosition.x}, {maxAreaPosition.y}) " +
                    $"with {maxSize} pixels");
            }
        }
        else if (showDebugInfo)
        {
            Debug.Log("No significant color area found");
            hasTarget = false;
        }
    }

    bool ShouldUpdatePosition(Vector2 newPosition)
    {
        // Check if enough time has passed since the last update
        bool timeElapsed = (Time.time - lastUpdateTime) >= positionUpdateInterval;

        // Check if the position has changed significantly
        bool significantChange = Vector2.Distance(newPosition, lastUpdatedPosition) >= minMovementThreshold;

        return timeElapsed && significantChange;
    }

    void ApplySmoothing()
    {
        // Apply exponential smoothing to the position
        smoothedPosition = Vector2.Lerp(smoothedPosition, colorAreaPosition, 1.0f - positionSmoothing);
        distanceToCenter = (smoothedPosition - new Vector2(0.5f, 0.5f)).sqrMagnitude;

        // Apply exponential smoothing to the area size
        smoothedAreaSize = Mathf.RoundToInt(Mathf.Lerp(smoothedAreaSize, detectedAreaSize, 1.0f - areaSizeSmoothing));
    }

    void MoveTargetObject()
    {
        if (targetObject != null)
        {
            float surfaceYFraction = (surfaceY - minY) / (maxY - minY);
            float
                yPosFactor,
                yPosition;

            if (smoothedAreaSize <= areaUnit * surfaceAreaPixels)
            {
                // Calculate the Y position based on the area size
                float normalizedSize =
                    Mathf
                        .InverseLerp(Mathf.Sqrt(minAreaPixels * areaUnit),
                        Mathf.Sqrt(surfaceAreaPixels * areaUnit),
                        Mathf.Sqrt(smoothedAreaSize));
                yPosFactor = Mathf.Lerp(surfaceYFraction, 0.0f, normalizedSize);
                yPosition = Mathf.Lerp(maxY, surfaceY, normalizedSize) + distanceToCenter * yOffsetCompensation;
            }
            else
            {
                float normalizedSize =
                    Mathf
                        .InverseLerp(Mathf.Sqrt(surfaceAreaPixels * areaUnit),
                        Mathf.Sqrt(maxAreaPixels * areaUnit),
                        Mathf.Sqrt(smoothedAreaSize));
                yPosFactor = Mathf.Lerp(1.0f, surfaceYFraction, normalizedSize);
                yPosition = Mathf.Lerp(surfaceY, minY, normalizedSize) - distanceToCenter * yOffsetCompensation;
            }

            // Map the smoothed position (0-1) to our world space bounds
            Vector3 targetPosition =
                new Vector3(Mathf
                        .Lerp(screenBounds.x,
                        -screenBounds.x,
                        0.5f + (smoothedPosition.x - 0.5f) * (1.0f + xzOffsetCompensation * yPosFactor)),
                    yPosition,
                    Mathf
                        .Lerp(-screenBounds.y,
                        screenBounds.y,
                        0.5f + (smoothedPosition.y - 0.5f) * (1.0f + xzOffsetCompensation * yPosFactor)));

            // Smoothly move the object towards the target position
            targetObject.position = Vector3.Lerp(targetObject.position, targetPosition, movementSpeed * Time.deltaTime);
        }
    }

    void OnGUI()
    {
        if (showDebugInfo)
        {
            // Show the webcam texture in the corner for debugging
            int displayWidth = 160;
            int displayHeight = 120;
            GUI.DrawTexture(new Rect(10, 10, displayWidth, displayHeight), webCamTexture);

            // Draw a marker for the detected position
            int markerX = Mathf.RoundToInt(10 + smoothedPosition.x * displayWidth);
            int markerY = Mathf.RoundToInt(10 + (1 - smoothedPosition.y) * displayHeight);

            // Set marker color based on detected color
            switch (detectedColor)
            {
                case TrackingColor.Red:
                    GUI.color = Color.red;
                    break;
                case TrackingColor.Green:
                    GUI.color = Color.green;
                    break;
                case TrackingColor.Blue:
                    GUI.color = Color.blue;
                    break;
                default:
                    GUI.color = Color.yellow;
                    break;
            }

            // Draw marker with size proportional to detected area
            float markerSize =
                Mathf
                    .Lerp(5,
                    20,
                    Mathf.InverseLerp(minAreaPixels * areaUnit, maxAreaPixels * areaUnit, smoothedAreaSize));
            GUI
                .DrawTexture(new Rect(markerX - markerSize / 2, markerY - markerSize / 2, markerSize, markerSize),
                Texture2D.whiteTexture);

            // Display coordinates, detected color, and area size
            GUI.color = Color.white;
            GUI
                .Label(new Rect(10, displayHeight + 20, 300, 20),
                $"{detectedColor} area position: ({smoothedPosition.x:F2}, {smoothedPosition.y:F2})");
            GUI
                .Label(new Rect(10, displayHeight + 40, 300, 20),
                $"Area size: {smoothedAreaSize} pixels (Y position: {targetObject?.position.y:F2})");

            // Display debounce status
            string debounceStatus =
                (Time.time - lastUpdateTime < positionUpdateInterval) ? "Debouncing" : "Ready for update";
            GUI.Label(new Rect(10, displayHeight + 60, 300, 20), debounceStatus);
        }
    }

    void OnDestroy()
    {
        // Clean up resources
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Stop();
        }
    }
}
