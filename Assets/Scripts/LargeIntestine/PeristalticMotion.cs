using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PeristalticMotion : MonoBehaviour
{
    public Mesh mesh;
    public SkinnedMeshRenderer smr;
    //private MeshCollider meshCollider;
    private Vector3[] originalVertices;
    public float speed;
    public float maxSpeed = 0.1f;
    private float scale;
    public float maxScale = 0.1f;
    private Motion motionState;
    private float stateDuration;
    private float duration;
    private float totalDuration;
    private float interpolationValue;
    public float decrement = 0.03f;
    public bool inMotion = false;
    public float pos = 0f;
    private Dictionary<int, float> originalWeights;
    public List<float> currentWeiths;


    public float currentMotionDuration = 0f;
    public float motionDuration = 5f;

    void Start()
    {
        mesh = GetComponent<SkinnedMeshRenderer>().sharedMesh;
        smr = GetComponent<SkinnedMeshRenderer>();
        //meshCollider = GetComponentInChildren<MeshCollider>();
        originalVertices = mesh.vertices;
        motionState = Motion.None;
        stateDuration = Random.Range(1f, 3f);
        currentMotionDuration = 0f;
        originalWeights = new Dictionary<int, float>();
        for(int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
        {
            originalWeights[i] = smr.GetBlendShapeWeight(i);
            currentWeiths.Add(0f);
        }
        currentWeiths = new List<float>();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateBlends();
        if (inMotion)
        {
            SetMotionState();
            UpdateMesh();
            duration += Time.deltaTime;
            totalDuration += Time.deltaTime;
        }
    }

    private void UpdateBlends()
    {
        var sectionCount = smr.sharedMesh.blendShapeCount / 19;
        var step = motionDuration / sectionCount;
        currentMotionDuration += Time.deltaTime * 0.1f;
        var point = (int) Mathf.Lerp(0, 44, currentMotionDuration);
        for(int i = 0; i < 45; i++)
        {
            if(i == point)
            {
                var offset = point * 19;
                smr.SetBlendShapeWeight(offset + 11, Mathf.Lerp(originalWeights[offset + 11], originalWeights[offset + 11] - 10, 0.5f));
                smr.SetBlendShapeWeight(offset + 12, Mathf.Lerp(originalWeights[offset + 12], originalWeights[offset + 12] - 10, 0.5f));
                smr.SetBlendShapeWeight(offset + 13, Mathf.Lerp(originalWeights[offset + 13], originalWeights[offset + 13] - 10, 0.5f));
                smr.SetBlendShapeWeight(offset + 14, Mathf.Lerp(originalWeights[offset + 14], originalWeights[offset + 14] - 10, 0.5f));
                smr.SetBlendShapeWeight(offset + 15, Mathf.Lerp(originalWeights[offset + 15], originalWeights[offset + 15] - 10, 0.5f));
                smr.SetBlendShapeWeight(offset + 16, Mathf.Lerp(originalWeights[offset + 16], originalWeights[offset + 16] - 10, 0.5f));
            }

            //if(i == point)
            //{
                //var offset = point * 19;
                /*smr.SetBlendShapeWeight(offset + 11, Mathf.Lerp((originalWeights[offset + 11] - 1), originalWeights[offset + 11], currentWeiths[offset + 11] = currentWeiths[offset + 11] + 0.1f));
                smr.SetBlendShapeWeight(offset + 12, Mathf.Lerp((originalWeights[offset + 12] - 1), originalWeights[offset + 12], currentWeiths[offset + 12] = currentWeiths[offset + 12] + 0.1f));
                smr.SetBlendShapeWeight(offset + 13, Mathf.Lerp((originalWeights[offset + 13] - 1), originalWeights[offset + 13], currentWeiths[offset + 13] = currentWeiths[offset + 13] + 0.1f));
                smr.SetBlendShapeWeight(offset + 14, Mathf.Lerp((originalWeights[offset + 14] - 1), originalWeights[offset + 14], currentWeiths[offset + 14] = currentWeiths[offset + 14] + 0.1f));
                smr.SetBlendShapeWeight(offset + 15, Mathf.Lerp((originalWeights[offset + 15] - 1), originalWeights[offset + 15], currentWeiths[offset + 15] = currentWeiths[offset + 15] + 0.1f));
                smr.SetBlendShapeWeight(offset + 16, Mathf.Lerp((originalWeights[offset + 16] - 1), originalWeights[offset + 16], currentWeiths[offset + 16] = currentWeiths[offset + 16] + 0.1f));*/
            //} else if (i == point - 1)
            /*{
                var offset = point * 19;
                smr.SetBlendShapeWeight(offset + 11, Mathf.Lerp(originalWeights[offset + 11], (originalWeights[offset + 11] - 1), (0.1f * speed)));
                smr.SetBlendShapeWeight(offset + 12, Mathf.Lerp(originalWeights[offset + 12], (originalWeights[offset + 12] - 1), (0.1f * speed)));
                smr.SetBlendShapeWeight(offset + 13, Mathf.Lerp(originalWeights[offset + 13], (originalWeights[offset + 13] - 1), (0.1f * speed)));
                smr.SetBlendShapeWeight(offset + 14, Mathf.Lerp(originalWeights[offset + 14], (originalWeights[offset + 14] - 1), (0.1f * speed)));
                smr.SetBlendShapeWeight(offset + 15, Mathf.Lerp(originalWeights[offset + 15], (originalWeights[offset + 15] - 1), (speed * scale)));
                smr.SetBlendShapeWeight(offset + 16, Mathf.Lerp(originalWeights[offset + 16], (originalWeights[offset + 16] - 1), (speed * scale)));
            } else if (i  == point - 2)
            {
                var offset = point * 19;
                smr.SetBlendShapeWeight(offset + 11, Mathf.Lerp(originalWeights[offset + 11], (originalWeights[offset + 11] - 1), (speed * scale)));
                smr.SetBlendShapeWeight(offset + 12, Mathf.Lerp(originalWeights[offset + 12], (originalWeights[offset + 12] - 1), (speed * scale)));
                smr.SetBlendShapeWeight(offset + 13, Mathf.Lerp(originalWeights[offset + 13], (originalWeights[offset + 13] - 1), (speed * scale)));
                smr.SetBlendShapeWeight(offset + 14, Mathf.Lerp(originalWeights[offset + 14], (originalWeights[offset + 14] - 1), (speed * scale)));
                smr.SetBlendShapeWeight(offset + 15, Mathf.Lerp(originalWeights[offset + 15], (originalWeights[offset + 15] - 1), (speed * scale)));
                smr.SetBlendShapeWeight(offset + 16, Mathf.Lerp(originalWeights[offset + 16], (originalWeights[offset + 16] - 1), (speed * scale)));
            }*/
        }
    }

    private void UpdateMesh()
    {
        var axis = Random.Range(1f, 3f);
        Vector3[] vertices = new Vector3[originalVertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = originalVertices[i];
            /*vertex.z += Mathf.Sin(totalDuration * speed + originalVertices[i].x + originalVertices[i].y + originalVertices[i].z) * scale;
            vertex.y += Mathf.Sin(totalDuration * speed + originalVertices[i].x + originalVertices[i].y + originalVertices[i].z) * scale;
            vertex.x += Mathf.Sin(totalDuration * speed + originalVertices[i].x + originalVertices[i].y + originalVertices[i].z) * scale;*/
            /*vertex.z += Mathf.PerlinNoise(originalVertices[i].x + speed, originalVertices[i].y + Mathf.Sin(Time.time * 0.1f)) * scale;
            vertex.y += Mathf.PerlinNoise(originalVertices[i].x + speed, originalVertices[i].y + Mathf.Sin(Time.time * 0.1f)) * scale;
            vertex.x += Mathf.PerlinNoise(originalVertices[i].x + speed, originalVertices[i].y + Mathf.Sin(Time.time * 0.1f)) * scale;*/
            //vertex.y += Mathf.Sin(totalDuration * speed + originalVertices[i].x + originalVertices[i].y + originalVertices[i].z) * scale;
            if(axis <= 1f)
            {
                vertex.x += Mathf.Sin(totalDuration * speed + originalVertices[i].x + originalVertices[i].y + originalVertices[i].z) * scale;
            } else if(axis <= 2f)
            {
                vertex.y += Mathf.Sin(totalDuration * speed + originalVertices[i].x + originalVertices[i].y + originalVertices[i].z) * scale;
            } else if (axis <= 3)
            {
                vertex.z += Mathf.Sin(totalDuration * speed + originalVertices[i].x + originalVertices[i].y + originalVertices[i].z) * scale;
            }
            vertices[i] = vertex;
        }
        mesh.vertices = vertices;
        //mesh.RecalculateBounds();
        //meshCollider.sharedMesh = null;
        //meshCollider.sharedMesh = mesh;
    }

    private void SetMotionState()
    {
        switch (motionState)
        {
            case Motion.None:
                if(!CheckStateDuration(4f,10f)) motionState = Motion.Positive;
                break;
            case Motion.Positive:
                if (!CheckStateDuration(1f, 3f)) motionState = Motion.Idle;
                interpolationValue += Time.deltaTime / stateDuration;
                speed = Mathf.Lerp(0.0f, maxSpeed, interpolationValue);
                scale = Mathf.Lerp(0.0f, maxScale, interpolationValue);
                break;
            case Motion.Idle:
                if (!CheckStateDuration(4f, 10f)) motionState = Motion.Negative;
                break;
            default:
                /*if (!CheckStateDuration(1f, 3f)) motionState = Motion.None;
                interpolationValue -= Time.deltaTime / stateDuration;
                speed = Mathf.Lerp(0f, 0.5f, interpolationValue);
                scale = Mathf.Lerp(0.05f, 0.1f, interpolationValue);*/
                if(scale > 0)
                {
                    speed -= Time.deltaTime * decrement;
                    scale -= Time.deltaTime * decrement;
                    if (!CommonUtils.IsInRange(scale, 0f, maxScale)) scale = 0;
                } else
                {
                    scale = 0;
                    speed = 0;
                    totalDuration = 0;
                    motionState = Motion.None;
                    stateDuration = Random.Range(1f, 3f);
                    interpolationValue = 0f;
                }
                break;
        }

    }

    private bool CheckStateDuration(float minInclusive, float maxInclusive)
    {
        if(duration >= stateDuration)
        {
            duration = 0f;
            stateDuration = Random.Range(minInclusive, maxInclusive);
            return false;
        }
        return true;
    }

    public enum Motion
    { 
        None,
        Idle,
        Positive,
        Negative
    }
}
