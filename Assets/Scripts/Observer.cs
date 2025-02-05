using System.Collections;
using UnityEngine;

public class Observer : MonoBehaviour
{
    GlobalEvents _gE;
    EnemyCounter _eC;
    private bool? kill = false; // Nullable boolean variable

    void Start()
    {
        _gE = GameObject.Find("GlobalEvents").GetComponent<GlobalEvents>();
        _eC = GameObject.Find("EnemyCounter").GetComponent<EnemyCounter>();
    }

    void Update()
    {
        if (_gE.gameStart)
        {
            _gE.gameStart = false;
            StartCoroutine(WaitRandomAndPerformEvent1(5f, 5f));
        }
        if (kill == null)
        {
            _gE.PerformEvent2(kill);
        }
        if (_eC.waveDestroyed)
        {
            StartCoroutine(WaitRandomAndPerformEvent1(5f, 8f));
        }
        if (_eC.wave4Destroyed)
        {
            _gE.PerformEvent4(kill);
        }
    }

    IEnumerator WaitRandomAndPerformEvent1(float min, float max)
    {
        yield return StartCoroutine(WaitRandom(min, max));
        _gE.PerformEvent1();
    }

    public IEnumerator WaitRandom(float min, float max)
    {
        float delay = Random.Range(min, max);
        yield return new WaitForSeconds(delay);
    }
}
