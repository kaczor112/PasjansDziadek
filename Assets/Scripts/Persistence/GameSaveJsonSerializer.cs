using UnityEngine;

namespace Pasjans.Persistence
{
    /// <summary>Oddziela zapis plikowy od JsonUtility, dzięki czemu można go łatwo testować.</summary>
    public interface IGameSaveJsonSerializer
    {
        string Serialize<T>(T value);
        T Deserialize<T>(string json);
    }

    public sealed class UnityGameSaveJsonSerializer : IGameSaveJsonSerializer
    {
        public string Serialize<T>(T value)
        {
            return JsonUtility.ToJson(value);
        }

        public T Deserialize<T>(string json)
        {
            return JsonUtility.FromJson<T>(json);
        }
    }
}
