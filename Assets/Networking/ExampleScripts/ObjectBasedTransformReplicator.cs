using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectBasedTransformReplicator : ReplicatiorBase
{
    public GameObject CoordinateBaseObject;

    Vector3 Prepos;

    private void Start()
    {
        CoordinateBaseObject = GameObject.Find("COBase");


        if (CoordinateBaseObject == null)
        {
            CoordinateBaseObject = GameObject.FindGameObjectWithTag("COBaseTag");

        }


    }

    public override byte[] GetReplicationData()
    {
        if (CoordinateBaseObject == null)
            return null;
        Vector3 vec = (transform.position - CoordinateBaseObject.transform.position);
        Vector3 Degradedvec = new Vector3(Vector3.Dot(vec, CoordinateBaseObject.transform.right), Vector3.Dot(vec, CoordinateBaseObject.transform.up), Vector3.Dot(vec, CoordinateBaseObject.transform.forward));
        float yrot = Quaternion.LookRotation(transform.position, CoordinateBaseObject.transform.position).eulerAngles.y;
        Prepos = transform.position;
        return NetworkManagerBase.encoding.GetBytes(Serializer.Vector3ToString(Degradedvec, 3) + "," + yrot);
    }

    public override byte[] GetAutonomousData()
    {
        return GetReplicationData();
    }

    public override void ReceiveReplicationData(byte[] repdata)
    {
        if (CoordinateBaseObject == null)
            return;
        string[] s = NetworkManagerBase.encoding.GetString(repdata).Split(',');
        Vector3 vec = Serializer.StringToVector3(s[0], s[1], s[2]);
        transform.position = CoordinateBaseObject.transform.position + CoordinateBaseObject.transform.right * vec.x + CoordinateBaseObject.transform.up * vec.y + CoordinateBaseObject.transform.forward * vec.z;
        transform.eulerAngles = new Vector3(0, float.Parse(s[3]), 0);
    }

    public override void ReceiveAutonomousData(byte[] autodata)
    {
        ReceiveReplicationData(autodata);
    }

    public override bool DoesServerNeedReplication()
    {
        return Prepos != transform.position&&IsAutonomousObject();
    }

    public override bool DoesClientNeedReplication(ClientDataContainer client)
    {
        return Prepos != transform.position&&!IsAutonomousObject();
    }
}
