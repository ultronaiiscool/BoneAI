using UnityEngine;

namespace BonelabAIAgent.Game;

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
        foreach (var id in _objects.Where(x => x.Value == null).Select(x => x.Key).ToArray()) _objects.Remove(id);
    }
}
