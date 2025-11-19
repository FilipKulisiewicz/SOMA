using UnityEngine;

public static class RosUnityConverter
{
    // Quaternion representing rotation from Unity to ROS (ENU → FLU)
    // Unity uses:{x,y,z,w} !!!!
    private static readonly Quaternion qUR = new Quaternion(1.0f, 0.0f, 0.0f, 0.0f); //new Quaternion(0.5f, 0.5f, -0.5f, 0.5f);

    /// <summary>
    /// Convert Unity position to ROS position (XYZ → Z, -X, Y)
    /// </summary>
    public static Vector3 UnityToRosPosition(Vector3 u)
    {
        return new Vector3(
            u.z,
            -u.x,
            u.y
        );
    }

    /// <summary>
    /// Convert Unity position to ROS position (XYZ → Z, X, Y)
    /// </summary>
    public static Vector3 UnityToRosScale(Vector3 scale)
    {
        return new Vector3(
            scale.z,
            scale.x,
            scale.y
        );
    }

    /// <summary>
    /// Convert Unity quaternion to ROS quaternion using q_UR * q_u
    /// </summary>
    public static Quaternion UnityToRosRotation(Quaternion qUnity)
    {
        Quaternion qUnity_corrected = new Quaternion(
            -qUnity.z,     // x'
            -qUnity.x,    // y'
            qUnity.y,     // z'
            qUnity.w      // w'
        );
        return qUR * qUnity_corrected;
    }

    /// <summary>
    /// Convert a Unity transform into ROS pose message fields
    /// </summary>
    public static void FillRosPose(
        Transform t,
        out Vector3 rosPos,
        out Quaternion rosRot
    ) {
        rosPos = UnityToRosPosition(t.position);
        rosRot = UnityToRosRotation(t.rotation);
    }
}
