using System.Collections.Generic;
using ThreatAnalysis = MinimaxThreatAnalysis;

/// <summary>
/// Minimax 후보 생성, 경량 점수 계산, 후보 정렬을 담당하는 partial 영역임.
/// </summary>
public partial class MinimaxGomokuAI
{
    /// <summary>
    /// 기존 돌 주변 반경 안에서 후보 수를 생성함.
    /// </summary>
    private List<GomokuMove> GenerateCandidates(StoneColor color, CandidateGenerationMode mode, bool limitCandidates)
    {
        List<GomokuMove> candidates = new List<GomokuMove>();
        bool hasStone = false;

        for (int x = 0; x < _boardSize; x++)
        {
            for (int y = 0; y < _boardSize; y++)
            {
                if (_logic.Board[x, y].Color != StoneColor.None)
                {
                    hasStone = true;
                    AddNearbyCandidates(candidates, x, y, color, mode);
                }
            }
        }

        if (!hasStone)
        {
            int center = _boardSize / 2;
            candidates.Add(new GomokuMove(center, center, 0, "Center fallback"));
        }

        SortCandidates(candidates, color);

        int candidateLimit = GetCandidateLimit(mode);
        if (limitCandidates && candidates.Count > candidateLimit)
        {
            candidates.RemoveRange(candidateLimit, candidates.Count - candidateLimit);
        }

        RecordGeneratedCandidates(mode, candidates.Count);
        return candidates;
    }

    /// <summary>
    /// 후보 생성 모드에 맞는 후보 상한을 반환함.
    /// </summary>
    /// <param name="mode">후보 생성 모드.</param>
    /// <returns>후보 목록에 유지할 최대 개수.</returns>
    private int GetCandidateLimit(CandidateGenerationMode mode)
    {
        switch (mode)
        {
            case CandidateGenerationMode.SearchNode:
                return SearchNodeCandidateCount;
            case CandidateGenerationMode.ThreatScan:
                return ThreatScanCandidateCount;
            default:
                return MaxCandidateCount;
        }
    }

    /// <summary>
    /// 특정 돌 주변의 빈 좌표를 후보 목록에 추가함.
    /// </summary>
    private void AddNearbyCandidates(List<GomokuMove> candidates, int originX, int originY, StoneColor color, CandidateGenerationMode mode)
    {
        for (int x = originX - CandidateRadius; x <= originX + CandidateRadius; x++)
        {
            ThrowIfCancellationRequested();
            for (int y = originY - CandidateRadius; y <= originY + CandidateRadius; y++)
            {
                if (!IsLegalMove(x, y, color) || ContainsMove(candidates, x, y))
                {
                    continue;
                }

                int score = EvaluateCandidateScore(x, y, color, mode);
                candidates.Add(new GomokuMove(x, y, score, "Nearby candidate"));
            }
        }
    }

    /// <summary>
    /// 후보 생성 모드에 맞는 후보 점수를 계산함.
    /// </summary>
    /// <param name="x">후보 X 좌표.</param>
    /// <param name="y">후보 Y 좌표.</param>
    /// <param name="color">후보 돌 색상.</param>
    /// <param name="mode">후보 생성 모드.</param>
    /// <returns>정렬에 사용할 후보 점수.</returns>
    private int EvaluateCandidateScore(int x, int y, StoneColor color, CandidateGenerationMode mode)
    {
        if (mode == CandidateGenerationMode.RootEvaluation)
        {
            // RootEvaluation은 루트 보드 기준 전체 평가라 탐색 1회 안에서만 캐싱 가능함.
            return EvaluateRootCandidateScore(x, y, color);
        }

        // SearchNode/ThreatScan은 반복 호출 비용을 줄이기 위해 경량 정렬 점수만 사용함.
        return EvaluateLightweightCandidateScore(x, y, color);
    }

    /// <summary>
    /// 루트 보드 기준 후보 평가를 탐색 1회 동안만 캐싱해 중복 전체 평가를 줄임.
    /// </summary>
    /// <param name="x">후보 X 좌표.</param>
    /// <param name="y">후보 Y 좌표.</param>
    /// <param name="color">후보 돌 색상.</param>
    /// <returns>루트 후보 평가 점수.</returns>
    private int EvaluateRootCandidateScore(int x, int y, StoneColor color)
    {
        int cacheKey = GetRootEvaluationCacheKey(x, y, color);
        if (_rootEvaluationCache.TryGetValue(cacheKey, out int cachedScore))
        {
            _stats.RootEvaluationCacheHitCount++;
            return cachedScore;
        }

        // 루트 보드는 동일 탐색 안에서만 고정되므로 EvaluateMove 결과를 안전하게 재사용함.
        _stats.EvaluateMoveCallCount++;
        int score = _evaluator.EvaluateMove(_logic, _boardSize, x, y, color, _aiColor);
        _rootEvaluationCache[cacheKey] = score;
        return score;
    }

