using UnityEngine;

[ExecuteAlways]
public class MorphController : MonoBehaviour
{
    [Range(0f, 1f)]
    public float morph = 0f;

    public SkinnedMeshRenderer smr;
    public Renderer rend;

    MaterialPropertyBlock mpb;
    bool initialized = false;

    void EnsureMPB()
    {
        if (mpb == null)
            mpb = new MaterialPropertyBlock();
    }

    void Awake()
    {
        AssignRandomMorph();
    }

    void OnEnable()
    {
        if (!Application.isPlaying) // also randomize in editor if you duplicate
            AssignRandomMorph();
    }

    void AssignRandomMorph()
    {
        morph = Random.value; // random 0..1
        initialized = true;
    }

    void Update()
    {
        if (!initialized) AssignRandomMorph();

        if (smr != null)
            smr.SetBlendShapeWeight(0, morph * 100f);

        if (rend != null)
        {
            EnsureMPB();
            rend.GetPropertyBlock(mpb);
            mpb.SetFloat("_Blend", morph);
            rend.SetPropertyBlock(mpb);
        }
    }
}
