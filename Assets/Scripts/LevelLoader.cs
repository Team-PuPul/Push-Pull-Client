using System.Collections;
using System.IO;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private float LoadingTime = 1f;

    public void LoadNextLevel()
    {
        EventSystem eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem != null)
            eventSystem.gameObject.SetActive(false);

        StartCoroutine(LoadLevel(SceneManager.GetActiveScene().buildIndex + 1));
    }

    public void LoadScene(string name)
    {
        EventSystem eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem != null)
            eventSystem.gameObject.SetActive(false);

        StartCoroutine(LoadLevel(name));
    }

    private IEnumerator LoadLevel(int levelIndex)
    {
        animator.SetTrigger("Start");

        yield return new WaitForSeconds(LoadingTime);

        string sceneName = GetSceneNameByBuildIndex(levelIndex);

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"BuildIndex {levelIndex}에 해당하는 씬을 찾을 수 없습니다.");
            yield break;
        }

        ChangeScene(sceneName);
    }

    private IEnumerator LoadLevel(string name)
    {
        animator.SetTrigger("Start");

        yield return new WaitForSeconds(LoadingTime);

        ChangeScene(name);
    }

    private void ChangeScene(string sceneName)
    {
        if (NetworkManager.singleton != null && NetworkServer.active)
        {
            NetworkManager.singleton.ServerChangeScene(sceneName);
            return;
        }

        if (NetworkClient.active)
        {
            Debug.LogWarning(
                $"클라이언트에서는 직접 씬을 변경하지 않습니다. sceneName={sceneName}"
            );
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private string GetSceneNameByBuildIndex(int buildIndex)
    {
        string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);

        if (string.IsNullOrEmpty(scenePath))
            return string.Empty;

        return Path.GetFileNameWithoutExtension(scenePath);
    }
}
