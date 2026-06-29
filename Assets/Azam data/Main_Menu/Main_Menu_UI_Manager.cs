using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine.Serialization;

public class Main_Menu_UI_Manager : MonoBehaviour, IManagerInterface
{
    [Header("Scene Buttons")] public Button btnAnxiety;
    [SerializeField] Image anxietySelectedImg = null;

    public Button btnFilter;
    [SerializeField] Image filterSelectedImg = null;

    public Button btnDeadline;
    [SerializeField] Image deadlineSelectedImg = null;


    [Header("Play")] public Button btnPlay;

    [SerializeField] Sprite musicOnSprite = null;
    [SerializeField] Sprite musicOffSprite = null;
    [SerializeField] Button musicBtn = null;

    private Image _musicImage = null;

    private const string SCENE_ANXIETY = "ANXIETY_PeperPaintScene_Azam_k";
    private const string SCENE_FILTER = "FILTER_GlassSampleScene Azam k";
    private const string SCENE_DEADLINE = "DEADLINE_Bomb_Scene Azam k 1";
    private const string BgMusicPrefKey = "BGMusic";

    // public Color COL_SELECTED = Color.white;
    // public Color COL_DESELECTED = new(0.38f, 0.38f, 0.38f, 1f);

    private Image _currentSelectedImg = null;

    int _selected = 0;
    Image[] _imgs;

    public IEnumerator Initialize()
    {
        _imgs = new[]
        {
            btnAnxiety.GetComponent<Image>(),
            btnFilter.GetComponent<Image>(),
            btnDeadline.GetComponent<Image>()
        };

        btnAnxiety.onClick.AddListener(() => { SelectBtn(0, anxietySelectedImg); });
        btnFilter.onClick.AddListener(() => { SelectBtn(1, filterSelectedImg); });
        btnDeadline.onClick.AddListener(() => { SelectBtn(2, deadlineSelectedImg); });
        btnPlay.onClick.AddListener(Play);

        musicBtn.onClick.AddListener(MusicBtn);

        _musicImage = musicBtn.GetComponent<Image>();
        RefreshMusicButtonVisual();

        anxietySelectedImg.DOFade(0, 0);
        filterSelectedImg.DOFade(0, 0);
        deadlineSelectedImg.DOFade(0, 0);

        Select(0, anxietySelectedImg);


        yield return null;
    }

    public IEnumerator PostInitialize()
    {
        yield return null;
    }

    public IEnumerator SetForGameplay()
    {
        RefreshMusicButtonVisual();
        ApplyBgMusicPlayback();
        yield return null;
    }

    private void MusicBtn()
    {
        SoundManager.Instance.PlayAudio(SoundManager.Instance.BtnClickSfx);

        PlayerPrefs.SetInt(BgMusicPrefKey, IsBgMusicEnabled() ? 0 : 1);
        PlayerPrefs.Save();

        RefreshMusicButtonVisual();
        ApplyBgMusicPlayback();
    }

    static bool IsBgMusicEnabled()
    {
        return PlayerPrefs.GetInt(BgMusicPrefKey, 1) == 1;
    }

    void RefreshMusicButtonVisual()
    {
        if (_musicImage == null)
            return;

        _musicImage.sprite = IsBgMusicEnabled() ? musicOnSprite : musicOffSprite;
    }

    void ApplyBgMusicPlayback()
    {
        if (SoundManager.Instance == null)
            return;

        if (IsBgMusicEnabled())
            SoundManager.Instance.PlayAudio(SoundManager.Instance.BgMusicASfx, true);
        else
            SoundManager.Instance.StopAudioSource(SoundManager.Instance.BgMusicASfx);
    }

    private void SelectBtn(int idx, Image currentBtnImg)
    {
        if (_currentSelectedImg == currentBtnImg) return;
        
        SoundManager.Instance.PlayAudio(SoundManager.Instance.BtnClickSfx);
        
        Select(idx,currentBtnImg);
    }

    private void Select(int idx, Image currentBtnImg)
    {
        if (_currentSelectedImg == currentBtnImg) return;

        if (_currentSelectedImg) _currentSelectedImg.DOFade(0, .2f);

        _selected = idx;
        _currentSelectedImg = currentBtnImg;

        if (_currentSelectedImg) _currentSelectedImg.DOFade(1, .2f);

        // for (int i = 0; i < _imgs.Length; i++)
        //     _imgs[i].color = (i == idx) ? COL_SELECTED : COL_DESELECTED;
    }

    private void Play()
    {
        SoundManager.Instance.PlayAudio(SoundManager.Instance.PlayBtnSfx);

        string[] scenes = { SCENE_ANXIETY, SCENE_FILTER, SCENE_DEADLINE };

        _ = SceneLoadManager.Instance.LoadSceneAsync(scenes[_selected], LoadSceneMode.Single, true, 0.5f);
    }
}