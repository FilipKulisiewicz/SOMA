using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Moveit;
using RosMessageTypes.Shape;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using System;

public class CollisionObjManager : MonoBehaviour
{
    public static CollisionObjManager Instance { get; private set; }
    public string collisionTopic = "/collision_object";
    public string frameId = "base_link";

    public LayerMask robotLayer;
    public string noCollisionTag = "noCollision";

    public float publishInterval = 50f;
    private float timer = 0f;

    private ROSConnection ros;
    public SceneObjectsSubscriber sceneObjTracker;

    void Start()
    {
        Instance = this;
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<CollisionObjectMsg>(collisionTopic);
        // ros.RegisterPublisher<AttachedCollisionObjectMsg>(attachedTopic);
        PublishSceneCubes();
    }

    void Update()
    {
        // Periodic auto-update
        timer += Time.deltaTime;
        if (timer >= publishInterval)
        {
            timer = 0f;
            PublishSceneCubes();
        }

        // Manual publish
        if (Input.GetKeyDown(KeyCode.G))
        {
            PublishSceneCubes();
        }
    }

    // =======================================================
    // Find cubes and publish as CollisionObjects
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
            PublishCube(col);
        }
    }

    public void PublishCube(BoxCollider col, sbyte operation = -1)
    {
        if (!IsObstacle(col))
            return;
        string cubeId = col.gameObject.name;
        if (!sceneObjTracker.IsAttached(cubeId)){
            PublishCollisionObj(col);
        }
    }

    bool IsObstacle(BoxCollider col)
    {
        if (col.CompareTag(noCollisionTag)) return false;
        if (((1 << col.gameObject.layer) & robotLayer) != 0) return false;
        return true;
    }

    void PublishCollisionObj(BoxCollider col, sbyte operation = -1)
    {
        string cubeId = col.gameObject.name;
        Transform t = col.transform;
        Vector3 colCenter = t.TransformPoint(col.center);
        Vector3 rosPos = RosUnityConverter.UnityToRosPosition(colCenter);
        Quaternion rosRot = RosUnityConverter.UnityToRosRotation(t.rotation);
        Vector3 scaledSize = Vector3.Scale(col.size, t.lossyScale);
        Vector3 rosScale = RosUnityConverter.UnityToRosScale(scaledSize);

        if (operation == -1){
            operation = sceneObjTracker.Exists(cubeId)
                ? CollisionObjectMsg.MOVE 
                : CollisionObjectMsg.ADD;
        }
        if(operation == CollisionObjectMsg.REMOVE){
            var msg = MakeCollisionObjectMsg(cubeId, operation: CollisionObjectMsg.REMOVE);
            if (sceneObjTracker.Exists(cubeId)){
                sceneObjTracker.DeleteSceneObject(cubeId);
            }
            ros.Publish(collisionTopic, msg);
        }
        else{
            var msg = MakeCollisionObjectMsg(cubeId, rosPos, rosRot, rosScale, operation);
            ros.Publish(collisionTopic, msg);
        }
    }

    // =======================================================
    // Generic CollisionObjectMsg constructor
    // =======================================================
    CollisionObjectMsg MakeCollisionObjectMsg(string id, Vector3 ros_position = default, Quaternion ros_rotation = default, Vector3 ros_scale = default, sbyte operation = -1)
    {
        SolidPrimitiveMsg primitive = new SolidPrimitiveMsg
        {
            type = SolidPrimitiveMsg.BOX,
            dimensions = new double[] { ros_scale.x, ros_scale.y, ros_scale.z }
        };

        PoseMsg objectPose = new PoseMsg(
            new PointMsg(ros_position.x, ros_position.y, ros_position.z),
            new QuaternionMsg(ros_rotation.x, ros_rotation.y, ros_rotation.z, ros_rotation.w)
        );

        PoseMsg identity = new PoseMsg(
            new PointMsg(0.0f, 0.0f, 0.0f),
            new QuaternionMsg(0.0f, 0.0f, 0.0f, 1.0f)
        );

        CollisionObjectMsg msg = new CollisionObjectMsg
        {
            header = new HeaderMsg { frame_id = frameId },
            id = id,
            pose = objectPose,
            primitives = new SolidPrimitiveMsg[] { primitive },
            primitive_poses = new PoseMsg[] { identity },

            meshes = new MeshMsg[0],
            mesh_poses = new PoseMsg[0],
            planes = new PlaneMsg[0],
            plane_poses = new PoseMsg[0],
            subframe_names = new string[0],
            subframe_poses = new PoseMsg[0],
            operation = operation,
        };
        // Debug.Log($"[MakeCollisionObjectMsg] Published '{id}' with op={operation}");
        return msg;
    }
}
