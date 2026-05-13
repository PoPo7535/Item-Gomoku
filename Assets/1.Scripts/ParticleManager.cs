using UnityEngine;
using System.Collections.Generic;


public enum StoneParticleType
{
    Normal,             // 일반 돌 착수
    HiddeMarker,       // 착수숨김
    FakeStone,          // 가짜 돌 
    TimeDecrease,       // 타이머 감소 
    DoubleMark,         // 더블 표시 
    SwapStone,          // 돌 바꾸기 
    TransparentStone,   // 투명 돌 
    Reveal // 간파하기 
}

[System.Serializable]
public struct ParticleInfo
{
    public StoneParticleType particleType;
    public ParticleSystem particlePrefab;
}

public class ParticleManager : MonoBehaviour
{
    
    public static ParticleManager I { get; private set; }

    [Header("파티클 리스트")]
    public List<ParticleInfo> particleList;

    // 검색할때쓸거
    private Dictionary<StoneParticleType, ParticleSystem> particleDict = new Dictionary<StoneParticleType, ParticleSystem>();

    private void Awake()
    {
        
        if (I == null)
        {
            I = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 리스트에 등록된 파티클들을 딕셔너리에 정리
        foreach (var info in particleList)
        {
            if (!particleDict.ContainsKey(info.particleType))
            {
                particleDict.Add(info.particleType, info.particlePrefab);
            }
        }
    }

    /// <summary>
    /// 원하는 타이밍에 파티클을 생성하고 실행함
    /// </summary>
    public void PlayParticle(StoneParticleType type, Vector3 position)
    {
        if (particleDict.TryGetValue(type, out ParticleSystem prefab))
        {
            // 지정된 위치에 파티클 프리팹 생성
            ParticleSystem newParticle = Instantiate(prefab, position, Quaternion.identity);
            
            // 파티클 실행
            newParticle.Play();

            // 파티클 재생이 끝나면 메모리에서 자동 삭제
            float duration = newParticle.main.duration + newParticle.main.startLifetime.constantMax;
            Destroy(newParticle.gameObject, duration);
            Debug.Log($"{type} 타입 실행댐");
        }
        else
        {
            Debug.LogWarning($"[ParticleManager] '{type}' 타입의 파티클이 등록되지 않았습니다!");
        }
    }
}