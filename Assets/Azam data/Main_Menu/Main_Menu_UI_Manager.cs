using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Main_Menu_UI_Manager : MonoBehaviour
{
    [Header("Scene Buttons")]
    public Button btnAnxiety;
    public Button btnFilter;
    public Button btnDeadline;

    [Header("Play")]
    public Button btnPlay;

    const string SCENE_ANXIETY  = "ANXIETY_PeperPaintScene_Azam_k";
    const string SCENE_FILTER   = "FILTER_GlassSampleScene Azam k";
    const string SCENE_DEADLINE = "DEADLINE_Bomb_Scene Azam k 1";

    static readonly Color COL_SELECTED   = Color.white;
    static readonly Color COL_DESELECTED = new(0.38f, 0.38f, 0.38f, 1f);

    int    _selected = 0;
    Image[] _imgs;

    void Start()
    {
        _imgs = new[]
        {
            btnAnxiety .GetComponent<Image>(),
            btnFilter  .GetComponent<Image>(),
            btnDeadline.GetComponent<Image>()
        };

        btnAnxiety .onClick.AddListener(() => Select(0));
        btnFilter  .onClick.AddListener(() => Select(1));
        btnDeadline.onClick.AddListener(() => Select(2));
        btnPlay    .onClick.AddListener(Play);

        Select(0);
    }

    void Select(int idx)
    {
        _selected = idx;
        for (int i = 0; i < _imgs.Length; i++)
            _imgs[i].color = (i == idx) ? COL_SELECTED : COL_DESELECTED;
    }

    void Play()
    {
        string[] scenes = { SCENE_ANXIETY, SCENE_FILTER, SCENE_DEADLINE };
        SceneManager.LoadScene(scenes[_selected]);
    }
}
