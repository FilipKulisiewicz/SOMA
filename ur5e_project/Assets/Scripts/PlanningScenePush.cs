// using UnityEngine;
// using RosMessageTypes.Moveit;
// using Unity.Robotics.ROSTCPConnector;
// using RosMessageTypes.Moveit;
// using RosMessageTypes.Std;

// public class PlanningScenePush : MonoBehaviour
// {
//     private ROSConnection ros;
//     void Start()
//     {
//         ros = ROSConnection.GetOrCreateInstance();
//         ros.RegisterRosService<
//             ApplyPlanningSceneRequest,
//             ApplyPlanningSceneResponse
//         >("apply_planning_scene");
//     }

//     public void PushObject(CollisionObjectMsg obj)
//     {
//         PlanningSceneMsg scene = new PlanningSceneMsg();
//         scene.is_diff = true;
//         scene.world.collision_objects = new CollisionObjectMsg[] { obj };
//         scene.robot_state = new RobotStateMsg();
//         scene.robot_state.is_diff = true;
//         // scene.robot_state.joint_state = new JointStateMsg();

//         // scene.robot_state.joint_state.name = new string[]
//         // {
//         //     "shoulder_pan_joint",
//         //     "shoulder_lift_joint",
//         //     "elbow_joint",
//         //     "wrist_1_joint",
//         //     "wrist_2_joint",
//         //     "wrist_3_joint"
//         // };

//         // If you don't know actual values, set zeros (UR allows this)
//         // scene.robot_state.joint_state.position = new double[]
//         // {
//         //     0, 0, 0, 0, 0, 0
//         // };
//         ApplyPlanningSceneRequest req = new ApplyPlanningSceneRequest(scene);
//         ros.SendServiceMessage<ApplyPlanningSceneResponse>(
//             "apply_planning_scene",
//             req,
//             OnSceneApplied
//         );

//     }

//     private void OnSceneApplied(ApplyPlanningSceneResponse resp)
//     {
//         Debug.Log("Scene applied: " + resp.success);
//     }
// }
