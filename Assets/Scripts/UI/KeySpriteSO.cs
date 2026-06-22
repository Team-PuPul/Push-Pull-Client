using System.Collections.Generic;
using UnityEngine;

// 키(Input System 컨트롤 이름) → Sprite 매핑 테이블.
// 리바인딩된 키를 아이콘으로 표시할 때 사용한다.
[CreateAssetMenu(fileName = "KeySpriteSO", menuName = "UI/Key Sprite SO")]
public class KeySpriteSO : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        [Tooltip("Input System 컨트롤 이름 (예: a, space, leftArrow). 대소문자 무시")]
        public string key;
        public Sprite sprite;
    }

    [SerializeField] private List<Entry> keySprites = new List<Entry>();
    [SerializeField] private Sprite fallbackSprite;     // 매핑에 없는 키일 때 표시할 기본 스프라이트

    private Dictionary<string, Sprite> _lookup;

    // 컨트롤 이름으로 스프라이트를 찾는다. 없으면 fallbackSprite 반환.
    public Sprite GetSprite(string key)
    {
        if (string.IsNullOrEmpty(key)) return fallbackSprite;

        if (_lookup == null) BuildLookup();

        return _lookup.TryGetValue(key.ToLowerInvariant(), out Sprite sprite)
            ? sprite
            : fallbackSprite;
    }

    // 리스트를 대소문자 무시 딕셔너리로 캐싱
    private void BuildLookup()
    {
        _lookup = new Dictionary<string, Sprite>();
        foreach (Entry entry in keySprites)
        {
            if (string.IsNullOrEmpty(entry.key)) continue;
            _lookup[entry.key.ToLowerInvariant()] = entry.sprite;
        }
    }

    // 에디터에서 값이 바뀌면 캐시를 무효화하여 다음 조회 시 다시 빌드
    private void OnValidate() => _lookup = null;
}
