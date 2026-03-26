using UnityEngine;

public class FlamePot : MonoBehaviour
{
    public ArduinoReader arduinoReader;
    public ParticleSystem flame;

    [Header("emission")]
    public float minEmission = 0f;
    public float maxEmission = 120f;

    [Header("particle size")]
    public float minSize = 0.08f;
    public float maxSize = 0.35f;

    [Header("particle speed")]
    public float minSpeed = 0.15f;
    public float maxSpeed = 1.8f;

    [Header("shape")]
    public float minRadius = 0.01f;
    public float maxRadius = 0.05f;

    [Header("off")]
    public float offThreshold = 0.03f;

    void Start()
    {
        if (flame == null)
            flame = GetComponent<ParticleSystem>();

        if (flame != null && !flame.isPlaying)
            flame.Play();
    }

    void Update()
    {
        if (arduinoReader == null || flame == null)
            return;

        float t = Mathf.Clamp01(arduinoReader.sensorValue);

        var main = flame.main;
        var emission = flame.emission;
        var shape = flame.shape;

        emission.enabled = true;
        shape.enabled = true;
        main.loop = true;

        if (t <= offThreshold)
        {
            emission.rateOverTime = 0f;
            return;
        }

        emission.rateOverTime = Mathf.Lerp(minEmission, maxEmission, t);
        main.startSize = Mathf.Lerp(minSize, maxSize, t);
        main.startSpeed = Mathf.Lerp(minSpeed, maxSpeed, t);
        shape.radius = Mathf.Lerp(minRadius, maxRadius, t);
    }
}