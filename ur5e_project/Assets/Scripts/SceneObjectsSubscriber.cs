using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.My;   // <-- adjust namespace if needed
using RosMessageTypes.Moveit;

public class SceneObjectsSubscriber : MonoBehaviour
{
    ROSConnection ros;
    public string topic = "/scene_object_list";
    public string attachedTopic = "/attached_collision_object";

    public bool full_msg_each_time = false;

    // Storage for scene object IDs
    readonly HashSet<string> objectsInScene = new HashSet<string>();
    readonly HashSet<string> attachedObjects = new HashSet<string>();


    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        ros.Subscribe<SceneObjectsMsg>(
            topic,
            CallbackSceneObjects
        );
        ros.Subscribe<AttachedCollisionObjectMsg>(
            attachedTopic,
            CallbackAttachedObjects
        );
    }

    void CallbackAttachedObjects(AttachedCollisionObjectMsg msg)
    {
        if (msg.@object.id != null)
        {
            if (!string.IsNullOrEmpty(msg.@object.id))
                if (msg.@object.operation == CollisionObjectMsg.ADD)
                    attachedObjects.Add(msg.@object.id);
                else if (msg.@object.operation == CollisionObjectMsg.REMOVE)
                    attachedObjects.Remove(msg.@object.id);
                Debug.Log($"[SceneObjectsSubscriber] attachedObjects: {attachedObjects.Count} objects");
        }
    }

    void CallbackSceneObjects(SceneObjectsMsg msg)
    {
        if (full_msg_each_time){
            objectsInScene.Clear();
        }

        if (msg.ids != null)
        {
            foreach (string id in msg.ids)
            {
                if (!string.IsNullOrEmpty(id))
                    objectsInScene.Add(id);
            }
        }
        Debug.Log($"[SceneObjectsSubscriber] objectsInScene: {objectsInScene.Count} objects");
    }

    public void DeleteSceneObject(string id)
    {
        if (objectsInScene.Contains(id))
        {
            objectsInScene.Remove(id);
            Debug.Log($"[SceneObjectsSubscriber] Removed object: {id}");
        }
    }

    /// <summary>
    /// Check if an object exists in the current scene.
    /// </summary>
    public bool Exists(string id)
    {
        return objectsInScene.Contains(id);
    }
    
    public bool IsAttached(string id)
    {
        return attachedObjects.Contains(id);
    }
}
