using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkLogicSample_Client : MonoBehaviour
{
    [SerializeField]
    NetworkManager_Client client;
    [SerializeField]
    string PrefabName;
    [SerializeField] GameObject A;

    [SerializeField]
    ReplicatiorBase AutonomousObj;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
            client.RequestCreatingNewAutonomousObject(AutonomousObj, PrefabName, AutonomousObj.transform.position, AutonomousObj.transform.eulerAngles, AutonomousObj.transform.parent!=null?AutonomousObj.transform.parent.name:"");
    }

    void test22()
    {

        client.RequestCreatingNewAutonomousObject(AutonomousObj, A.name , AutonomousObj.transform.position, AutonomousObj.transform.eulerAngles, AutonomousObj.transform.parent != null ? AutonomousObj.transform.parent.name : "");
    
    }


}
