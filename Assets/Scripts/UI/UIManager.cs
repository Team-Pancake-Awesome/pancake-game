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
    public GameObject HappyPancake;
    public GameObject MidPancake;
    public GameObject BurntPancake;
    public GameObject receipt;
    
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
            } else if(UIOpen == true)
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
        WorkdayManager.Instance.EndWorkday();
        //subscribe to ondayended event in workday manager in order to make the end of day ui open
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
        UIOpen = true;
        Time.timeScale = 0f;
        GameIsPaused = true;
    }

    /*void SetActivePancakeCharacter()
    {
        if (PancakeDoneness || PancakeDoneness )
        {
            BurntPancake.SetActive(true);
        } else if (PancakeDoneness || PancakeDoneness)
        {
            MidPancake.SetActive(true);
        } else
        {
            HappyPancake.SetActive(true);
        }
    }*/
}
