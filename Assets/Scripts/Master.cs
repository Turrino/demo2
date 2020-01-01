﻿using Assets.Scripts.Bayeux;
using Assets.Scripts.Resources;
using BayeuxBundle;
using BayeuxBundle.Models.Palettes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static DialogueManager;

public class Master : MonoBehaviour {

    public static Master GM;
    public GameObject Player;
    public GameObject CurrentParent;
    public ScenarioData ScenarioData = null;
    public Location Location;
    public LocationInfo CurrentLocation = null;
    public DialogueManager DialogueManager;

    public DrawerLogic Contents;

    public GameObject Tooltip;
    public GameObject PomaUI;
    public Button PomaButton;
    public bool PomaButtonOver;

    //public ListButtonScript ListButton;
    //public Player PlayerScript;
    public GameObject PlayerInstance;
    public Texture2D MasterTexture;
    public GameObject LoadingScreen;
    public GameObject LocationInterface;
    public ClockScript Clock;
    public GameObject ClockUI;
    public TimeSpan TotalTimeSpent;
    public AudioSource AudioSrc;

    public GameObject Final;
    public FinalScript FinalScript;

    public GameObject CanvasBars;

    public int LevelCounter;

    public SfxScript Sfx;
    public bool LoadingScreenOn;
    public bool IsFinalScene;
    //public bool RenderLowResAssets;
    private int difficulty;

    private bool AllMenuOverlaysOff() => !DialogueManager.DialogueEnabled && !DialogueManager.MenuEnabled && !LoadingScreenOn;
    public bool TooltipEnabled() => AllMenuOverlaysOff() && !Contents.Open ;
    public bool DrawerTooltipEnabled() => AllMenuOverlaysOff() && Contents.Open;

    private GameObject _parentRef;
    private BackgroundMusic _music;
    private int LevelChangeFlag = int.MaxValue;
    private List<Sprite> VariousCharacters;

    void Awake () {
        if (GM == null)
        {
            GM = this;
            DontDestroyOnLoad(gameObject);

            LocalRandom.ConfigureRandom(new System.Random());

            _music = GetComponent<BackgroundMusic>();
            AudioSrc = GetComponent<AudioSource>();

            var settings = GameObject.Find("Settings");
            difficulty = settings != null?
                settings.GetComponent<Settings>().DifficultySetting
                : 1;

            LevelCounter = 0;

            PomaButton.onClick.AddListener(() => {
                TriggerPoma();
            });

            ConfigureColors();

            LevelSetup();            
        }            
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public Queue<Sprite> GetFinalChars() => new Queue<Sprite>(VariousCharacters); 
    public static void ConfigureColors()
    {
        var textureTools = new TextureTools(OverlayRef.Am6RefDictWHash);
        ColorsList.OverlayProps = textureTools.StringToColor(NamesList.OverlayProps);
        ColorsList.OverlayWallProps = textureTools.StringToColor(NamesList.OverlayWallProps);
        ColorsList.OverlayCharacter = textureTools.StringToColor(NamesList.OverlayCharacter);
        ColorsList.OverlayMapLocation = textureTools.StringToColor(NamesList.OverlayMapLocation);
        ColorsList.OverlayMapSpawn = textureTools.StringToColor(NamesList.OverlayMapSpawn);

        Palettes.BgColors = Palettes.BgStringColors.Select(x => textureTools.StringToColor32(x)).ToList();
        Palettes.Replacement0 = textureTools.ConvertPalette(Palettes.Replacement0String.AddHashes());
        Palettes.Replacement1 = textureTools.ConvertPalette(Palettes.Replacement1String.AddHashes());
        Palettes.Replacement2 = textureTools.ConvertPalette(Palettes.Replacement2String.AddHashes());
    }

    public Transform GetSceneTransform()
    {
        return _parentRef.transform;
    }

    public void QuitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
             Application.Quit();
#endif
    }

    public void Start()
    {
        Location.CreateLocation(CurrentLocation, GetSceneTransform());
    }

    public void RestartButton()
    {
        DialogueManager.Setup();
        LevelCounter = 0;
        CallNextLevel();        
    }

