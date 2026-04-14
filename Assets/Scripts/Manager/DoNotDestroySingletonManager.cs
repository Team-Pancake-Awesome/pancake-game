using UnityEngine;

public abstract class DoNotDestroySingletonManager<T> : SingletonManager<T> where T : MonoBehaviour
{
    protected virtual bool PersistAcrossScenes => true;
    
    protected override void Awake()
	{
		base.Awake();

		if (Instance != this)
		{
			return;
		}

		if (PersistAcrossScenes)
		{
			DontDestroyOnLoad(gameObject);
		}
	}
}