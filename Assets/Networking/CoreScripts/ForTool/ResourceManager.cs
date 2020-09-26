using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.AccessControl;

/// <summary>
/// ResourceManager manage all resourcedata of application contain built-in resource and downloaded resource.
/// Also, manage Registration and Update of ResourceData.
/// </summary>
public static class ResourceManager
{
    static List<ResourceData> resources = new List<ResourceData>();
    static Dictionary<string, ResourceData> ResourceDic = new Dictionary<string, ResourceData>();
    static Dictionary<string, ResourceData> BuiltInResourceDic = new Dictionary<string, ResourceData>()
    {
        { "BI_SAMPLE",new ResourceData("StreamingAssets/Sample.txt", "BI_SAMPLE",UriKind.Relative)},
    };
    static int IDBuffer = 0;
    private static string iDHeader = "User";

    public static string DownloadedResourceFolder = "Resources" + Path.DirectorySeparatorChar + "Downloaded" + Path.DirectorySeparatorChar;
    public static string IDHeader { get => iDHeader; set => iDHeader = value + Guid.NewGuid().ToString().Substring(0, 6); }

    public delegate void ResourceNotify(ResourceData resource);

    public static event ResourceNotify OnNewResourceRegisted;
    public static event ResourceNotify OnResourceUpdated;
    public static event ResourceNotify OnResourceNeedUpdated;

    /// <summary>
    /// Create New ResourceData if path Not Exist In Manager's List.
    /// </summary>
    /// <param name="filepath"></param>
    /// <param name="ResId">If ResId is null, setted by GetNewResourceId()</param>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static ResourceData CreateNewResourceIfNotExist(string filepath, string ResId = null, UriKind kind = UriKind.Absolute)
    {
        ResourceData resource;
        if ((resource = resources.Find((res) => filepath == res.path)) != null)
            return resource;

        if (ResId == null)
            ResId = GetNewResourceId();
        resource = new ResourceData(filepath, ResId, kind);
        AddResource(resource);
        return resource;
    }
    /// <summary>
    /// Add ResourceData Manually, Dont Recommend this for LocalFile Resource. Use CreateNewResourceIfNotExist instead.
    /// </summary>
    /// <param name="resource"></param>
    public static void AddResource(ResourceData resource)
    {
        if (resource == null)
            return;

        if (resources.Contains(resource) || resources.Exists((res) => res.path == resource.path))
            return;

        if (resource.Id == null)
            resource.Id = GetNewResourceId();

        resources.Add(resource);
        ResourceDic.Add(resource.Id, resource);
        resource.NeedUpdateNotify += (res) => OnResourceNeedUpdated?.Invoke(res);
        resource.OnUpdatedNotify += (res) => OnResourceUpdated?.Invoke(res);
        OnNewResourceRegisted?.Invoke(resource);
    }

    public static List<ResourceData> GetAllResources()
    {
        return resources;
    }

    public static ResourceData GetResource(string Id)
    {
        ResourceData data;
        if (ResourceDic.TryGetValue(Id, out data))
            return data;
        else
            return GetBuiltInResource(Id);
    }

    public static ResourceData GetBuiltInResource(string Id)
    {
        BuiltInResourceDic.TryGetValue(Id, out ResourceData data);
        return data;
    }

    public static string GetDLResourceFolderPath(bool Absolute = false)
    {
        string ABS = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + DownloadedResourceFolder;
        if (!Directory.Exists(ABS))
            Directory.CreateDirectory(ABS);
        return Absolute ? ABS : DownloadedResourceFolder;
    }
    public static string GetNewResourceId()
    {
        return iDHeader + "-" + IDBuffer++;
    }
}

/// <summary>
/// ResourceData contain URI of File, Type of File, Extension, Path, and UpdateNotifySystem.
/// This class used for manage and aggregation of file dependencies.
/// ResourceData is created for each managed file.
/// </summary>
public class ResourceData
{
    // /Resources/....
    public string path = null;
    public Uri URI;
    public string name, extension, Id;
    public ResourceType type;
    public event ResourceManager.ResourceNotify NeedUpdateNotify;
    public event ResourceManager.ResourceNotify OnUpdatedNotify;

    public ResourceData(string filepath, string ResId, UriKind kind = UriKind.Absolute)
    {
        ChangeResourcePath(filepath, kind);
        Id = ResId;
    }

    public bool Exist()
    {
        return File.Exists(path);
    }

    /// <summary>
    /// Change ResourcePath from path. After Changing, Issue NeedUpdateNotify.
    /// </summary>
    /// <param name="newpath"></param>
    public void ChangeResourcePath(string newpath, UriKind uriKind)
    {
        if (newpath == path)
            return;

        bool ExistOld = path != null;
        path = newpath;
        URI = new Uri(newpath, uriKind);
        name = Path.GetFileNameWithoutExtension(path);
        extension = Path.GetExtension(path);
        NeedUpdateNotify?.Invoke(this);
    }

    void SelectType()
    {
        switch (extension)
        {
            case ".jpeg":
            case ".jpg":
            case ".png":
                type = ResourceType.Image;
                break;
            case ".mp3":
            case ".wav":
            case ".m4a":
            case ".aac":
                type = ResourceType.Sound;
                break;
        }
    }

    public void IssueUpdateNotifyRequest()
    {
        NeedUpdateNotify?.Invoke(this);
    }

    public void IssueFinishedUpdateNotify()
    {
        OnUpdatedNotify?.Invoke(this);
    }
}

public interface IUseResourceObjcet
{
    List<ResourceData> GetAllDependResources();
    ResourceData GetResourceById(string Id);
    bool ExistAllResources();
    List<ResourceData> GetUnExistResources();

    void ReassignAllResource(ResourceData[] newres);

    event Action<IUseResourceObjcet> OnResourceReassigned;
}

public enum ResourceType
{
    CharactorFile, Document, Image, Sound, Media
}
