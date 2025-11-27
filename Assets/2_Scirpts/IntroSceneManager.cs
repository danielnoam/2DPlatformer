using System;
using System.Collections;
using CustomAttribute;
using UnityEngine;
using UnityEngine.SceneManagement;
using VInspector;
using PrimeTween;
using TMPro;
using UnityEngine.UI;

public class IntroSceneManager : MonoBehaviour
{
    

    [Header("References")] 
    [SerializeField] private SceneField mainMenuScene;
    [SerializeField] private Image player;
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI gameTitle;
    
    private Vector3 _defaultPlayerSize;
    private Vector3 _defaultPlayerRotation;
    private Vector3 _defaultPlayerPosition;
    private  Color _defaultPlayerColor;
    private float _defaultPlayerAlpha;
    private Vector3 _defaultGameTitlePosition;
    private Vector3 _defaultGameTitleRotation;
    private Vector3 _defaultGameTitleSize;
    private Color _defaultGameTitleColor;
    private float _defaultGameTitleAlpha;
    private  Sequence _sequence;

    private void Awake()
    {
        _defaultPlayerSize = player.transform.localScale;
        _defaultPlayerRotation = player.transform.eulerAngles;
        _defaultPlayerPosition = player.transform.position;
        _defaultPlayerColor = player.color;
        _defaultPlayerAlpha = player.color.a;
        
        _defaultGameTitlePosition = gameTitle.transform.position;
        _defaultGameTitleRotation = gameTitle.transform.eulerAngles;
        _defaultGameTitleSize = gameTitle.transform.localScale;
        _defaultGameTitleColor = gameTitle.color;
        _defaultPlayerAlpha = gameTitle.color.a;
    }


    private void Start()
    {
        PlayTestIntro2();
    }
    

    private void GoToMainMenu()
    {
        if (mainMenuScene == null) return;
        
        SceneManager.LoadScene(mainMenuScene.BuildIndex);
    }
    
    
    [Button] private void ResetIntroScreen()
    {
        player.transform.localScale = _defaultPlayerSize;
        player.transform.eulerAngles = _defaultPlayerRotation;
        player.transform.position = _defaultPlayerPosition;
        player.color = _defaultPlayerColor;

        if (_sequence.isAlive)
        {
            _sequence.Stop();
        }
    }
    
    
    
    


    [Button] private void PlayTestIntro2()
    {
        ResetIntroScreen();

        float duration = 2;

        _sequence = Sequence.Create(2, Sequence.SequenceCycleMode.Yoyo)
            .ChainDelay(1f)
            .Group(Tween.Alpha(player, startValue: 0, endValue: 1, duration: duration, Ease.InExpo))
            .Group(Tween.Scale(player.transform, startValue: 0.1f, endValue: 4, duration: duration))
            .Group(Tween.EulerAngles(player.transform, startValue: Vector3.zero, endValue: new Vector3(0, 0, -720f), duration: duration, ease: Ease.InOutSine))
            .OnComplete(GoToMainMenu);

    }





    
}
