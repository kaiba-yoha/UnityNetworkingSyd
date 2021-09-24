using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

public class SimpleHTTPGetter : MonoBehaviour
{
    HttpClient client;
    // Start is called before the first frame update
    void Start()
    {
        client = new HttpClient();
        GetAsync(@"https://19cahylda1.execute-api.ap-northeast-1.amazonaws.com/staging/movies");
    }

    public async Task<string> GetAsync(string URL)
    {
        HttpResponseMessage message = await client.GetAsync(URL); // GET
        string Body = await message.Content.ReadAsStringAsync();
        Debug.Log(Body);
        return Body;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