    public void NextLevel()
    {
        LocationInterface.SetActive(false);
        LevelCounter++;
        if (LevelCounter == Levels.LevelList.Length)
        {
            FinalMapSetup();
            //DialogueManager.GameEnd();
        }
        else
        {
            CallNextLevel();
        }
    }

    public void MayoIsCaughtMusic()
    {
        AudioSrc.clip = _music.OutroMusic;
        AudioSrc.time = 0;
        AudioSrc.Play();
        AudioSrc.loop = true;
    }

    public void ToOutro()
    {
        Destroy(_parentRef);
        DialogueManager.HideInfo();
        ClockUI.SetActive(false);
        LocationInterface.SetActive(false);
        FinalScript.Location.enabled = false;
        HidePoma();
        CanvasBars.SetActive(false);
    }

    public void FinalMapSetup()
    {
        SetLoadingScreen();
        IsFinalScene = true;
        Destroy(_parentRef);
        var finalStuff = Instantiate(Final, Vector2.zero, Quaternion.identity);
        finalStuff.transform.SetParent(gameObject.transform);
        FinalScript = finalStuff.GetComponent<FinalScript>();
        PlayerInstance.transform.position = FinalScript.PolisSpawnPoint.transform.position + new Vector3(-0.2f, 0.2f, 0);

        DialogueManager.HideInfo();
        DialogueManager.FinalSceneSetup();

        ToggleLocation(true);

        
        AudioSrc.clip = _music.MayoMusic;
        AudioSrc.time = 0;
        AudioSrc.Play();
        AudioSrc.loop = true;

        Clock.FinalTimer();
        PomaUI.GetComponentInChildren<Text>().text = "Mr. Mayor Mayo IS A BIG FAT LIAR";
        Sfx.Stop();
        SetLoadingScreenOff();
    }

    public void FinalLoc()
    {
        FinalScript.PolisSpawnPoint.enabled = false;
        FinalScript.Map.enabled = false;
        FinalScript.Location.enabled = true;

        ToggleLocation(false);

        DialogueManager.ShowInfo("Mr. Mayor Mayo");
    }

    public void ChangeScene(LocationInfo To, bool restart = false)
    {
        // Save the current playback time & stop music
        CurrentLocation.MusicPlaybackTime = AudioSrc.time;
        AudioSrc.Stop();
        Sfx.Stop();

        // with the new map movement, we no longer store the location & always spawn at the center
        //var playerLoc = restart ? ScenarioData.Main.SpawnPoint
        //    : CurrentLocation.CoordsOnMainMap;


        // LOCATION CHANGE!
        CurrentLocation = To;
        SceneInit();

        // Restart music        
        AudioSrc.clip = To.IsMap ? GetMapMusicClip() : _music.Clips[CurrentLocation.MusicClipIndex];
        AudioSrc.time = CurrentLocation.MusicPlaybackTime;
        AudioSrc.Play();
        AudioSrc.loop = true;

        ToggleLocation(To.IsMap);

        if (To.IsMap)
        {
            HidePoma();
            DialogueManager.HideInfo();
            SceneManager.LoadScene(NamesList.MainScenario, LoadSceneMode.Single);
            Location.CreateLocation(CurrentLocation, GetSceneTransform());
            PlayerInstance.transform.position = ScenarioData.Main.SpawnPoint;
        }
        else
        {
            SceneManager.LoadScene(NamesList.Location, LoadSceneMode.Single);
            Location.CreateLocation(CurrentLocation, GetSceneTransform());
            DialogueManager.ShowInfo(
                TextResources.LocationInfo(CurrentLocation.Person.Name));
        }
    }

    public void ArrestAttempt()
    {
        Clock.StopTimer();
        TotalTimeSpent = TotalTimeSpent + Clock.GetTimeSpent();
        var success = CurrentLocation.Person.IsThief;
        var function = success ? DiagButtonsFunction.ArrestSuccess :
            DiagButtonsFunction.ArrestFailed;

        var arrestMsg = TextResources.GetArrestMessage(
                success,
                GM.CurrentLocation.Person.Name
            );

        AudioSrc.Stop();

        if (!success)
            Sfx.Fail();

        DialogueManager.CaptureMenu(function, arrestMsg);
    }