    /// <summary>
    /// 루트 후보 평가 캐시에 사용할 좌표와 색상 기반 키를 생성함.
    /// </summary>
    /// <param name="x">후보 X 좌표.</param>
    /// <param name="y">후보 Y 좌표.</param>
    /// <param name="color">후보 돌 색상.</param>
    /// <returns>캐시 키.</returns>
    private int GetRootEvaluationCacheKey(int x, int y, StoneColor color)
    {
        return ((x * _boardSize) + y) * 8 + (int)color;
    }

    /// <summary>
    /// 전체 보드 평가 없이 지역 위협과 중앙 근접도만으로 후보 점수를 계산함.
    /// </summary>
    /// <param name="x">후보 X 좌표.</param>
    /// <param name="y">후보 Y 좌표.</param>
    /// <param name="color">후보 돌 색상.</param>
    /// <returns>정렬에 사용할 경량 후보 점수.</returns>
    private int EvaluateLightweightCandidateScore(int x, int y, StoneColor color)
    {
        _stats.LightweightEvaluationCallCount++;
        // lightweight score는 좋은 후보를 먼저 보게 하는 정렬 점수이며 leaf 평가값이 아님.
        ThreatAnalysis ownThreat = AnalyzeThreatAt(x, y, color);
        StoneColor opponentColor = GetOppositeColor(color);
        ThreatAnalysis opponentThreat = AnalyzeThreatAt(x, y, opponentColor);
        int center = _boardSize / 2;
        int centerDistance = System.Math.Abs(x - center) + System.Math.Abs(y - center);
        int centerBonus = _boardSize - centerDistance;
        int score = ownThreat.Score +
                    GetThreatOrderingBonus(ownThreat) +
                    opponentThreat.Score / 2 +
                    GetThreatOrderingBonus(opponentThreat) / DefenseOrderingBonusDivisor +
                    centerBonus;

        return color == _aiColor ? score : -score;
    }

    /// <summary>
    /// 후보 정렬용 위협 형태 보너스를 계산함.
    /// </summary>
    /// <param name="analysis">후보 좌표의 위협 분석 결과.</param>
    /// <returns>정렬 우선순위를 높일 보너스 점수.</returns>
    private int GetThreatOrderingBonus(ThreatAnalysis analysis)
    {
        int bonus = 0;

        if (analysis.OpenFourCount > 0)
        {
            bonus += OpenFourOrderingBonus;
        }

        if (analysis.BlockedFourCount > 0)
        {
            bonus += BlockedFourOrderingBonus;
        }

        if (analysis.GappedFourCount > 0)
        {
            bonus += GappedFourOrderingBonus * analysis.GappedFourCount;
        }

        if (analysis.OpenThreeCount > 0)
        {
            bonus += OpenThreeOrderingBonus * analysis.OpenThreeCount;
        }

        if (analysis.BrokenThreeCount > 0)
        {
            bonus += BrokenThreeOrderingBonus * analysis.BrokenThreeCount;
        }

        if (analysis.OpenTwoDirectionCount > 0)
        {
            // 열린 2는 공격 잠재력만 약하게 반영하고 pre-check로 승격하지 않음.
            bonus += OpenTwoOrderingBonus * analysis.OpenTwoDirectionCount;
        }

        if (analysis.OpenTwoDirectionCount >= 2)
        {
            // 서로 다른 방향의 열린 2는 단일 열린 2보다 조금 더 우대함.
            bonus += DoubleOpenTwoOrderingBonus;
        }

        if (analysis.BlockedFourCount > 0 && analysis.OpenThreeCount > 0)
        {
            // 복합 위협은 단일 열린 3보다 먼저 탐색되도록 추가 보정함.
            bonus += CompositeThreatOrderingBonus;
        }

        return bonus;
    }

    /// <summary>
    /// 후보 수를 평가 점수와 색상 역할 기준으로 정렬함.
    /// </summary>
    private void SortCandidates(List<GomokuMove> candidates, StoneColor color)
    {
        if (color == _opponentColor)
        {
            // 백돌 AI 평가 기준에서 흑돌 응수는 낮은 점수가 더 위협적인 수임.
            candidates.Sort((left, right) => left.Score.CompareTo(right.Score));
            return;
        }

        candidates.Sort((left, right) => right.Score.CompareTo(left.Score));
    }

    /// <summary>
    /// 후보 목록에 같은 좌표가 이미 있는지 확인함.
    /// </summary>
    private bool ContainsMove(List<GomokuMove> candidates, int x, int y)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].X == x && candidates[i].Y == y)
            {
                return true;
            }
        }

        return false;
    }
}
