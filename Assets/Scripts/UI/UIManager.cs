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
    public GameObject pauseMenu;
    
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
        //need to handle key input
        if(GameIsPaused == true)
        {
            Time.timeScale = 0f;
        }
    }

    public void Resume()
    {
            GameIsPaused = false;
            pauseMenu.SetActive(false);
            UIOpen = false;
        
    }

    public void Restart()
    {
        //Restart game scene
        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    public void Exit()
    {
        Application.Quit();
    }

    public void LoadGameScene()
    {
        //Used to load between MainMenu, the Main game scene, and Tutorial scene (if we make one).
        Time.timeScale = 1f;
        GameIsPaused = false;
        SceneManager.LoadScene(gameSceneName);
    }

    public void LoadMainMenuScene()
    {
        Time.timeScale = 1f;
        GameIsPaused = false;
        SceneManager.LoadScene(mainMenu);
    }

    public void Pause()
    {
        if(Input.GetKeyDown(KeyCode.P) && UIOpen == false)
        {
            GameIsPaused = true;
            pauseMenu.SetActive(true);
            UIOpen = true;
        }
    }

    public void OpenDebug()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            debugMenu.SetActive(true);
        }
        while (debugMenu == true)
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                Pause();
            }
        }
    }
}
