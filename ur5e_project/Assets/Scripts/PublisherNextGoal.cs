using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class RaycastGoalPublisher : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "/next_goal";

    [Header("Robot reference frame")]
    public Transform robotEnvironment;

    [Header("Goal adjustments")]
    public float aboveOffset = 0.002f; // = 2mm , 0.01f = 1cm;  
    public Vector3 customEulerAnglesRobot = new Vector3(180f, 0f, 0f);
    public bool useCustomOrientation = true;

    public float rayDistance = 100f;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PoseStampedMsg>(topicName);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryPublishGoalFromClick();
    }

    // ----------------------------------------------------------------------
    // MAIN CLICK LOGIC
    // ----------------------------------------------------------------------

    void TryPublishGoalFromClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
            PublishGoal(hit.transform);
    }

    // ----------------------------------------------------------------------
    // MAIN GOAL PUBLISH LOGIC
    // ----------------------------------------------------------------------

    void PublishGoal(Transform obj)
    {
        Vector3 worldHighest = GetHighestPointWorld(obj);
        worldHighest += Vector3.up * aboveOffset;

        Vector3 posRobot = ConvertPointToRobotFrame(worldHighest);
        Quaternion rotRobot = GetGoalOrientation(obj);

        Vector3 posRos = RosUnityConverter.UnityToRosPosition(posRobot);
        Quaternion rotRos = RosUnityConverter.UnityToRosRotation(rotRobot);

        PoseStampedMsg msg = BuildPoseStamped(posRos, rotRos);
        ros.Publish(topicName, msg);

        Debug.Log($"Goal ABOVE {obj.name}");
    }

    // ----------------------------------------------------------------------
    // POSITION HELPERS
    // ----------------------------------------------------------------------

    Vector3 GetHighestPointWorld(Transform obj)
    {
        Collider col = obj.GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning("Object has no collider");
            return obj.position;
        }

        Bounds b = col.bounds;
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    Vector3 ConvertPointToRobotFrame(Vector3 worldPoint)
    {
        return robotEnvironment.InverseTransformPoint(worldPoint);
    }

    // ----------------------------------------------------------------------
    // ORIENTATION HELPERS
    // ----------------------------------------------------------------------

    Quaternion GetGoalOrientation(Transform obj)
    {
        if (useCustomOrientation)
        {
            return Quaternion.Euler(customEulerAnglesRobot);
        }

        return Quaternion.Inverse(robotEnvironment.rotation) * obj.rotation;
    }

    // ----------------------------------------------------------------------
    // ROS MESSAGE BUILDER
    // ----------------------------------------------------------------------

    PoseStampedMsg BuildPoseStamped(Vector3 pos, Quaternion rot)
    {
        PoseStampedMsg msg = new PoseStampedMsg();
        msg.header.stamp.sec = (int)Time.time;
        msg.header.frame_id = "base_link";

        msg.pose.position.x = pos.x;
        msg.pose.position.y = pos.y;
        msg.pose.position.z = pos.z;

        msg.pose.orientation.x = rot.x;
        msg.pose.orientation.y = rot.y;
        msg.pose.orientation.z = rot.z;
        msg.pose.orientation.w = rot.w;

        return msg;
    }
}
