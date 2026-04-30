using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class UIManager : MonoBehaviour
{
    private static bool GameIsPaused = false;
    private static bool UIOpen = false;

    [Header("UI References")]
    public GameObject endOfDayUI;
    public GameObject debugMenu;
    public GameObject pauseMenu;
    public GameObject gameUI;
    public GameObject HappyPancake;
    public GameObject MidPancake;
    public GameObject BurntPancake;
    
    public string mainMenu;
    public string gameSceneName;
    
    void Start()
    {
       Time.timeScale = 1f;
       GameIsPaused = false;
       UIOpen = false;

       if (WorkdayManager.Instance != null)
        {
            WorkdayManager.Instance.OnDayEnded += HandleEndOfDay;
        }
    }

    void OnDestroy()
    {
        if (WorkdayManager.Instance != null)
        {
            WorkdayManager.Instance.OnDayEnded -= HandleEndOfDay;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            if(GameIsPaused && !endOfDayUI.activeInHierarchy)
            {
                Resume();
            } else if(UIOpen)
            {
                
            } else
            {
                Pause();   
            }
        }
        if (Input.GetKeyDown(KeyCode.G))
        {
            if(debugMenu.activeInHierarchy)
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

    private void HandleEndOfDay(WorkdaySummary summary)
    {
        OpenEndOfDay();
        SetActivePancakeCharacter(summary.averageStars);
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

    void OpenEndOfDay()
    {
        endOfDayUI.SetActive(true);
        gameUI.SetActive(false);
        UIOpen = true;
        Time.timeScale = 0f;
        GameIsPaused = true;
    }

    public void CloseEndOfDay()
    {
        endOfDayUI.SetActive(false);
        gameUI.SetActive(true);
        UIOpen = false;
        Time.timeScale = 1f;
        GameIsPaused = false;
    }

    void SetActivePancakeCharacter(float averageStars)
    {
        HappyPancake.SetActive(false);
        MidPancake.SetActive(false);
        BurntPancake.SetActive(false);

        if (averageStars < 2.0f)
        {
            BurntPancake.SetActive(true);
        } else if (averageStars < 4.0f)
        {
            MidPancake.SetActive(true);
        } else
        {
            HappyPancake.SetActive(true);
        }
    }
}
