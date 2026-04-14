using UnityEngine;

public abstract class AudioManager<TManager> : DoNotDestroySingletonManager<TManager> where TManager : MonoBehaviour
{
	protected TClipList CreateRuntimeCueList<TClipList>(TClipList source) where TClipList : ScriptableObject
	{
		if (!Application.isPlaying || source == null)
		{
			return source;
		}

		TClipList runtimeCopy = Instantiate(source);
		runtimeCopy.name = $"{source.name} (Runtime)";
		return runtimeCopy;
	}

	protected AudioSource GetOrCreatePooledSource(AudioSource[] sourcePool, int index, string sourceName)
	{
		if (sourcePool == null || index < 0 || index >= sourcePool.Length)
		{
			return null;
		}

		if (sourcePool[index] == null)
		{
			GameObject sourceObject = new(sourceName);
			sourceObject.transform.SetParent(transform, false);
			sourcePool[index] = sourceObject.AddComponent<AudioSource>();
		}

		return sourcePool[index];
	}
}