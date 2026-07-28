using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;

public enum PlayerSounds
{
    Jump = 0,
    Push = 1,
    Pull = 2,	
	Die = 3,
    SoundsCount = 4
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance
	{
		get
		{
			if (instance == null) instance = FindObjectOfType<SoundManager>();
			return instance;
		}
	}
	public AudioMixer audioMixer;

	public const string MasterVolumeKey = "MasterVolume";
	public const string BgmVolumeKey = "MusicVolume";
	public const string SfxVolumeKey = "SFXVolume";

	private static SoundManager instance;
	private ObjectPool<AudioSource> sfxPool;


	private void Awake()
	{
		if(instance == null)
		{
			instance = this;
			DontDestroyOnLoad(gameObject);

			sfxPool = new ObjectPool<AudioSource>(
				createFunc: () =>
				{
					GameObject go = new GameObject("PooledSFX");
					AudioSource source = go.AddComponent<AudioSource>();
					go.transform.SetParent(transform);
					source.outputAudioMixerGroup = audioMixer.FindMatchingGroups("SFX")[0];
					return source;
				},
				actionOnGet: (source) => source.gameObject.SetActive(true),
				actionOnRelease: source =>
				{
					source.Stop();
					source.clip = null;
					source.gameObject.SetActive(false);
				},
				actionOnDestroy: source => Destroy(source.gameObject),
				defaultCapacity: 7,
				maxSize: 15
			);
        }
		else
		{
			Destroy(gameObject);
		}
	}

	// PlayerPrefs 키 이름과 오디오 믹서 키 이름은 동일합니다.
	private void Start()
	{
		LoadSoundKey(MasterVolumeKey);
		LoadSoundKey(SfxVolumeKey);
		LoadSoundKey(BgmVolumeKey);
	}

	// 사운드 PlayerPrefs 내부 값 가져오기.
	private void LoadSoundKey(string key)
	{
        if (PlayerPrefs.HasKey(key))
        {
            SetSoundVolume(key, PlayerPrefs.GetFloat(key));
        }
        else
        {
            SetSoundVolume(key, 1);
        }
    }

    #region Sounds
	public void SFXPlay(string sfxName, AudioClip clip)
	{
		AudioSource source = sfxPool.Get();
		source.gameObject.name = sfxName + "SFX";		// 추후 문자열 최적화 진행(필요 시)
		source.clip = clip;
		source.Play();

		StartCoroutine(ReleaseAfterRealtime(source, clip.length));
	}
    public void BgFadeIn(AudioSource BgPlayer)
	{
		StartCoroutine(FadeIn(BgPlayer));
	}
	public void BgFadeInCustom(AudioSource BgPlayer, float volume, float time)
	{
		StartCoroutine(FadeIn(BgPlayer, volume, time));
	}
	public void BgFadeOut(AudioSource BgPlayer)
	{
		StartCoroutine(FadeOut(BgPlayer));
	}
	public void BgFadeOutCustom(AudioSource BgPlayer, float time)
	{
		StartCoroutine(FadeOut(BgPlayer, time));
	}

	public void SetSoundVolume(string key, float volume)
	{
        float n = Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20;
        audioMixer.SetFloat(key, n);
        PlayerPrefs.SetFloat(key, volume);
    }

    // 변경된 볼륨 값을 디스크에 기록한다.
    //
    // PlayerPrefs.SetFloat은 메모리 캐시에만 쓰고, 실제 디스크 쓰기는 Save()가 한다.
    // Save()를 SetSoundVolume 안에서 부르면 슬라이더를 드래그하는 동안
    // 매 프레임 동기 디스크 쓰기가 발생해 프레임이 끊기므로,
    // 사운드 설정 화면을 떠나는 시점에 한 번만 호출한다.
    // (저장 전에 게임이 종료돼도 Unity가 OnApplicationQuit에서 자동으로 기록한다.)
    public void SaveSoundVolumes()
    {
        PlayerPrefs.Save();
    }

    // 현재 적용 중인 볼륨 값을 반환한다. (UI 슬라이더 표시용)
    //
    // 오디오 믹서에는 데시벨로 변환된 값이 들어 있어 역변환이 필요하므로,
    // PlayerPrefs에 저장된 원본 선형 값(0~1)을 그대로 사용한다.
    // 저장된 값이 없을 때 1을 반환하는 것은 LoadSoundKey의 기본값과 동일하며,
    // 이렇게 해야 슬라이더 표시와 실제 볼륨이 항상 일치한다.
    public float GetSoundVolume(string key)
    {
        return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key) : 1f;
    }

    #endregion

    #region Coroutines
    private IEnumerator ReleaseAfterRealtime(AudioSource source, float delay)
	{
		yield return new WaitForSecondsRealtime(delay); // TimeScale 0이어도 기다림
		sfxPool.Release(source);
	}
	private IEnumerator FadeIn(AudioSource BgPlayer)
	{
		float ElapsedTime = 0f;
		float Duration = 0.8f;
		float volume = BgPlayer.volume;
		while(ElapsedTime < Duration)
		{
			ElapsedTime += Time.deltaTime;
			float t = ElapsedTime / Duration;
			BgPlayer.volume = Mathf.Lerp(volume, 0f, t);
			yield return null;
		}
	}
	private IEnumerator FadeIn(AudioSource BgPlayer, float v, float Time)
	{
		float ElapsedTime = 0f;
		float volume = BgPlayer.volume;
		while (ElapsedTime < Time)
		{
			ElapsedTime += UnityEngine.Time.unscaledDeltaTime;
			float t = ElapsedTime / Time;
			BgPlayer.volume = Mathf.Lerp(volume, v, t);
			yield return null;
		}
	}
	private IEnumerator FadeOut(AudioSource BgPlayer)
	{
		float ElapsedTime = 0f;
		float Duration = 0.8f;
		float volume = BgPlayer.volume;
		while (ElapsedTime < Duration)
		{
			ElapsedTime += Time.deltaTime;
			float t = ElapsedTime / Duration;
			BgPlayer.volume = Mathf.Lerp(volume, 1f, t);
			yield return null;
		}
	}
	private IEnumerator FadeOut(AudioSource BgPlayer, float ti)
	{
		float ElapsedTime = 0f;
		while (ElapsedTime < ti)
		{
			ElapsedTime += Time.deltaTime;
			float v = BgPlayer.volume;
			float t = ElapsedTime / ti;
			BgPlayer.volume = Mathf.Lerp(v, 1f, t);
			yield return null;
		}
	}
    #endregion
}
