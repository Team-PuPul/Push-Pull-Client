using UnityEngine;
using UnityEngine.UI;

public class ExChargeUi : MonoBehaviour
{
	Canvas canvas;

	public InputPlayer Player;

	public Image PChargeGage;
	public Image PGage;

	public GameObject PGrabRange;

	[Header("SkillObjects")]
	public GameObject PushGlove;
	public GameObject PlayerGrab;

	private Vector3 scale;


	Vector3 offset = new Vector3(0, 120, 0);
	private void Awake()
	{
		canvas = GetComponent<Canvas>();
		canvas.worldCamera = Camera.main;
	}

	private void Start()
	{
		PGage.gameObject.SetActive(false);

		PGrabRange.SetActive(false);
	}

	private void Update()
	{
		PChargeGage.fillAmount = Player.PushCharge / 35f;
	}

	public void OnPush()
	{
            PGage.gameObject.SetActive(true);
            Vector3 scale = PGage.rectTransform.localScale;
            if (Player != null && Player.transform.localScale.x < 0f)
            {
                PGage.rectTransform.localScale = new Vector3(
                    -Mathf.Abs(scale.x),
                    scale.y,
                    scale.z
                );
            }
            else
            {
                PGage.rectTransform.localScale = new Vector3(
                    Mathf.Abs(scale.x),
                    scale.y,
                    scale.z
                );
            }
    }

	public void OffPush()
	{
        PGage.gameObject.SetActive(false);
    }

	public void OnGrab()
	{
            PGrabRange.SetActive(true);
    }

	public void OffGrab()
	{
        PGrabRange.SetActive(false);
    }
}
