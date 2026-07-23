using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Obstacle : MonoBehaviour
{
    LevelLoader levelLoader;

    private BGMScript bs;
    private bool isLoadingScene;

    private IEnumerator Start()
    {
        bs = FindObjectOfType<BGMScript>(true);
        yield return WaitForActiveLevelLoader();
    }

    private IEnumerator WaitForActiveLevelLoader()
    {
        while (!TryCacheActiveLevelLoader())
        {
            yield return null;
        }
    }

    private bool TryCacheActiveLevelLoader()
    {
        if (levelLoader != null && levelLoader.isActiveAndEnabled)
            return true;

        foreach (LevelLoader loader in FindObjectsOfType<LevelLoader>(true))
        {
            if (!loader.isActiveAndEnabled)
                continue;

            levelLoader = loader;
            return true;
        }

        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<InputPlayer>(out InputPlayer player))
        {
            if (isLoadingScene)
                return;

            isLoadingScene = true;
            player.Die();

            if (bs != null)
                bs.FadeOut();

            StartCoroutine(LoadCurrentSceneWhenReady());
        }
    }

    private IEnumerator LoadCurrentSceneWhenReady()
    {
        yield return WaitForActiveLevelLoader();
        levelLoader.LoadScene(SceneManager.GetActiveScene().name);
    }

    //기존코드
    //private void OnCollisionEnter2D(Collision2D collision)
    //{
    //    if (collision.gameObject.TryGetComponent<NewPlayer1>(out var player1))
    //    {
    //        player1.Die();
    //        bs.FadeOut();
    //        levelLoader.LoadScene(SceneManager.GetActiveScene().name);
    //    }
    //    else if (collision.gameObject.TryGetComponent<NewPlayer2>(out var player2))
    //    {
    //        player2.Die();
    //        bs.FadeOut();
    //        levelLoader.LoadScene(SceneManager.GetActiveScene().name);
    //    }
    //}
}
