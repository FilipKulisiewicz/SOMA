using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.UrdfImporter;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;

public class URJointStateSubscriber : MonoBehaviour
{
    public ArticulationBody[] joints;
    private int[] map;
    private bool mapReady = false;

    private float[] lastGoodDeg;

    // Jump-filter fields
    private bool jumpDetected = false;
    private int jumpFrameCounter = 0;

    public int jumpConfirmFrames = 10;  // frames to confirm jump
    public float jumpThresholdDeg = 1.0f; // what counts as a big jump

    void Start()
    {
        ROSConnection.instance.Subscribe<JointStateMsg>("/joint_states", CommandCallback);
        lastGoodDeg = new float[joints.Length];

        // ----------------------------------------------
        // Add damping to all joints to reduce jittering
        // ----------------------------------------------
        foreach (var j in joints)
        {
            var d = j.xDrive;
            d.stiffness = 0.001f;
            d.damping = 20f;       // strong damping → smooth motion
            d.forceLimit = 5000000f;  // prevent weak joints
            j.xDrive = d;
        }
    }

    void CommandCallback(JointStateMsg msg)
    {
        if (!mapReady)
        {
            BuildJointMap(msg);
            return;
        }

        ApplyJointTargets(msg);
    }

    void BuildJointMap(JointStateMsg msg)
    {
        map = new int[msg.name.Length];

        for (int i = 0; i < msg.name.Length; i++)
        {
            for (int j = 0; j < joints.Length; j++)
            {
                var urdf = joints[j].GetComponent<UrdfJoint>();

                if (msg.name[i] == joints[j].name ||
                    (urdf != null && msg.name[i] == urdf.jointName))
                {
                    map[i] = j;
                    break;
                }
            }
        }

        mapReady = true;
    }

    void ApplyJointTargets(JointStateMsg msg)
    {
        float[] deg = new float[msg.position.Length];
        for (int i = 0; i < deg.Length; i++)
            deg[i] = (float)msg.position[i] * Mathf.Rad2Deg;

        if (!FrameIsValid(deg))
            return;

        // Store last valid frame
        for (int i = 0; i < deg.Length; i++)
            lastGoodDeg[i] = deg[i];

        // Apply to Unity articulation bodies
        for (int i = 0; i < deg.Length; i++)
        {
            int idx = map[i];

            var d = joints[idx].xDrive;
            d.target = deg[i];
            joints[idx].xDrive = d;
        }
    }

    bool FrameIsValid(float[] targetDeg)
    {
        float combinedMotion = 0f;

        // Compute combined motion between new frame and last valid frame
        for (int i = 0; i < targetDeg.Length; i++)
        {
            float last = lastGoodDeg[i];
            if (Mathf.Abs(last) < 1e-6f) continue;
            combinedMotion += Mathf.Abs(targetDeg[i] - last);
        }

        //--------------------------------------------------------
        // SIMPLE JUMP FILTER
        //--------------------------------------------------------

        if (!jumpDetected)
        {
            // Sudden big jump was detected
            if (combinedMotion > jumpThresholdDeg)
            {
                jumpDetected = true;
                jumpFrameCounter = 0;
                return false;
            }

            // No jump → valid frame
            return true;
        }
        else
        {
            // Already filtering a jump
            jumpFrameCounter++;

            bool jumpStillPresent = combinedMotion > jumpThresholdDeg;

            if (jumpStillPresent)
            {
                if (jumpFrameCounter >= jumpConfirmFrames)
                {
                    // Debug.Log("Jump confirmed → accepting large change");
                    jumpDetected = false;
                    return true;
                }

                return false;
            }
            else
            {
                Debug.Log("Jump disappeared → rejecting noise");
                jumpDetected = false;
                return false;
            }
        }
    }
}


// public class URJointStateSubscriber : MonoBehaviour
// {
//     public ArticulationBody[] joints;
//     int[] map;
//     bool mapReady = false;

//     float[] filteredDeg;  
//     // public float smoothing = 100f;

//     void Start()
//     {
//         ROSConnection.instance.Subscribe<JointStateMsg>("/joint_states", Callback);

//         // Prevent oscillation – velocity-like controller
//         foreach (var j in joints)
//         {
//             var d = j.xDrive;
//             d.stiffness = 0.001f;       // No spring pulling
//             d.damping = 200f;     // High damping = no overshoot
//             d.forceLimit = 200000f;
//             j.xDrive = d;
//         }
//     }

//     // float ExpSmooth(float current, float target, float factor)
//     // {
//     //     return Mathf.Lerp(current, target, 1 - Mathf.Exp(-factor * Time.deltaTime));
//     // }

//     void Callback(JointStateMsg msg)
//     {
//         if (!mapReady)
//         {
//             BuildMap(msg);
//             filteredDeg = new float[joints.Length];
//             return;
//         }

