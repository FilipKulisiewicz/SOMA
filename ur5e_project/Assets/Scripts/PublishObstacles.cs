using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Moveit;
using RosMessageTypes.Shape;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using System;

public class PublishObstacles : MonoBehaviour
{
    public string topic = "/collision_object";
    public string frameId = "base_link";
    // public Quaternion rot_quat = new Quaternion(1.0f, 0.0f, 0.0f, 0.0f); // to convert Unity to ROS

    public LayerMask robotLayer;
    public string noCollisionTag = "noCollision";

    private ROSConnection ros;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<CollisionObjectMsg>(topic);
        PublishSceneCubes();
    }

    // public static Quaternion UnityToRosRotation(Quaternion qUnity, Quaternion rot_quat)
    // {
    //     Quaternion qUnity_corrected = new Quaternion(
    //         -qUnity.z,     // x'
    //         -qUnity.x,    // y'
    //         qUnity.y,     // z'
    //         qUnity.w      // w'
    //     );
    //     Quaternion asn = rot_quat * qUnity_corrected;
    //     return asn;
    // }

    // =======================================================
    // Called by Unity UI Button → assign in Inspector
    // =======================================================
    void Update() { 
        if (Input.GetKeyDown(KeyCode.G)){ // press G to publish    
            PublishSceneCubes();
        }
    }

    // =======================================================
    // Find first cube (BoxCollider) in scene and publish
    // =======================================================
   void PublishSceneCubes()
    {
        BoxCollider[] boxes = FindObjectsOfType<BoxCollider>();

        if (boxes.Length == 0)
        {
            Debug.LogWarning("[PublishSceneCubes] No BoxColliders found!");
            return;
        }

        foreach (var col in boxes)
        {
            if (!IsObstacle(col)) 
                continue;  // Skip robot parts or excluded tags

            Transform t = col.transform;

            // Convert position/orientation using your helper
            Vector3 rosPos = RosUnityConverter.UnityToRosPosition(t.position);
            Quaternion rosRot = RosUnityConverter.UnityToRosRotation(t.rotation);
            
            // Quaternion rosRot = UnityToRos(t.rotation);
            // rosRot = rot_quat * rosRot;
            // Compute world-scaled size
            Vector3 scaledSize = Vector3.Scale(col.size, t.lossyScale);
            // Vector3 rosScale = scaledSize;
            Vector3 rosScale = RosUnityConverter.UnityToRosScale(scaledSize);
            string cubeId = col.gameObject.name;

            PublishCube(cubeId, rosPos, rosRot, rosScale);
        }
    }

    bool IsObstacle(BoxCollider col)
    {
        if (col.CompareTag(noCollisionTag)) return false;
        if (((1 << col.gameObject.layer) & robotLayer) != 0) return false;
        return true;
    }

    // =======================================================
    // Main publisher
    // =======================================================
    void PublishCube(string id, Vector3 ros_position, Quaternion ros_rotation, Vector3 ros_scale)
    {
        // 1. Primitive description
        SolidPrimitiveMsg primitive = new SolidPrimitiveMsg
        {
            type = SolidPrimitiveMsg.BOX,
            dimensions = new double[]
            {
                ros_scale.x,
                ros_scale.y,
                ros_scale.z
            }
        };

        // 2. Main pose (use ROS-converted values)
        PoseMsg objectPose = new PoseMsg(
            new PointMsg(ros_position.x, ros_position.y, ros_position.z),
            new QuaternionMsg(ros_rotation.x, ros_rotation.y, ros_rotation.z, ros_rotation.w)
        );

        // 3. Primitive pose MUST be identity for MoveIt

        PoseMsg identity = new PoseMsg(
            new PointMsg(0.0f, 0.0f, 0.0f),
            new QuaternionMsg(0.0f, 0.0f, 0.0f, 1.0f)
        );

        try
        {
            // 4. Final CollisionObjectMsg
            CollisionObjectMsg msg = new CollisionObjectMsg
            {
                header = new HeaderMsg { frame_id = frameId },
                id = id,
                operation = CollisionObjectMsg.ADD,

                // Either use objectPose here OR put it inside primitive_poses
                pose = objectPose,
                primitives = new SolidPrimitiveMsg[] { primitive },
                primitive_poses = new PoseMsg[] { identity },   // correct syntax

                meshes = new MeshMsg[0],
                mesh_poses = new PoseMsg[0],
                planes = new PlaneMsg[0],
                plane_poses = new PoseMsg[0],
                subframe_names = new string[0],
                subframe_poses = new PoseMsg[0],
            };

            ros.Publish(topic, msg);
            Debug.Log($"[PublishSingleCube] Published '{id}' at ROS {ros_position}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PublishSingleCube] Failed to publish collision object '{id}': {ex.Message}\n{ex.StackTrace}");
        }

    }
}
