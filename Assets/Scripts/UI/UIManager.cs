using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    private static bool GameIsPaused = false;
    private static bool UIOpen = false;

    [Header("UI References")]
    public GameObject endOfDayUI;
    public GameObject debugMenu;
    
    [SerializeField]
    public string mainMenu;
    public string gameSceneName;
    // Start is called before the first frame update
    void Start()
    {
       Time.timeScale = 1f;
       GameIsPaused = false;
       UIOpen = false;
 
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void Restart()
    {
        //Restart game scene
        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    void Exit()
    {
        //Exit the game
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
    }

    void LoadGameScene()
    {
        //Used to load between MainMenu, the Main game scene, and Tutorial scene (if we make one).
        SceneManager.LoadScene(gameSceneName);
    }

    void LoadMainMenuScene()
    {
        SceneManager.LoadScene(mainMenu);
    }

    void OpenDebug()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            debugMenu.SetActive(true);
        }
        while (debugMenu == true)
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                GameIsPaused = true;
                Time.timeScale = 0f;
            }
        }
        Time.timeScale = 1f;
        GameIsPaused = false;
    }
}
