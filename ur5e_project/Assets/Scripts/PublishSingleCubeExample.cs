using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Moveit;
using RosMessageTypes.Shape;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;

public class PublishSingleCubeExample : MonoBehaviour
{
    public string topic = "/collision_object";
    public string frameId = "base_link";

    private ROSConnection ros;
    public BoxCollider boxCollider;
    
    private Vector3 position;
    private Quaternion rotation;    
    private Vector3 size;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<CollisionObjectMsg>(topic);
        position = new Vector3(boxCollider.transform.position.x, boxCollider.transform.position.y, boxCollider.transform.position.z); //boxCollider.transform.position;
        rotation = new Quaternion(boxCollider.transform.rotation.x, boxCollider.transform.rotation.y, boxCollider.transform.rotation.z, boxCollider.transform.rotation.w);
        size = Vector3.Scale(boxCollider.size, boxCollider.transform.lossyScale);

        string cubeId = "test_cube_in_start";
        PublishCube(cubeId, position, rotation, size);
    }

    void Update() { 
        if (Input.GetKeyDown(KeyCode.G)){ // press G to publish    
            string cubeId = "test_cube";
            PublishCube(cubeId, position, rotation, size);
        }
    }

    void PublishCube(string id, Vector3 position, Quaternion rotation, Vector3 size)
    {
        // 1. Convert Unity → ROS
        Vector3 pos_ros = RosUnityConverter.UnityToRosPosition(position);
        Quaternion rot_ros = RosUnityConverter.UnityToRosRotation(rotation);

        // 2. Primitive description
        SolidPrimitiveMsg primitive = new SolidPrimitiveMsg
        {
            type = SolidPrimitiveMsg.BOX,
            dimensions = new double[]
            {
                size.x,
                size.y,
                size.z
            }
        };

        // 3. Main pose (use ROS-converted values)
        PoseMsg objectPose = new PoseMsg(
            new PointMsg(pos_ros.x, pos_ros.y, pos_ros.z),
            new QuaternionMsg(rot_ros.x, rot_ros.y, rot_ros.z, rot_ros.w)
        );

        // 4. Primitive pose MUST be identity for MoveIt
        PoseMsg identity = new PoseMsg(
            new PointMsg(0.0f, 0.0f, 0.0f),
            new QuaternionMsg(0.0f, 0.0f, 0.0f, 1.0f)
        );

        // 5. Final CollisionObjectMsg
        CollisionObjectMsg msg = new CollisionObjectMsg
        {
            header = new HeaderMsg { frame_id = frameId },
            id = id,
            operation = CollisionObjectMsg.ADD,
            pose = objectPose,
            primitives = new[] { primitive },
            primitive_poses = new[] { identity },
            meshes = new MeshMsg[0],
            mesh_poses = new PoseMsg[0],
            planes = new PlaneMsg[0],
            plane_poses = new PoseMsg[0],
            subframe_names = new string[0],
            subframe_poses = new PoseMsg[0],
        };

        ros.Publish(topic, msg);
        Debug.Log($"[PublishSingleCube] Published '{id}' at ROS {pos_ros}");
    }
}
