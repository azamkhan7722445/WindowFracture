using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenTextGenerator : MonoBehaviour
{
    [SerializeField] private Text txt;
    private readonly List<string> _txtList = new()
    {
        "Loading Rage...",
        "Preparing Destruction...",
        "Shattering Expectations...",
        "Calculating Chaos...",
        "Warming Up the Hammer...",
        "Summoning Fractures...",
        "Priming the Breakage..."
    };

    private void OnEnable()
    {
        if (_txtList == null || _txtList.Count == 0) return;
        txt.text = _txtList[Random.Range(0, _txtList.Count)];
    }
}