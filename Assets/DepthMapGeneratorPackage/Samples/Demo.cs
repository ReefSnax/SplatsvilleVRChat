using UnityEngine;
using UnityEngine.SceneManagement;

public class Demo : MonoBehaviour
{
    static bool first = true;
    int sceneIndex = 0;

    void Start()
    {
        if (first)
        {
            first = false;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            LoadPrevScene();
        }
        if (Input.GetKeyDown(KeyCode.M))
        {
            LoadNextScene();
        }
    }

    public void LoadPrevScene()
    {
        sceneIndex--;
        if (sceneIndex < 0)
        {
            sceneIndex = SceneManager.sceneCountInBuildSettings - 1;
        }
        SceneManager.LoadScene(sceneIndex);
    }

    public void LoadNextScene()
    {
        sceneIndex++;
        if (sceneIndex > SceneManager.sceneCountInBuildSettings - 1)
        {
            sceneIndex = 0;
        }
        Debug.Log("sceneIndex: "+ sceneIndex + " SceneManager.sceneCountInBuildSettings: " + SceneManager.sceneCountInBuildSettings);
        SceneManager.LoadScene(sceneIndex);
    }
}
