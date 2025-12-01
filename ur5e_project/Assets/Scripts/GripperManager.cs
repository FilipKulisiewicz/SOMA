using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Moveit;
using RosMessageTypes.Shape;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using System;

public class FakeGripperManager : MonoBehaviour
{
    [Header("Gripper Settings")] public Transform endEffectorFrame;
    public KeyCode pickKey = KeyCode.P;
    public float maxPickDistance = 1.0f;
    public float maxObjectSize = 0.1f;

    [Header("ROS")] public string worldFrame = "world";
    public string robotLink = "tool0";
    public string attachedTopic = "/attached_collision_object";

    private ROSConnection ros;
    private Transform heldObject;
    private Transform originalParent;
    private string heldId;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        // Required publishers
        // ros.RegisterPublisher<CollisionObjectMsg>(collisionTopic);
        ros.RegisterPublisher<AttachedCollisionObjectMsg>(attachedTopic);
    }

    void Update()
    {
        if (Input.GetKeyDown(pickKey))
        {
            if (heldObject == null) TryPick(); else ReleaseObject();
        }
    }

    // ---------------------------------------------------------------
    // PICK
    // ---------------------------------------------------------------
    void TryPick()
    {
        if (!Physics.Raycast(endEffectorFrame.position, endEffectorFrame.forward,
                out var hit, maxPickDistance)) return;

        var obj = hit.collider.transform;
        var bounds = hit.collider.bounds.size;
        if (bounds.x > maxObjectSize || bounds.y > maxObjectSize || bounds.z > maxObjectSize) return;

        heldObject = obj;
        originalParent = obj.parent;
        heldId = heldObject.name;

        // REMOVE from world in MoveIt
        CollisionObjManager.Instance.PublishCube(heldObject.GetComponent<BoxCollider>(), CollisionObjectMsg.REMOVE);
        CollisionObjManager.Instance.sceneObjTracker.DeleteSceneObject(heldId);
        // ATTACH to robot
        PublishAttach(heldId, robotLink);

        // Unity parent
        heldObject.SetParent(endEffectorFrame, true);
        Debug.Log($"Picked {heldId}");
    }

    // ---------------------------------------------------------------
    // RELEASE
    // ---------------------------------------------------------------
    void ReleaseObject()
    {
        if (heldObject == null) return;

        // Store world pose
        Vector3 pos = heldObject.position;
        Quaternion rot = heldObject.rotation;

        // REMOVE attached in MoveIt
        PublishDetach(heldId, robotLink);
        // ADD back to world
        CollisionObjManager.Instance.PublishCube(heldObject.GetComponent<BoxCollider>(), CollisionObjectMsg.ADD);

        // Unity unparent
        heldObject.SetParent(originalParent, true);
        Debug.Log($"Released {heldId}");

        heldObject = null;
    }
    
    void PublishAttach(string id, string link)
    {
        var msg = new AttachedCollisionObjectMsg
        {
            link_name = link,
            @object = new CollisionObjectMsg { id = id, operation = CollisionObjectMsg.ADD }
        };
        ros.Publish(attachedTopic, msg);
    }

    void PublishDetach(string id, string link)
    {
        var msg = new AttachedCollisionObjectMsg
        {
            link_name = link,
            @object = new CollisionObjectMsg { id = id, operation = CollisionObjectMsg.REMOVE }
        };

        ros.Publish(attachedTopic, msg);
    }
}
