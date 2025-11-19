using UnityEngine;
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Moveit;
using RosMessageTypes.Shape;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.Visualization;


public class PublisherObstaclesFailed : MonoBehaviour
{
    private string topic = "/collision_object";
    private string frameId = "base_link";
    private float publishInterval = 1.0f;  // 1 Hz update rate
    private float keepAliveAddInterval = 20.0f; // 0.05 Hz update rate

    public LayerMask robotLayer;
    public string noCollisionTag = "noCollision";

    ROSConnection ros;

    private Dictionary<string, float> lastAddTimes = new Dictionary<string, float>();
    private Dictionary<string, Pose> tracked = new Dictionary<string, Pose>();
    private float timer = 0f;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PlanningSceneMsg>(topic);
        ros.RegisterPublisher<MarkerArrayMsg>("/obstacle_markers");

    }

    void Update()
    {
        // if (!ros.HasConnection())
        //     return;
        timer += Time.deltaTime;
        if (timer >= publishInterval)
        {
            timer = 0f;
            AutoUpdateScene();
        }
    }

    // ----------------------------------------------------------------------
    // AUTO UPDATE SYSTEM
    // ----------------------------------------------------------------------
    void AutoUpdateScene()
    {
        List<CollisionObjectMsg> updates = new List<CollisionObjectMsg>();

        // 1) Scan all colliders in the scene
        HashSet<string> currentObjects = new HashSet<string>();

        foreach (Collider col in FindObjectsOfType<Collider>())
        {
            if (!IsObstacle(col)) continue;

            string id = col.gameObject.name;
            currentObjects.Add(id);

            Pose currentPose = new Pose(col.transform.position, col.transform.rotation);

            // NEW → ADD
            if (!tracked.ContainsKey(id))
            {
                CollisionObjectMsg addMsg = CreateAdd(col);
                if (addMsg != null)
                {
                    updates.Add(addMsg);
                    tracked[id] = currentPose;
                }
                continue;
            }

            // EXISTING → check if moved
            Pose previous = tracked[id];
            if (HasMoved(previous, currentPose))
            {
                CollisionObjectMsg moveMsg = CreateMove(id, currentPose);
                updates.Add(moveMsg);
                tracked[id] = currentPose;
            }

            // ---- INSERT KEEP-ALIVE ADD HERE ----
            if (!lastAddTimes.ContainsKey(id))
                lastAddTimes[id] = Time.time;

            if ((Time.time - lastAddTimes[id]) > keepAliveAddInterval)
            {
                CollisionObjectMsg addAgain = CreateAdd(col);
                if (addAgain != null)
                {
                    updates.Add(addAgain);
                    lastAddTimes[id] = Time.time;
                }
            }
        }

        // 2) REMOVE objects no longer in scene
        List<string> toRemove = new List<string>();
        foreach (var kv in tracked)
        {
            if (!currentObjects.Contains(kv.Key))
            {
                updates.Add(CreateRemove(kv.Key));
                toRemove.Add(kv.Key);
            }
        }

        foreach (string r in toRemove)
            tracked.Remove(r);

        // 3) Nothing changed? → don't publish
        if (updates.Count == 0)
            return;

        // 4) Publish diff PlanningScene
        PlanningSceneMsg msg = new PlanningSceneMsg
        {
            is_diff = true,
            world = new PlanningSceneWorldMsg
            {
                collision_objects = updates.ToArray()
            }
        };

        ros.Publish(topic, msg);
        Debug.Log($"[AUTO UPDATE] {updates.Count} updates published");
        PublishMarkers();
    }

    // ----------------------------------------------------------------------
    // HELPERS
    // ----------------------------------------------------------------------

    bool IsObstacle(Collider col)
    {
        if (col.CompareTag(noCollisionTag)) return false;
        if (((1 << col.gameObject.layer) & robotLayer) != 0) return false;
        return true;
    }

    bool HasMoved(Pose a, Pose b, float posThr = 0.001f, float rotThr = 0.5f)
    {
        if (Vector3.Distance(a.position, b.position) > posThr)
            return true;

        if (Quaternion.Angle(a.rotation, b.rotation) > rotThr)
            return true;

        return false;
    }

    // ----------------------------------------------------------------------
    // ADD OBJECT
    // ----------------------------------------------------------------------
    CollisionObjectMsg CreateAdd(Collider col)
    {
        CollisionObjectMsg obj = new CollisionObjectMsg
        {
            id = col.gameObject.name,
            header = new HeaderMsg { frame_id = frameId },
            operation = CollisionObjectMsg.ADD
        };

        // Pose of whole object
        obj.pose = new PoseMsg(
            new PointMsg(col.transform.position.x, col.transform.position.y, col.transform.position.z),
            new QuaternionMsg(col.transform.rotation.x, col.transform.rotation.y, col.transform.rotation.z, col.transform.rotation.w)
        );

        // Default empty fields
        obj.meshes = new MeshMsg[0];
        obj.mesh_poses = new PoseMsg[0];
        obj.planes = new PlaneMsg[0];
        obj.plane_poses = new PoseMsg[0];
        obj.subframe_names = new string[0];
        obj.subframe_poses = new PoseMsg[0];

        // Geometry
        if (col is BoxCollider box)
        {
            SolidPrimitiveMsg prim = new SolidPrimitiveMsg
            {
                type = SolidPrimitiveMsg.BOX,
                dimensions = new double[]
                {
                    box.size.x * col.transform.lossyScale.x,
                    box.size.y * col.transform.lossyScale.y,
                    box.size.z * col.transform.lossyScale.z
                }
            };

            obj.primitives = new[] { prim };
            obj.primitive_poses = new[]
            {
                new PoseMsg(
                    new PointMsg(0,0,0),
                    new QuaternionMsg(0,0,0,1)
                )
            };
        }
        else
        {
            Debug.LogWarning("Unsupported collider type: " + col.GetType());
            return null;
        }

        Debug.Log($"[ADD] {obj.id}");
        return obj;
    }

    // ----------------------------------------------------------------------
    // MOVE OBJECT
    // ----------------------------------------------------------------------
    CollisionObjectMsg CreateMove(string id, Pose newPose)
    {
        CollisionObjectMsg obj = new CollisionObjectMsg
        {
            id = id,
            header = new HeaderMsg { frame_id = frameId },
            operation = CollisionObjectMsg.MOVE,

            pose = new PoseMsg(
                new PointMsg(newPose.position.x, newPose.position.y, newPose.position.z),
                new QuaternionMsg(newPose.rotation.x, newPose.rotation.y, newPose.rotation.z, newPose.rotation.w)
            ),

            // MUST BE EMPTY for MOVE
            primitives = new SolidPrimitiveMsg[0],
            primitive_poses = new PoseMsg[0],
            meshes = new MeshMsg[0],
            mesh_poses = new PoseMsg[0],
            planes = new PlaneMsg[0],
            plane_poses = new PoseMsg[0],
            subframe_names = new string[0],
            subframe_poses = new PoseMsg[0]
        };

        Debug.Log($"[MOVE] {id}");
        return obj;
    }

    // ----------------------------------------------------------------------
    // REMOVE OBJECT
    // ----------------------------------------------------------------------
    CollisionObjectMsg CreateRemove(string id)
    {
        Debug.Log($"[REMOVE] {id}");
        return new CollisionObjectMsg
        {
            id = id,
            header = new HeaderMsg { frame_id = frameId },
            operation = CollisionObjectMsg.REMOVE,

            // Must be empty
            primitives = new SolidPrimitiveMsg[0],
            primitive_poses = new PoseMsg[0],
            meshes = new MeshMsg[0],
            mesh_poses = new PoseMsg[0],
            planes = new PlaneMsg[0],
            plane_poses = new PoseMsg[0],
            subframe_names = new string[0],
            subframe_poses = new PoseMsg[0]
        };
    }

    MarkerMsg CreateMarker(Collider col, int id)
    {
        MarkerMsg marker = new MarkerMsg();

        marker.header.frame_id = frameId;
        marker.ns = "unity_obstacles";
        marker.id = id;
        marker.action = MarkerMsg.ADD;
        marker.type = MarkerMsg.CUBE;
        marker.lifetime = new RosMessageTypes.BuiltinInterfaces.DurationMsg(0,0);

        // Pose
        marker.pose = new PoseMsg(
            new PointMsg(col.transform.position.x, col.transform.position.y, col.transform.position.z),
            new QuaternionMsg(col.transform.rotation.x, col.transform.rotation.y, col.transform.rotation.z, col.transform.rotation.w)
        );

        // Scale (Unity lossyScale accounts for parent transform)
        Vector3 s = Vector3.one;
        if (col is BoxCollider box)
            s = Vector3.Scale(box.size, col.transform.lossyScale);

        marker.scale = new Vector3Msg(s.x, s.y, s.z);

        // Color
        marker.color = new RosMessageTypes.Std.ColorRGBAMsg { r = 0f, g = 1f, b = 0f, a = 0.4f };

        return marker;
    }


    void PublishMarkers()
    {
        MarkerArrayMsg array = new MarkerArrayMsg();
        List<MarkerMsg> markers = new List<MarkerMsg>();

        int markerId = 0;
        foreach (var kv in tracked)
        {
            string id = kv.Key;
            Collider col = GameObject.Find(id)?.GetComponent<Collider>();
            if (col == null) continue;

            markers.Add(CreateMarker(col, markerId));
            markerId++;
        }

        array.markers = markers.ToArray();

        ros.Publish("/obstacle_markers", array);
    }

}
