using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using VInspector;

[DefaultExecutionOrder(-1)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Tab("Settings")] // ------------------------------------------
    public GameStates gameState = GameStates.GamePlay;
    public GameDifficulty gameDifficulty = GameDifficulty.None;
    public bool funnyMode; 
    public bool debugMode;
    [SerializeField] private KeyCode quitGameKey;
    [SerializeField] private KeyCode toggleFunnyMode = KeyCode.F2;
    [SerializeField] private KeyCode toggleDebugMode = KeyCode.F1;
    [SerializeField] private KeyCode restartSceneKey = KeyCode.F4;
    [SerializeField] private KeyCode finishGame = KeyCode.F8;
    [SerializeField] private KeyCode deleteSave = KeyCode.F9;
    public Level[] levels;
    public Unlock[] unlocks;
    [EndTab]
    
    [Tab("References")] // ------------------------------------------
    [SerializeField] private InputManager inputManagerPrefab;
    [SerializeField] private SoundManager soundManagerPrefab;
    [SerializeField] private GameUIManager gameUIManagerPrefab;
    [SerializeField] private VFXManager vfxManagerPrefab;
    [SerializeField] private TextMeshPro statUpdatePrefab;
    private Camera _camera;
    [EndTab]
    
    public static event UnityAction<GameStates> OnOnGameStateChange;
    public static event UnityAction<GameDifficulty> OnOnGameDifficultyChange;
    private readonly Dictionary<string, string> _infoTexts = new Dictionary<string, string>();
    
    
    
    
    private void Awake() {
        
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SetUpTutorialTexts();
        
    }
    
    private void Start() {
        
        // Check that all managers are instanced
        if (InputManager.Instance == null) { Instantiate(inputManagerPrefab); }
        if (SoundManager.Instance == null) { Instantiate(soundManagerPrefab); }
        if (GameUIManager.Instance == null) { Instantiate(gameUIManagerPrefab); }
        if (VFXManager.Instance == null) { Instantiate(vfxManagerPrefab); }

    }
    
    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    #if UNITY_EDITOR
        private void OnValidate()
        {
            SetUpTutorialTexts();
        } 
    #endif


    private void OnActiveSceneChanged(Scene currentScene, Scene nextScene)
    {
        if (!_camera) { _camera = Camera.main;}
        
        if (nextScene.name == "MainMenu") {
            
            SetGameState(GameStates.None);
            SetGameDifficulty(GameDifficulty.None);
        } else {
            SetGameState(GameStates.GamePlay);
            SetGameDifficulty(GameDifficulty.None);
        }
        
        LoadCollectibles();
        LoadStats();
    }
    
    
    private void Update() {
        if (Input.GetKeyUp(quitGameKey)) { QuitGame(); }
        if (Input.GetKeyUp(restartSceneKey)) { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
        if (Input.GetKeyUp(toggleDebugMode)) { ToggleDebugMode(); SoundManager.Instance?.PlaySoundFX("Toggle");}
        if (Input.GetKeyUp(toggleFunnyMode)) { ToggleFunnyMode(); SoundManager.Instance?.PlaySoundFX("Toggle");}
        if (Input.GetKeyUp(finishGame)) { FinishGame(); SoundManager.Instance?.PlaySoundFX("Toggle");}
        if (Input.GetKeyUp(deleteSave)) { DeleteSave(); SoundManager.Instance?.PlaySoundFX("Toggle");}
    }
    

    public void QuitGame()
    {
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
        #endif
    }
    
    [Button] private void FinishGame() {
        
        // Collect all collectibles
        CollectAllCollectibles();
        
        // Set no death run for all levels
        foreach (Level level in levels) {
            level.noDeathRunRun = true;
            SaveManager.SaveBool(level.connectedScene.SceneName + " NoDeathRun", level.noDeathRunRun);
        }

        
        // Set highest level
        SaveManager.SaveInt("HighestLevel", levels.Length);
        SaveManager.SaveString("SavedLevel", "Level1");
        SaveManager.SaveInt("TotalCollectibles", levels.Length);
        
        // Restart scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    
    
    
    [Button] public void DeleteSave()
    {
        SaveManager.DeleteAllKeys();
        SaveManager.SaveInt("HighestLevel", 1);
        SaveManager.SaveString("SavedLevel", "Level1");
        SaveManager.SaveInt("SavedCheckpoint", 0);
        SettingsManager.Instance?.LoadAllSettings();
        ResetCollectibles();
        ResetUnlocks();
        ResetStats();
        

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    

    
    #region Stats // -----------------------------------------
    
    private void LoadStats()
    {
        foreach (Level level in levels) {
            
            level.totalDeaths = SaveManager.LoadInt(level.connectedScene.SceneName + " TotalDeaths");
            level.bestTime = SaveManager.LoadFloat(level.connectedScene.SceneName + " BestTime");
            level.noDeathRunRun = SaveManager.LoadBool(level.connectedScene.SceneName + " NoDeathRun");
        }
    }

    private void ResetStats()
    {
        foreach (Level level in levels)
        {
            level.totalDeaths = 0;
            level.bestTime = 0;
            level.noDeathRunRun = false;
        }
    }
    
    
    
    public void SaveBestTime(string connectedLevelName, float time)
    {
        foreach (Level level in levels) {
            if (level.connectedScene.SceneName == connectedLevelName) {

                if (time < level.bestTime || level.bestTime == 0) {
                    level.bestTime = time;
                    SaveManager.SaveFloat(level.connectedScene.SceneName + " BestTime", level.bestTime);

                    if (PlayerController.Instance && funnyMode)
                    {
                        StartCoroutine(UpdateStatEffect(PlayerController.Instance.transform.position, text: $"New Best Time! {time:F1}", color: Color.green));
                    }
                }
                return;
            }
        }
    }

    public void SaveDeath(string connectedLevelName)
    {
        foreach (Level level in levels) {
            if (level.connectedScene.SceneName == connectedLevelName) {

                level.totalDeaths += 1;
                SaveManager.SaveInt(level.connectedScene.SceneName + " TotalDeaths", level.totalDeaths);
                
                if (PlayerController.Instance&& funnyMode)
                {
                    StartCoroutine(UpdateStatEffect(PlayerController.Instance.transform.position, text:"+1 Death", color: Color.red));    
                }
                
            }
        }
    }
    
    public void SaveNoDeathRun(string connectedLevelName)
    {
        foreach (Level level in levels) {
            if (level.connectedScene.SceneName == connectedLevelName) {
                if (!level.noDeathRunRun)
                {
                    level.noDeathRunRun = true;
                    SaveManager.SaveBool(level.connectedScene.SceneName + " NoDeathRun", true);
                
                    if (PlayerController.Instance&& funnyMode)
                    {
                        StartCoroutine(UpdateStatEffect(PlayerController.Instance.transform.position,text:"No Death Run!", color: Color.green));    
                    }
                    
                }

            }
        }
    }
    
    
    
    private IEnumerator UpdateStatEffect(Vector3 position, string text, float duration = 2, Color color = default)
    {
        if (!statUpdatePrefab) yield return null;

        TextMeshPro stat = Instantiate(statUpdatePrefab, position, statUpdatePrefab.transform.rotation);
        Vector3 randDir = new Vector3(Random.Range(-3f, 3f), Random.Range(1f, 2f), 0);
        Vector3 randRot = new Vector3(0, 0, Random.Range(-60f, 60f));
        float randSize = Random.Range(1.2f, 1.7f);
        
        stat.text = text;
        stat.color = color;
        Tween.Alpha(stat,startValue:1, 0, duration);
        Tween.Position(stat.transform, stat.transform.position + randDir, duration);
        Tween.Rotation(stat.transform, randRot, duration);
        Tween.Scale(stat.transform, randSize, duration);
        
        yield return new WaitForSeconds(duration);
        if (stat) Destroy(stat.gameObject);
    }
    
    #endregion // -----------------------------------------


    #region Levels // -----------------------------------------

    public bool SceneIsALevel()
    {
        foreach (Level level in levels) {
            if (SceneManager.GetActiveScene().name == level.connectedScene.SceneName) {
                return true;
            }
        }
        return false;
    }

    public string CurrentLevel()
    {
        foreach (Level level in levels) {
            if (SceneManager.GetActiveScene().name == level.connectedScene.SceneName) {
                return level.connectedScene.SceneName;
            }
        }
        return "Not A Level!";
    }

    public int CurrentLevelNumber()
    {
        foreach (Level level in levels) {
            if (SceneManager.GetActiveScene().name == level.connectedScene.SceneName) {
                return System.Array.IndexOf(levels, level);
            }
        }
        return 0;
    }
    

    #endregion Levels // -----------------------------------------
    
    
    #region GameStates // -----------------------------------------

    
    private void ToggleFunnyMode()
    {
        funnyMode = !funnyMode;
    }
    
    private void ToggleDebugMode() {
        debugMode = !debugMode;
        GameUIManager.Instance?.ToggleDebugUI(debugMode);
    }
    
    
    public void SetGameState(GameStates state) {
        
        gameState = state;
        OnOnGameStateChange?.Invoke(state);
        
        switch (state) {
            case GameStates.GamePlay:
                Time.timeScale = 1;
                
                break;
            case GameStates.Paused:
                Time.timeScale = 0;
                
                break;
            case GameStates.GameOver:
                Time.timeScale = 0;
                break;
            
                case GameStates.None:
                Time.timeScale = 1;
                break;
        }

        
    }
    
    private void SetGameDifficulty(GameDifficulty gameMode) {
        gameDifficulty = gameMode;

        switch(gameMode)
        {
            case GameDifficulty.Easy:

                break;
            case GameDifficulty.Normal:

                break;
            case GameDifficulty.Hard:

                break;
            
            case GameDifficulty.None:
                break;
            default:
                Debug.LogError("Invalid Game Mode, Setting Normal");
                break;
        }
        
        OnOnGameDifficultyChange?.Invoke(gameMode);
    }
    
    

    #endregion GameStates
    
    
    #region Unlockss // ---------------------------------

    private void CheckUnlocks()
    {
        
        foreach (Unlock unlock in unlocks)
        {
            if (TotalCollectiblesCollected() >= unlock.unlockedAtCollectible)
            {
                unlock.received = TotalCollectiblesCollected() >= unlock.unlockedAtCollectible;
            }
        }
    }
    
    public void ToggleUnlock(string unlockName, bool state)
    {
        foreach (Unlock unlock in unlocks)
        {
            if (unlock.name == unlockName)
            {
                unlock.state = state;
                SaveManager.SaveBool(unlockName, unlock.state);
                PlayerController.Instance?.ToggleAllCosmetics();
                return;
            }
        }
        Debug.LogError("Unlock not found");
    }

    public bool CheckUnlockReceived(string unlockName)
    {
        foreach (Unlock unlock in unlocks)
        {
            if (unlock.name == unlockName)
            {
                return unlock.received;
            }
        }
        Debug.LogError("Unlock not found");
        return false;
    }
    
    public bool CheckUnlockActive(string unlockName)
    {
        foreach (Unlock unlock in unlocks)
        {
            if (unlock.name == unlockName)
            {
                return unlock.state;
            }
        }
        Debug.LogError("Unlock not found");
        return false;
    }
    
    private void ResetUnlocks()
    {
        foreach (Unlock unlock in unlocks)
        {
            unlock.received = false;
            unlock.state = false;
        }
    }
    
    #endregion Unlocks
    
    
    #region Collectibles // -----------------------------------------------
    
    public void CollectCollectible(string connectedLevelName) {
        
        if (IsCollectibleCollected(connectedLevelName)) { return; }
        
        foreach (Level level in levels) {
            if (level.connectedScene.SceneName == connectedLevelName) {
                level.collectibleCollected = true;
                SaveManager.SaveBool(level.connectedScene.SceneName + " Collectible", level.collectibleCollected);
                return;
            }
        }
    }
    
    
    public bool IsCollectibleCollected(string levelName) {
        foreach (Level level in levels) {
            if (level.connectedScene.SceneName == levelName) {
                return level.collectibleCollected;
            }
        }
        return false;
    }

    public int TotalCollectiblesAmount() // The total amount of collectibles in the game
    {
        int total = 0;
        foreach (Level level in levels) {
            if (level.countsTowardsUnlocks) {
                total++;
            }
        }
        return total;
    }
    
    public int TotalCollectiblesCollected() // The total amount of the collected collectibles
    {
        int total = 0;
        foreach (Level level in levels) {
            if (level.collectibleCollected && level.countsTowardsUnlocks) {
                total++;
            }
        }
        return total;
    }

    private void LoadCollectibles()
    {
        foreach (Level level in levels) {
            
            level.collectibleCollected = SaveManager.LoadBool(level.connectedScene.SceneName + " Collectible");
        }

        CheckUnlocks();

    }

    private void ResetCollectibles()
    {
            foreach (Level level in levels) 
            {
                level.collectibleCollected = false;
                SaveManager.SaveBool(level.connectedScene.SceneName + " Collectible", level.collectibleCollected);
            }
            
            CheckUnlocks();
    }

    private void CollectAllCollectibles()
    {
        
            foreach (Level level in levels) 
            {
                level.collectibleCollected = true;
                SaveManager.SaveBool(level.connectedScene.SceneName  + " Collectible", level.collectibleCollected);
            }

            CheckUnlocks();
    }
    
    #endregion Collectibles

    
    #region Tutorial Text // -----------------------------------------------

    private void SetUpTutorialTexts()
    {
        _infoTexts.Clear();
        
        AddInfoText("move", "Use <sprite=46> / <sprite=40><sprite=104> to move"); // Use <sprite=46> / <sprite=40><sprite=104> to move
        AddInfoText("jump", "Press <sprite=66> / <sprite=232> / <sprite=210> to jump"); // Press <sprite=66> / <sprite=210> to jump
        AddInfoText("fastDrop", "press <sprite=44> / <sprite=206> \n to fall faster");
        AddInfoText("doubleJump", "press jump mid-air  \n to double jump"); // press <sprite=66> / <sprite=210> mid-air  \n \nto double jump
        AddInfoText("fastSlide", "press <sprite=44> / <sprite=206> to slide faster");
        AddInfoText("dropDown", "press down + jump to drop");
        AddInfoText("dash", "press Alt to dash");
    }

    private void AddInfoText(string id, string text)
    {
        _infoTexts[id] = text;
    }

    public string GetInfoText(string id)
    {
        return _infoTexts.GetValueOrDefault(id, "Text not found");
    }
    


    #endregion
}