    public void TriggerPoma()
    {
        GM.PomaUI.gameObject.SetActive(!GM.PomaUI.gameObject.activeSelf);
    }

    public void HidePoma()
    {
        GM.PomaUI.gameObject.SetActive(false);
    }

    public void WDCTimeout()
    {
        AudioSrc.Stop();
        Sfx.TimeUp();
        DialogueManager.CaptureMenu(DiagButtonsFunction.ArrestFailed,
            TextResources.ArrestTimeout);
    }

    //public void pList()
    //{
    //    _listInstance.name = "List";
    //    _listInstance.transform.SetParent(gameObject.transform);
    //    ListButton = List.GetComponent<ListButtonScript>();
    //}

    public void ToggleLocation(bool isMap = true)
    {
        PlayerInstance.SetActive(isMap);
        Clock.ToggleBadge(isMap);
        LocationInterface.SetActive(!isMap);
    }

    private void LevelSetup()
    {
        if (LevelCounter == 0)
        {
            TotalTimeSpent = new TimeSpan();
            VariousCharacters = new List<Sprite>();
            ClockUI.gameObject.SetActive(true);
            CanvasBars.SetActive(true);
        }

        var scenarioGenerator = new ScenarioSetup(_music.Clips.Length);
        var level = Levels.LevelList[LevelCounter];
        ScenarioData = scenarioGenerator.GenerateScenarioData(level);
        VariousCharacters.Add(ScenarioData.Locations.PickRandom().Person.Sprite);
        // TODO depending on the setup, get the location where the player is supposed to start off
        CurrentLocation = ScenarioData.Main;

        PlayerInstance = Instantiate(Player, ScenarioData.Main.SpawnPoint, Quaternion.identity);
        PlayerInstance.transform.SetParent(gameObject.transform);
        PlayerInstance.name = NamesList.Player;

        SceneInit();

        ToggleLocation(true);
        DialogueManager.HideInfo();

        AudioSrc.clip = GetMapMusicClip();
        AudioSrc.Play();
        AudioSrc.loop = true;
        Clock.StartTimer(Levels.LevelList[LevelCounter].SecondsAvailable);

        SetLoadingScreenOff();
        PomaUI.GetComponentInChildren<Text>().text = GM.ScenarioData.PomaText;
    }

    private AudioClip GetMapMusicClip()
    {
        return LevelCounter < 2 ? _music.MapMusic1 : LevelCounter < 4 ? _music.MapMusic2 : _music.MapMusic3; ;
    }

    private void SceneInit()
    {
        if (_parentRef != null)
        {
            Destroy(_parentRef);
        }
        _parentRef = Instantiate(CurrentParent, new Vector2(0, 0), Quaternion.identity);
        _parentRef.transform.SetParent(transform);
    }

    private void TearDown()
    {
        Destroy(PlayerInstance);
        ScenarioData = null;
        AudioSrc.Stop();
    }

    private void SetLoadingScreen()
    {
        LoadingScreenOn = true;
        DialogueManager.MenuScreen.SetActive(false);
        LoadingScreen.SetActive(true);
    }

    private void SetLoadingScreenOff()
    {
        LoadingScreenOn = false;
        LoadingScreen.SetActive(false);
    }

    private void CallNextLevel()
    {
        SetLoadingScreen();
        LevelChangeFlag = Time.frameCount;
    }

    private void LoadLevel()
    {
        TearDown();
        LevelSetup();
        ChangeScene(ScenarioData.Main, true);
    }

    private void Update()
    {
        // TODO can't get the loading screen to display properly is restart starts too early.
        // find a better fix
        if (Time.frameCount - LevelChangeFlag > 3)
        {
            LevelChangeFlag = int.MaxValue;
            LoadLevel();
        }
    }

    private void FixedUpdate()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (AllMenuOverlaysOff())
                TriggerPoma();
        }

        // No longer needed with browser
        //if (Input.GetKeyUp(KeyCode.Q) || Input.GetKeyUp(KeyCode.Escape))
        //{
        //    Application.Quit();
        //}

        if (Input.GetKeyUp(KeyCode.Space))
        {
            NextLevel();
        }
    }
}