//         for (int i = 0; i < msg.position.Length; i++)
//         {
//             float targetDeg = (float)msg.position[i] * Mathf.Rad2Deg;

//             // smooth internal buffer, NOT the drive.target
//             // filteredDeg[i] = ExpSmooth(filteredDeg[i], targetDeg, smoothing);

//             int jIndex = map[i];
//             var drive = joints[jIndex].xDrive;

//             drive.target = targetDeg; // filteredDeg[i];     // Set final target
//             joints[jIndex].xDrive = drive;
//         }
//     }

//     void BuildMap(JointStateMsg msg)
//     {
//         map = new int[msg.name.Length];

//         for (int i = 0; i < msg.name.Length; i++)
//         {
//             for (int j = 0; j < joints.Length; j++)
//             {
//                 var urdfName = joints[j].GetComponent<UrdfJoint>().jointName;
//                 if (msg.name[i] == urdfName)
//                 {
//                     map[i] = j;
//                     break;
//                 }
//             }
//         }

//         mapReady = true;
//     }
// }



// public class ROSTopicBasedControlPlugin : MonoBehaviour {

//     ROSConnection ros;

//     public ArticulationBody[] Joints;
    
//     public string jointStatesTopic = "/joint_states";
//     public string jointCommandsTopic = "/joint_command";

//     public float frequency = 20f;

//     private float TimeElapsed;
//     private JointStateMsg stateMsg;
//     private JointStateMsg commandMsg;

//     private bool recvdCommandJointOrder = false;
//     private int[] commandJointOrder;

//     void Start()
//     {
//         jointCommandsTopic = jointCommandsTopic; //changed gameObject.transform.root.name+jointCommandsTopic
//         ros = ROSConnection.GetOrCreateInstance();
//         ros.RegisterPublisher<JointStateMsg>(gameObject.transform.root.name+jointStatesTopic);
//         ros.Subscribe<JointStateMsg>(jointCommandsTopic, CommandCallback);

//         stateMsg = new JointStateMsg();
//         commandMsg = new JointStateMsg();

//         stateMsg.name = new string[Joints.Length];

//         for (uint i = 0; i < Joints.Length; i++)
//         {
//             stateMsg.name[i] = Joints[i].GetComponent<UrdfJoint>().jointName;
//         }

//         stateMsg.position = new double[Joints.Length];
//         stateMsg.velocity = new double[Joints.Length];
//         stateMsg.effort = new double[Joints.Length];

//         stateMsg.header = new HeaderMsg();
// #if ROS2
// #else
//         stateMsg.header.seq = 0;
// #endif

//     }

//     private void Update()
//     {
//         TimeElapsed += Time.deltaTime;

//         if (TimeElapsed > 1/frequency)
//         {
//             var now = DateTimeOffset.UtcNow;
//             long unixNano = now.ToUnixTimeMilliseconds() * 1_000_000
//                             + now.Ticks % TimeSpan.TicksPerMillisecond * 100;

//             stateMsg.header.stamp.sec = (int)(unixNano / 1_000_000_000);
//             stateMsg.header.stamp.nanosec = (uint)(unixNano % 1_000_000_000);

//             for (uint i = 0; i < Joints.Length; i++)
//             {
//                 // 0 as the index as all of them are single degree of freedom joints.
//                 stateMsg.position[i] = (double)Joints[i].jointPosition[0];
//                 stateMsg.velocity[i] = (double)Joints[i].jointVelocity[0];
//                 stateMsg.effort[i] = (double)Joints[i].jointForce[0];
//             }

//             ros.Publish(gameObject.transform.root.name+jointStatesTopic, stateMsg);
//             TimeElapsed = 0;
//         }
//     }

//     void CommandCallback(JointStateMsg msg)
//     {
//         if (msg.position.Length != Joints.Length)
//         {
//             Debug.LogWarning("received message contains inavlid number of joints");
//         }
//         else
//         {
//             if (!recvdCommandJointOrder)
//             {
//                 commandJointOrder = new int[msg.name.Length];
//                 for (int i = 0; i < msg.name.Length; i++)
//                 {
//                     for (int j = 0; j < Joints.Length; j++)
//                     {
//                         if (msg.name[i] == Joints[j].GetComponent<UrdfJoint>().jointName)
//                         {
//                             commandJointOrder[i] = j;
//                             break;
//                         }
//                     }
//                 }
//                 recvdCommandJointOrder = true;
//             }
//             else
//             {
//                 for (int i = 0; i < msg.position.Length; i++)
//                 {
//                     Joints[commandJointOrder[i]].SetDriveTarget(ArticulationDriveAxis.X, (float)msg.position[i] * Mathf.Rad2Deg);
//                 }
//             }
//         }
//     }

// }