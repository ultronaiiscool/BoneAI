using UnityEngine;

namespace BoneAI.Game;

public sealed class ObjectRegistry
{
    private readonly Dictionary<string, GameObject> _objects = new();
    public string Register(GameObject gameObject)
    {
        var id = "obj_" + gameObject.GetInstanceID().ToString("x");
        _objects[id] = gameObject;
        return id;
    }
    public bool TryGet(string id, out GameObject gameObject)
    {
        if (_objects.TryGetValue(id, out gameObject!) && gameObject != null) return true;
        _objects.Remove(id); gameObject = null!; return false;
    }
    public void Prune()
    {
        List<string>? expired = null;
        foreach (var item in _objects)
            if (item.Value == null) (expired ??= new List<string>()).Add(item.Key);
        if (expired == null) return;
        foreach (var id in expired) _objects.Remove(id);
    }
}
