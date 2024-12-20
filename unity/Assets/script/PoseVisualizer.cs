using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;

public class PoseVisualizer : MonoBehaviour
{
    public string[] data;
    public GameObject pointPrefab; // Prefab for visualizing landmarks
    public int udpPort = 12345;    // UDP port to receive data
    public float scaleFactor = 1.0f; // Scaling factor for received data
    public Material lineMaterial;  // Material for the lines
    public Transform leftToesTransform;  // Transform for the Left Toes bone
    public Transform rightToesTransform; // Transform for the Right Toes bone
    private UdpClient udpClient;
    private Queue<Action> actionsQueue = new Queue<Action>();
    private List<GameObject> poseLandmarkPoints = new List<GameObject>(); // Points for pose landmarks
    private List<LineRenderer> poseConnections = new List<LineRenderer>(); // Pose connections (lines)
    public Animator characterAnimator;  // Reference to the Animator component of your character
    
    private Dictionary<int, HumanBodyBones> landmarkToBoneMap = new Dictionary<int, HumanBodyBones>
    {
        // Map of landmark indices to the corresponding bones in the humanoid character
        { 12, HumanBodyBones.LeftUpperArm }, 
        { 11, HumanBodyBones.RightUpperArm },

        { 14, HumanBodyBones.LeftLowerArm }, 
        { 13, HumanBodyBones.RightLowerArm },

        { 24, HumanBodyBones.LeftUpperLeg }, 
        { 23, HumanBodyBones.RightUpperLeg },
        { 26, HumanBodyBones.LeftLowerLeg }, 
        { 25, HumanBodyBones.RightLowerLeg },
        { 28, HumanBodyBones.LeftFoot }, 
        { 27, HumanBodyBones.RightFoot },

        { 30, HumanBodyBones.LeftToes }, 
        { 29, HumanBodyBones.RightToes },

        { 33, HumanBodyBones.Hips}, 
        { 34, HumanBodyBones.Spine},
        { 35, HumanBodyBones.Neck},
        { 36, HumanBodyBones.Head}

    };
    Dictionary<HumanBodyBones, (int startLandmark, int endLandmark)> boneToLandmarkPairs = new Dictionary<HumanBodyBones, (int, int)>
    {
        { HumanBodyBones.LeftUpperArm, (12, 14) }, // Left Shoulder (12) to Left Elbow (14)
        { HumanBodyBones.RightUpperArm, (11, 13) }, // Right Shoulder (11) to Right Elbow (13)

        { HumanBodyBones.LeftLowerArm, (14, 16) }, // Left Elbow (14) to Left Wrist (16)
        { HumanBodyBones.RightLowerArm, (13, 15) }, // Right Elbow (13) to Right Wrist (15)

        { HumanBodyBones.LeftUpperLeg, (24, 26) }, // Left Hip (24) to Left Knee (26)
        { HumanBodyBones.RightUpperLeg, (23, 25) }, // Right Hip (23) to Right Knee (25)

        { HumanBodyBones.LeftLowerLeg, (26, 28) }, // Left Knee (26) to Left Ankle (28)
        { HumanBodyBones.RightLowerLeg, (25, 27) }, // Right Knee (25) to Right Ankle (27)

        { HumanBodyBones.LeftFoot, (28, 30) }, // Left Ankle (28) to Left Foot (30)
        { HumanBodyBones.RightFoot, (27, 29) }, // Right Ankle (27) to Right Foot (29)

        { HumanBodyBones.LeftToes, (30, 32) }, // Right Ankle (27) to Right Foot (29)
        { HumanBodyBones.RightToes, (29, 31) }, // Right Ankle (27) to Right Foot (29)
        
        { HumanBodyBones.Hips, (33, 34) }, // Hips (33) to Spine (34)
        { HumanBodyBones.Spine, (34, 35) }, // Spine (34) to Neck (35)
        { HumanBodyBones.Neck, (35, 36)},
        
    };
    // Connections as pairs of indices (based on Mediapipe pose landmarks)
    private readonly int[,] connectionsIndices = new int[,]
    {
        { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 7 }, {12,24}, {11,23}, // Example: Head to shoulder
        { 0, 4 }, { 4, 5 }, { 5, 6 }, { 6, 8 }, // Another side of head
        { 9, 10 }, { 11, 12 }, { 11, 13 }, { 13, 15 }, // Arms and torso
        { 12, 14 }, { 14, 16 }, { 15, 17 }, { 16, 18 }, // More arms
        { 23, 24 }, { 23, 25 }, { 24, 26 }, { 25, 27 }, { 26, 28 }, // Legs
        {28, 32}, {32, 30}, {28,30}, {27,29}, {27,31}, {29,31},
        {33, 34}, {34 ,35}, {35, 36}

    };
    void Start()
    {
        udpClient = new UdpClient(udpPort);
        udpClient.EnableBroadcast = true;

        Debug.Log("Listening for UDP data on port " + udpPort);

        ReceiveData();
    }
    void Update()
    {
        while (actionsQueue.Count > 0)
        {
            actionsQueue.Dequeue().Invoke();
        }
    }
    private void ReceiveData()
    {
        udpClient.BeginReceive(ReceiveCallback, null);
    }
    private void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, udpPort);
            byte[] receivedBytes = udpClient.EndReceive(ar, ref remoteEndPoint);
            string receivedData = Encoding.UTF8.GetString(receivedBytes);

           // Extract left and right hand landmarks from JSON data
            // Debug.Log(receivedData);
            JObject jsonData = JObject.Parse(receivedData);

            
            actionsQueue.Enqueue(() => ParseAndVisualizeData(receivedData));

            udpClient.BeginReceive(ReceiveCallback, null);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error receiving data: " + ex.Message);
        }
    }
    private void ParseAndVisualizeData(string data)
    {
        try
        {
            JObject jsonData = JObject.Parse(data);
            var poseLandmarks = jsonData["pose"] as JArray;

            float lerpFactor = Time.deltaTime * 15f;
            // Update visualizations
            if (poseLandmarks != null)
            {
                UpdateLandmarkPoints(poseLandmarks, poseLandmarkPoints, connectionsIndices, poseConnections);
                ApplyCharacterMovements(poseLandmarks, lerpFactor);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error parsing or visualizing data: " + ex.Message);
        }
    }
    private JArray GenerateOffsetLandmarks(Vector3 wristPosition, Vector3 offset, int landmarkCount)
    {
        JArray landmarks = new JArray();

        for (int i = 0; i < landmarkCount; i++)
        {
            JObject landmark = new JObject
            {
                ["x"] = wristPosition.x + offset.x,
                ["y"] = wristPosition.y + offset.y,
                ["z"] = wristPosition.z + offset.z
            };
            landmarks.Add(landmark);
        }

        return landmarks;
    }
    private void UpdateLandmarkPoints(JArray landmarks, List<GameObject> landmarkPoints, int[,] connectionIndices, List<LineRenderer> connections, Vector3? offset = null)
    {
        HashSet<int> requiredIndices = new HashSet<int>();
        for (int i = 0; i < connectionIndices.GetLength(0); i++)
        {
            requiredIndices.Add(connectionIndices[i, 0]);
            requiredIndices.Add(connectionIndices[i, 1]);
        }

        // Ensure enough points for landmarks
        while (landmarkPoints.Count < requiredIndices.Count)
        {
            GameObject point = Instantiate(pointPrefab, transform);
            landmarkPoints.Add(point);
        }

        List<int> requiredIndicesList = new List<int>(requiredIndices);
        requiredIndicesList.Sort();
        Dictionary<int, GameObject> indexToPoint = new Dictionary<int, GameObject>();
        for (int i = 0; i < requiredIndicesList.Count; i++)
        {
            indexToPoint[requiredIndicesList[i]] = landmarkPoints[i];
            landmarkPoints[i].name = $"Point[{requiredIndicesList[i]}]"; // Set name to include the index
        }

        // Update positions of points
        foreach (int index in requiredIndices)
        {
            if (index < landmarks.Count)
            {
                var landmark = landmarks[index];
                if (landmark != null)
                {
                    Vector3 position = new Vector3(
                        landmark["x"].Value<float>(),
                        -landmark["y"].Value<float>(),
                        -landmark["z"].Value<float>()
                    );

                    // Apply offset if provided
                    if (offset.HasValue)
                    {
                        position += offset.Value;
                    }

                    indexToPoint[index].transform.position = position;
                }
            }
        }

        // Draw connections
        while (connections.Count < connectionIndices.GetLength(0))
        {
            LineRenderer line = new GameObject("Connection").AddComponent<LineRenderer>();
            line.transform.parent = transform;
            line.material = lineMaterial;
            line.startWidth = 0.01f;
            line.endWidth = 0.01f;
            connections.Add(line);
        }

        for (int i = 0; i < connectionIndices.GetLength(0); i++)
        {
            int startIdx = connectionIndices[i, 0];
            int endIdx = connectionIndices[i, 1];

            if (indexToPoint.ContainsKey(startIdx) && indexToPoint.ContainsKey(endIdx))
            {
                connections[i].SetPosition(0, indexToPoint[startIdx].transform.position);
                connections[i].SetPosition(1, indexToPoint[endIdx].transform.position);
            }
        }
    }
    private void ApplyCharacterMovements(JArray poseLandmarks, float lerpFactor)
{
    // Iterate through the mapping and apply the pose data to the bones
    foreach (var entry in landmarkToBoneMap)
    {
        int landmarkIndex = entry.Key;
        HumanBodyBones bone = entry.Value;

        if (landmarkIndex < poseLandmarks.Count && characterAnimator != null)
        {
            Transform boneTransform = characterAnimator.GetBoneTransform(bone);
            if (boneTransform != null)
            {
                // Get the current position of the landmark
                Vector3 currentLandmarkPos = new Vector3(
                    poseLandmarks[landmarkIndex]["x"].Value<float>(),
                    -poseLandmarks[landmarkIndex]["y"].Value<float>(),
                    -poseLandmarks[landmarkIndex]["z"].Value<float>()
                );

                // Smoothly interpolate position (not just rotation)
                // boneTransform.position = Vector3.Lerp(boneTransform.position, currentLandmarkPos, lerpFactor);

                // Now apply rotation based on adjacent landmarks (if possible)
                if (boneToLandmarkPairs.TryGetValue(bone, out var adjacentLandmarks))
                {
                    // Get the adjacent landmarks (start and end of the bone segment)
                    int startLandmarkIndex = adjacentLandmarks.Item1;
                    int endLandmarkIndex = adjacentLandmarks.Item2;

                    if (startLandmarkIndex < poseLandmarks.Count && endLandmarkIndex < poseLandmarks.Count)
                    {
                        // Get the positions of the start and end landmarks
                        Vector3 startLandmarkPos = new Vector3(
                            poseLandmarks[startLandmarkIndex]["x"].Value<float>(),
                            -poseLandmarks[startLandmarkIndex]["y"].Value<float>(),
                            -poseLandmarks[startLandmarkIndex]["z"].Value<float>()
                        );

                        Vector3 endLandmarkPos = new Vector3(
                            poseLandmarks[endLandmarkIndex]["x"].Value<float>(),
                            -poseLandmarks[endLandmarkIndex]["y"].Value<float>(),
                            -poseLandmarks[endLandmarkIndex]["z"].Value<float>()
                        );

                        // Calculate the direction vector from start to end
                        Vector3 direction = (endLandmarkPos - startLandmarkPos).normalized;

                        // Calculate the target rotation
                        Quaternion targetRotation = Quaternion.LookRotation(direction);

                        // Apply an offset rotation depending on whether the bone is left or right
                        Quaternion offsetRotation;
                        if (IsLeftSideBone(bone))
                        {
                            offsetRotation = Quaternion.Euler(90, 0, 0);
                        }
                        else if (IsHeadBone(bone))
                        {
                            offsetRotation = Quaternion.Euler(45, 0, 0);
                        }
                        else if (IsNeckBone(bone))
                        {
                            offsetRotation = Quaternion.Euler(-90, 0, 0);
                        }
                        else
                        {
                            offsetRotation = Quaternion.Euler(-90, 0, 0);
                        }

                        // Apply the rotation with offset first, then smooth interpolation
                        boneTransform.rotation = Quaternion.Lerp(boneTransform.rotation, targetRotation * offsetRotation, lerpFactor);
                    }
                }
            }
        }
    }
}

    // Helper function to determine if the bone is on the left side of the body
    private bool IsLeftSideBone(HumanBodyBones bone)
    {
        switch (bone)
        {
            case HumanBodyBones.LeftUpperArm:
            case HumanBodyBones.LeftLowerArm:
            case HumanBodyBones.LeftHand:
            case HumanBodyBones.LeftUpperLeg:
            case HumanBodyBones.LeftLowerLeg:
            case HumanBodyBones.LeftFoot:
            case HumanBodyBones.LeftToes:
            case HumanBodyBones.Hips:
            case HumanBodyBones.Spine:
            case HumanBodyBones.Neck:
                return true;
            default:
                return false;
        }
    }
    private bool IsHeadBone(HumanBodyBones bone)
    {
        switch (bone)
        {
            case HumanBodyBones.Head:
                return true;
            default:
                return false;
        }
    }
    private bool IsNeckBone(HumanBodyBones bone)
    {
        switch (bone)
        {
            case HumanBodyBones.Neck:
                return true;
            default:
                return false;
        }
    }
    private bool IsToesBone(HumanBodyBones bone)
    {
        switch (bone)
        {
            case HumanBodyBones.LeftToes:
                return true;
            default:
                return false;
        }
    }
    private void OnApplicationQuit()
    {
        udpClient.Close();
    }
}