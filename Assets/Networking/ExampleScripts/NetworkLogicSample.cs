using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkLogicSample : MonoBehaviour
{
    public string prefabName;
    public NetworkManager_Server server;
    // Start is called before the first frame update
    void Start()
    {

    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            server.CreateNetworkPrefab(prefabName, transform.position, transform.eulerAngles, gameObject.name);
        }
    }
}
