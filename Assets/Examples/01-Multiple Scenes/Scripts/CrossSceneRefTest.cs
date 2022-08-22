using Persyst;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CrossSceneRefTest : MonoBehaviour, ISaveable
{
    [SerializeField][SaveThis] GameObject crossSceneRef;


    [NaughtyAttributes.Button("Load Scene 2")]
    void loadScene(){
        SceneManager.LoadSceneAsync("MS_Scene2", LoadSceneMode.Additive);
    }

    [NaughtyAttributes.Button("Get Reference")]
    void getReference(){
        crossSceneRef = SceneManager.GetSceneByName("MS_Scene2").GetRootGameObjects()[0];
    }
}
