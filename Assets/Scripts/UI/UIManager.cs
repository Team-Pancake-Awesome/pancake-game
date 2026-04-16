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
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            if(GameIsPaused == true && endOfDayUI.activeInHierarchy == false)
            {
                Resume();
            } else if(endOfDayUI.activeInHierarchy == true)
            {
                
            } else
            {
                Pause();   
            }
        }
        if (Input.GetKeyDown(KeyCode.G))
        {
            if(debugMenu == true)
            {
                CloseDebug();
            } else
            {
                OpenDebug();
            }
        } 
        if (Input.GetKeyDown(KeyCode.R))
        {
            Restart();
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
        SceneManager.LoadScene(gameSceneName);
        Time.timeScale = 1f;
        GameIsPaused = false;
        UIOpen = false;
    }

    public void Exit()
    {
        Application.Quit();
        Debug.Log("Application has quit");
    }

    public void LoadGameScene()
    {
        //Used to load between MainMenu, the Main game scene, and Tutorial scene (if we make one).
        Time.timeScale = 1f;
        GameIsPaused = false;
        UIOpen = false;
        SceneManager.LoadScene(gameSceneName);
    }

    public void LoadMainMenuScene()
    {
        Time.timeScale = 1f;
        GameIsPaused = false;
        UIOpen = false;
        SceneManager.LoadScene(mainMenu);
    }

    public void Pause()
    {
        GameIsPaused = true;
        pauseMenu.SetActive(true);
        UIOpen = true;
        Time.timeScale = 0f;        
    }

    public void OpenDebug()
    {
        debugMenu.SetActive(true);
    }

    void CloseDebug()
    {
        debugMenu.SetActive(false);
    }
}
