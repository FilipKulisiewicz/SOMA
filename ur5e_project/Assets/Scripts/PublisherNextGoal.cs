using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class RaycastGoalPublisher : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "/next_goal";

    [Header("Robot reference frame")]
    public Transform robotEnvironment;
    // Transform that defines robot base_link frame in Unity

    [Header("Goal adjustments")]
    public Vector3 positionOffsetRobot = new Vector3(0f, 0f, 0.2f);
    // Offset added in ROBOT frame after transforming the clicked object

    private bool useCustomOrientation = true;
    public Vector3 customEulerAnglesRobot = new Vector3(180f, 0f, 0f);
    // Example: 180° around X → end-effector faces down

    public float rayDistance = 100f;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PoseStampedMsg>(topicName);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryPublishGoalFromClick();
        }
    }

    void TryPublishGoalFromClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            PublishGoal(hit.transform);
        }
    }

    void PublishGoal(Transform clickedObject)
    {
        // 1) Convert clicked object's world pose → robot frame pose
        Vector3 posInRobot = robotEnvironment.InverseTransformPoint(clickedObject.position);
        Quaternion rotInRobot = Quaternion.Inverse(robotEnvironment.rotation) * clickedObject.rotation;

        // 2) Apply position offset (offset also in robot coordinates)
        posInRobot += positionOffsetRobot;

        // 3) Apply orientation override (if enabled)
        if (useCustomOrientation)
        {
            Quaternion customRot = Quaternion.Euler(customEulerAnglesRobot);
            rotInRobot = customRot;
        }

        // 4) Convert to ROS coordinates
        posInRobot = RosUnityConverter.UnityToRosPosition(posInRobot);
        rotInRobot = RosUnityConverter.UnityToRosRotation(rotInRobot); 

        // 5) Build ROS PoseStamped message
        PoseStampedMsg msg = new PoseStampedMsg();
        msg.header.stamp.sec = (int)Time.time;
        msg.header.frame_id = "base_link";  // robot coordinate frame

        msg.pose.position.x = posInRobot.x;
        msg.pose.position.y = posInRobot.y;
        msg.pose.position.z = posInRobot.z;

        msg.pose.orientation.x = rotInRobot.x;
        msg.pose.orientation.y = rotInRobot.y;
        msg.pose.orientation.z = rotInRobot.z;
        msg.pose.orientation.w = rotInRobot.w;

        ros.Publish(topicName, msg);

        Debug.Log(
            $"Published GOAL for {clickedObject.name} | Pos (robot): {posInRobot} | Rot (robot): {rotInRobot.eulerAngles}"
        );
    }
}
