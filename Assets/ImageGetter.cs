using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using UnityEngine;
using System.IO;


public class ImageGetter : MonoBehaviour
{
    HttpClient client;
    public List<ImageDataContainer> containers;
    // Start is called before the first frame update
    void Start()
    {
        client = new HttpClient();
        GetImageAsync();
    }

    public async void GetImageAsync()
    {
        string Body = await GetAsync(@"https://19cahylda1.execute-api.ap-northeast-1.amazonaws.com/staging/movies");
        Body = Body.Substring(Body.IndexOf("body") + 8);
        Body = Body.Substring(0, Body.Length - 3);
        string[] vs = Body.Split(',');
        containers = new List<ImageDataContainer>();
        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ImageDataContainer));
        for (int i = 0; i < vs.Length; i += 2)
        {
            string str = vs[i] + "," + vs[i + 1];
            str = str.Replace("\\", string.Empty);
            Debug.Log(str);
            MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(str));
            containers.Add(serializer.ReadObject(stream) as ImageDataContainer);
            Debug.Log(containers.Count + ": " + containers[containers.Count - 1].id);
        }
        foreach (ImageDataContainer container in containers)
        {
            Texture2D texture = new Texture2D(1, 1);
            Debug.Log(texture.LoadImage(await GetImageAsync(container.url)));
            texture.Apply();
            container.texture = texture;
        }
    }

    public async Task<string> GetAsync(string URL)
    {
        HttpResponseMessage message = await client.GetAsync(URL); // GET
        string Body = await message.Content.ReadAsStringAsync();
        Debug.Log(Body);
        return Body;
    }
    public async Task<byte[]> GetImageAsync(string URL)
    {
        HttpResponseMessage message = await client.GetAsync(URL); // GET
        byte[] Body = await message.Content.ReadAsByteArrayAsync();
        return Body;
    }

    private void OnDestroy()
    {
        client.Dispose();
    }
}

public class ImageDataContainer
{
    public int id;
    public string url;
    public Texture2D texture;
}
