using System;
using UnityEngine;

public class MusicManager : MonoBehaviour
{


    private readonly AudioSource[] sources = new AudioSource[Enum.GetValues(typeof(MusicCues)).Length];

    private static MusicManager _instance;

    public static MusicManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject managerObject = new("MusicManager");
                _instance = managerObject.AddComponent<MusicManager>();
            }
            return _instance;
        }
    }

    private MusicManager() { }

}