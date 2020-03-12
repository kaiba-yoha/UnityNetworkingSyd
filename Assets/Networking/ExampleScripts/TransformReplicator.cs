using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformReplicator : ReplicatiorBase
{
    public override byte[] GetReplicationData()
    {
        string s = Serializer.Vector3ToString(transform.position) + "," + Serializer.Vector3ToString(transform.eulerAngles);
        return NetworkManager_Server.encoding.GetBytes(s);
    }

    public override byte[] GetAutonomousData()
    {
        return GetReplicationData();
    }

    public override void ReceiveReplicationData(byte[] repdata)
    {
        string[] s = NetworkManager_Server.encoding.GetString(repdata).Split(',');
        transform.position = Serializer.StringToVector3(s[0], s[1], s[2]);
        transform.eulerAngles = Serializer.StringToVector3(s[3], s[4], s[5]);
    }

    public override void ReceiveAutonomousData(byte[] autodata)
    {
        ReceiveReplicationData(autodata);
    }
}
