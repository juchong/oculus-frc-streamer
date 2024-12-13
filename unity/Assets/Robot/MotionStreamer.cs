/* Unity-based example for publishing Quest headset data to FRC-compatible Network Tables */
/* Juan Chong - 2024 */
using UnityEngine;
using NetworkTables;

/* Extend Vector3 with a ToArray() function */
public static class VectorExtensions
{
    public static float[] ToArray(this Vector3 vector)
    {
        return new float[] { vector.x, vector.y, vector.z };
    }
}

/* Extend Quaternion with a ToArray() function */
public static class QuaternionExtensions
{
    public static float[] ToArray(this Quaternion quaternion)
    {
        return new float[] { quaternion.x, quaternion.y, quaternion.z, quaternion.w };
    }
}

public class MotionStreamer : MonoBehaviour
{
    /* Initialize local variables */
    public int frameIndex; // Local variable to store the headset frame index
    public double timeStamp; // Local variable to store the headset timestamp
    public Vector3 position; // Local variable to store the headset position in 3D space
    public Quaternion rotation; // Local variable to store the headset rotation in quaternion form
    public Vector3 eulerAngles; // Local variable to store the headset rotation in Euler angles
    public OVRCameraRig cameraRig;
    public Nt4Source frcDataSink = null;
    private long command = 0;

    [SerializeField] public Transform poseOffset;

    [SerializeField] public Transform vrCamera; // The VR camera transform
    [SerializeField] public Transform vrCameraRoot; // The root of the camera transform
    [SerializeField] public Transform resetTransform; // The desired position & rotation (look direction) for your player
    private float batteryLevel; // Local variable to store the headset battery level

    /* NT configuration settings */
    private readonly string appName = "Quest3S"; // A fun name to ID the client in the robot logs
    private readonly string serverAddress = "10.99.99.2"; // RoboRIO IP Address (typically 10.TE.AM.2)
    private readonly int serverPort = 5810; // Typically 5810
    private int delayCounter = 0; // Counter used to delay checking for commands from the robot

    void Start()
    {
        ConnectToRobot();
    }

    void LateUpdate()
    {
        if (frcDataSink.Client.Connected())
        {
            PublishFrameData();

            if (delayCounter >= 0)
            {
                ProcessCommands();
                delayCounter = 0;
            }
            else
            {
                delayCounter++;
            }
        }
        else
        {
            HandleDisconnectedState();
        }
    }

    // Connect to the RoboRIO and publish topics
    private void ConnectToRobot()
    {
        UnityEngine.Debug.Log("[MotionStreamer] Attempting to connect to the RoboRIO at " + serverAddress + ".");
        frcDataSink = new Nt4Source(appName, serverAddress, serverPort);
        PublishTopics();
    }

    // Handle the disconnected state (RIO reboot, code restart, etc.)
    private void HandleDisconnectedState()
    {
        UnityEngine.Debug.Log("[MotionStreamer] Robot disconnected. Resetting connection and attempting to reconnect...");
        frcDataSink.Client.Disconnect();
        ConnectToRobot();
    }

    // Publish topics to Network Tables
    private void PublishTopics()
    {
        frcDataSink.PublishTopic("/oculus/miso", "int");
        frcDataSink.PublishTopic("/oculus/frameCount", "int");
        frcDataSink.PublishTopic("/oculus/timestamp", "double");
        frcDataSink.PublishTopic("/oculus/position", "float[]");
        frcDataSink.PublishTopic("/oculus/quaternion", "float[]");
        frcDataSink.PublishTopic("/oculus/eulerAngles", "float[]");
        frcDataSink.PublishTopic("/oculus/batteryLevel", "double");
        frcDataSink.Subscribe("/oculus/mosi", 0.1, false, false, false);
        frcDataSink.Subscribe("/oculus/init/position", 0.1, false, false, false);
        frcDataSink.Subscribe("/oculus/init/eulerAngles", 0.1, false, false, false);
    }

    // Publish the Quest pose data to Network Tables
    private void PublishFrameData()
    {
        frameIndex = UnityEngine.Time.frameCount;
        timeStamp = UnityEngine.Time.time;
        position = cameraRig.centerEyeAnchor.position;
        rotation = cameraRig.centerEyeAnchor.rotation;
        eulerAngles = cameraRig.centerEyeAnchor.eulerAngles;
        batteryLevel = SystemInfo.batteryLevel;

        if (poseOffset != null)
        {
            position += poseOffset.position;
            rotation = poseOffset.rotation * rotation;
        }

        frcDataSink.PublishValue("/oculus/frameCount", frameIndex);
        frcDataSink.PublishValue("/oculus/timestamp", timeStamp);
        frcDataSink.PublishValue("/oculus/position", position.ToArray());
        frcDataSink.PublishValue("/oculus/quaternion", rotation.ToArray());
        frcDataSink.PublishValue("/oculus/eulerAngles", eulerAngles.ToArray());
        frcDataSink.PublishValue("/oculus/batteryLevel", batteryLevel);
    }

    // Process commands from the robot
    private void ProcessCommands()
    {
        command = frcDataSink.GetLong("/oculus/mosi");
        switch (command)
        {
            case 1:
                RecenterPlayer();
                UnityEngine.Debug.Log("[MotionStreamer] Processed a heading reset request.");
                frcDataSink.PublishValue("/oculus/miso", 99);
                break;
            case 2:
                InitPose();
                UnityEngine.Debug.Log("[MotionStreamer] Processed a pose reset request.");
                frcDataSink.PublishValue("/oculus/miso", 99);
                break;
            default:
                frcDataSink.PublishValue("/oculus/miso", 0);
                break;
        }
    }

    // Clean up if the app crashes or is stopped
    void OnApplicationQuit()
    {

    }

    // Transform the HMD's rotation to virtually "zero" the robot position. Similar result as long-pressing the Oculus button.
    void RecenterPlayer()
    {
        float rotationAngleY = vrCamera.rotation.eulerAngles.y - resetTransform.rotation.eulerAngles.y;

        vrCameraRoot.transform.Rotate(0, -rotationAngleY, 0);

        Vector3 distanceDiff = resetTransform.position - vrCamera.position;
        vrCameraRoot.transform.position += distanceDiff;

    }

    void InitPose()
    {
        Double[] letpositionArray = frcDataSink.GetDoubleArray("/oculus/init/position");
        Double [] eulerAnglesArray = frcDataSink.GetDoubleArray("/oculus/init/eulerAngles");
        
        poseOffset.position = new Vector3((float)letpositionArray[0], (float)letpositionArray[1], (float)letpositionArray[2]);
        Quaternion quaternion = new Quaternion((float)eulerAnglesArray[0], (float)eulerAnglesArray[1], (float)eulerAnglesArray[2], (float)eulerAnglesArray[3]);
        poseOffset.rotation = quaternion;
                
    }
}