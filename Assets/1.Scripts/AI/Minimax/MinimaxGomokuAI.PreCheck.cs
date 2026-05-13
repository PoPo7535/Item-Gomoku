using System.Collections.Generic;
using ThreatAnalysis = MinimaxThreatAnalysis;

/// <summary>
/// Minimax 진입 전 확정 전술 후보를 검사하는 partial 영역임.
/// </summary>
public partial class MinimaxGomokuAI
{
    /// <summary>
    /// Minimax 탐색 전에 확정성이 높은 후보를 기존 우선순서대로 찾음.
    /// </summary>
    /// <param name="fullCandidates">pre-check에 사용할 비제한 루트 후보 목록.</param>
    /// <param name="move">선택된 pre-minimax 후보.</param>
    /// <returns>minimax 전에 바로 반환할 후보가 있으면 true.</returns>
    private bool TryFindPreMinimaxMove(List<GomokuMove> fullCandidates, out GomokuMove move)
    {
        // 오프닝 -> 즉시 승리 -> 즉시 방어 -> AI 열린 4 공격 -> 상대 직접 위협 방어 -> AI 복합 위협 순서임.
        move = FindOpeningMove();
        if (move.IsValid)
        {
            return true;
        }

        move = FindImmediateMove(fullCandidates, _aiColor, "Immediate win");
        if (move.IsValid)
        {
            LogAiDebug($"Immediate win selected {FormatMove(move)}");
            return true;
        }

        move = FindImmediateMove(fullCandidates, _opponentColor, "Immediate defense");
        if (move.IsValid)
        {
            LogAiDebug($"Immediate defense selected {FormatMove(move)}");
            return true;
        }

        move = FindOpenFourAttackMove(fullCandidates);
        if (move.IsValid)
        {
            LogAiDebug($"Open four attack selected {FormatMove(move)}");
            return true;
        }

        move = FindThreatDefenseMove(fullCandidates);
        if (move.IsValid)
        {
            // 난이도와 무관한 직접 위협은 minimax 전에 공통으로 차단함.
            return true;
        }

        move = FindAiCompoundThreatMove(fullCandidates);
        if (move.IsValid)
        {
            LogAiDebug($"Compound threat selected {FormatMove(move)}");
            return true;
        }

        move = GomokuMove.Invalid("Pre-minimax move not found");
        return false;
    }

    /// <summary>
    /// 즉시 승리 또는 즉시 방어가 가능한 수를 찾음.
    /// </summary>
    private GomokuMove FindImmediateMove(List<GomokuMove> candidates, StoneColor color, string reason)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove candidate = candidates[i];

            if (!IsLegalMove(candidate.X, candidate.Y, color))
            {
                continue;
            }

            bool isWin;

            // 한 수로 승리 가능한 좌표는 minimax보다 우선함.
            PlaceTemporary(candidate.X, candidate.Y, color);
            try
            {
                isWin = _logic.CheckWin(candidate.X, candidate.Y, color);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            if (isWin)
            {
                return new GomokuMove(candidate.X, candidate.Y, color == _aiColor ? WinScore : -WinScore, reason);
            }
        }

        return GomokuMove.Invalid(reason + " not found");
    }

    /// <summary>
    /// AI가 열린 4를 만들 수 있는 공격 후보를 찾음.
    /// </summary>
    /// <param name="candidates">검사할 루트 후보 목록.</param>
    /// <returns>열린 4를 만드는 최선 공격 후보.</returns>
    private GomokuMove FindOpenFourAttackMove(List<GomokuMove> candidates)
    {
        GomokuMove bestAttack = GomokuMove.Invalid("Open four attack not found");
        int bestOpenFourCount = 0;
        int bestThreatScore = int.MinValue;
        int bestCandidateScore = int.MinValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove candidate = candidates[i];

            if (!IsLegalMove(candidate.X, candidate.Y, _aiColor))
            {
                continue;
            }

            ThreatAnalysis threatAnalysis;

            // AI 열린 4 공격권은 상대 직접 위협 방어보다 먼저 확보함.
            PlaceTemporary(candidate.X, candidate.Y, _aiColor);
            try
            {
                threatAnalysis = AnalyzeThreatAt(candidate.X, candidate.Y, _aiColor);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            if (threatAnalysis.OpenFourCount <= 0)
            {
                continue;
            }

            if (!bestAttack.IsValid ||
                threatAnalysis.OpenFourCount > bestOpenFourCount ||
                (threatAnalysis.OpenFourCount == bestOpenFourCount && threatAnalysis.Score > bestThreatScore) ||
                (threatAnalysis.OpenFourCount == bestOpenFourCount && threatAnalysis.Score == bestThreatScore && candidate.Score > bestCandidateScore))
            {
                bestOpenFourCount = threatAnalysis.OpenFourCount;
                bestThreatScore = threatAnalysis.Score;
                bestCandidateScore = candidate.Score;
                bestAttack = new GomokuMove(candidate.X, candidate.Y, threatAnalysis.Score, "Open four attack");
            }
        }

        return bestAttack;
    }

    /// <summary>
    /// 상대의 직접 위협을 난이도와 무관하게 막을 방어 수를 찾음.
    /// </summary>
    private GomokuMove FindThreatDefenseMove(List<GomokuMove> candidates)
    {
        GomokuMove bestDefense = GomokuMove.Invalid("Threat defense not found");
        int bestThreatPriority = 0;
        int bestDefenseScore = int.MinValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove candidate = candidates[i];

            if (!IsLegalMove(candidate.X, candidate.Y, _opponentColor))
            {
                continue;
            }

            ThreatAnalysis threatAnalysis;

            // 상대가 해당 좌표에 뒀을 때 바로 생기는 위협만 공통 방어 대상으로 봄.
            PlaceTemporary(candidate.X, candidate.Y, _opponentColor);
            try
            {
                threatAnalysis = AnalyzeThreatAt(candidate.X, candidate.Y, _opponentColor);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            int threatPriority = GetThreatPriority(threatAnalysis);
            if (threatPriority <= 0)
            {
                continue;
            }

            int defenseScore = threatAnalysis.Score;
            LogAiDebug(
                $"ThreatDefense candidate=({candidate.X},{candidate.Y}) score={threatAnalysis.Score} " +
                $"open3={threatAnalysis.OpenThreeCount} broken3={threatAnalysis.BrokenThreeCount} " +
                $"blocked4={threatAnalysis.BlockedFourCount} gapped4={threatAnalysis.GappedFourCount} open4={threatAnalysis.OpenFourCount} " +
                $"tier={threatPriority}");

            if (!bestDefense.IsValid ||
                threatPriority > bestThreatPriority ||
                (threatPriority == bestThreatPriority && defenseScore > bestDefenseScore))
            {
                bestThreatPriority = threatPriority;
                bestDefenseScore = defenseScore;
                bestDefense = new GomokuMove(candidate.X, candidate.Y, defenseScore, GetThreatDefenseReason(threatAnalysis));
            }
        }

        return bestDefense;
    }

    /// <summary>
    /// AI가 한 수로 복합 공격 위협을 만들 수 있는 후보를 찾음.
    /// </summary>
    /// <param name="candidates">검사할 루트 후보 목록.</param>
    /// <returns>복합 공격 위협을 만드는 최선 후보.</returns>
    private GomokuMove FindAiCompoundThreatMove(List<GomokuMove> candidates)
    {
        GomokuMove bestThreat = GomokuMove.Invalid("Compound threat not found");
        int bestPriority = 0;
        int bestFourThreatCount = 0;
        int bestOpenThreeCount = 0;
        int bestThreatScore = int.MinValue;
        int bestCandidateScore = int.MinValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove candidate = candidates[i];

            // 흑돌 금수 후보는 복합 위협처럼 보여도 선택하면 안 됨.
            if (!IsLegalMove(candidate.X, candidate.Y, _aiColor))
            {
                continue;
            }

            ThreatAnalysis threatAnalysis;

            // 합법 후보만 가상 착수해 복합 위협 여부를 확인함.
            PlaceTemporary(candidate.X, candidate.Y, _aiColor);
            try
            {
                threatAnalysis = AnalyzeThreatAt(candidate.X, candidate.Y, _aiColor);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            int priority = GetCompoundThreatPriority(threatAnalysis);
            if (priority <= 0)
            {
                continue;
            }

            int fourThreatCount = GetCompoundFourThreatDirectionCount(threatAnalysis);
            int openThreeDirectionCount = CountDirectionBits(threatAnalysis.OpenThreeDirectionMask);
            if (!bestThreat.IsValid ||
                priority > bestPriority ||
                (priority == bestPriority && fourThreatCount > bestFourThreatCount) ||
                (priority == bestPriority && fourThreatCount == bestFourThreatCount && openThreeDirectionCount > bestOpenThreeCount) ||
                (priority == bestPriority && fourThreatCount == bestFourThreatCount && openThreeDirectionCount == bestOpenThreeCount && threatAnalysis.Score > bestThreatScore) ||
                (priority == bestPriority && fourThreatCount == bestFourThreatCount && openThreeDirectionCount == bestOpenThreeCount && threatAnalysis.Score == bestThreatScore && candidate.Score > bestCandidateScore))
            {
                bestPriority = priority;
                bestFourThreatCount = fourThreatCount;
                bestOpenThreeCount = openThreeDirectionCount;
                bestThreatScore = threatAnalysis.Score;
                bestCandidateScore = candidate.Score;
                bestThreat = new GomokuMove(candidate.X, candidate.Y, threatAnalysis.Score, GetCompoundThreatReason(threatAnalysis));
            }
        }

        return bestThreat;
    }

    /// <summary>
    /// AI 복합 위협 분석 결과를 공격 우선순위 등급으로 변환함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <returns>클수록 먼저 선택해야 하는 복합 공격 등급.</returns>
    private int GetCompoundThreatPriority(ThreatAnalysis analysis)
    {
        int fourThreatDirectionCount = GetCompoundFourThreatDirectionCount(analysis);

        if (fourThreatDirectionCount >= 2)
        {
            return 4;
        }

        if (HasDistinctThreatDirections(analysis.BlockedFourDirectionMask, analysis.OpenThreeDirectionMask))
        {
            return 3;
        }

        if (HasDistinctThreatDirections(analysis.GappedFourDirectionMask, analysis.OpenThreeDirectionMask))
        {
            return 2;
        }

        if (CountDirectionBits(analysis.OpenThreeDirectionMask) >= 2)
        {
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// AI 복합 위협 분석 결과에 맞는 선택 사유 문자열을 반환함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <returns>복합 공격 사유 문자열.</returns>
    private string GetCompoundThreatReason(ThreatAnalysis analysis)
    {
        int fourThreatDirectionCount = GetCompoundFourThreatDirectionCount(analysis);

        if (fourThreatDirectionCount >= 2)
        {
            return "Two fours compound attack";
        }

        if (HasDistinctThreatDirections(analysis.BlockedFourDirectionMask, analysis.OpenThreeDirectionMask))
        {
            return "Blocked four open three compound attack";
        }

        if (HasDistinctThreatDirections(analysis.GappedFourDirectionMask, analysis.OpenThreeDirectionMask))
        {
            return "Gapped four open three compound attack";
        }

        if (CountDirectionBits(analysis.OpenThreeDirectionMask) >= 2)
        {
            return "Double open three compound attack";
        }

        return "Compound threat attack";
    }

    /// <summary>
    /// 복합 공격 판정에 사용할 four 계열 위협 방향 수를 반환함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <returns>막힌 4와 끊어진 4가 존재하는 서로 다른 방향 수.</returns>
    private int GetCompoundFourThreatDirectionCount(ThreatAnalysis analysis)
    {
        return CountDirectionBits(analysis.BlockedFourDirectionMask | analysis.GappedFourDirectionMask);
    }

    /// <summary>
    /// 두 위협 마스크가 서로 다른 방향의 위협을 포함하는지 확인함.
    /// </summary>
    /// <param name="firstMask">첫 번째 위협 방향 마스크.</param>
    /// <param name="secondMask">두 번째 위협 방향 마스크.</param>
    /// <returns>서로 다른 방향에 위협이 존재하면 true.</returns>
    private bool HasDistinctThreatDirections(int firstMask, int secondMask)
    {
        return firstMask != 0 &&
               secondMask != 0 &&
               CountDirectionBits(firstMask | secondMask) >= 2;
    }

    /// <summary>
    /// 방향 마스크에 켜진 bit 수를 계산함.
    /// </summary>
    /// <param name="directionMask">방향 bit 마스크.</param>
    /// <returns>켜진 방향 bit 수.</returns>
    private int CountDirectionBits(int directionMask)
    {
        int count = 0;

        while (directionMask != 0)
        {
            count += directionMask & 1;
            directionMask >>= 1;
        }

        return count;
    }

    /// <summary>
    /// 위협 분석 결과에 맞는 방어 사유 문자열을 반환함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <returns>방어 사유 문자열.</returns>
    private string GetThreatDefenseReason(ThreatAnalysis analysis)
    {
        if (analysis.OpenFourCount > 0)
        {
            return "Open four defense";
        }

        if (analysis.BlockedFourCount > 0)
        {
            return "Blocked four defense";
        }

        if (analysis.GappedFourCount > 0)
        {
            return "Gapped four defense";
        }

        return "Threat defense";
    }

    /// <summary>
    /// 현재 위협 분석 결과를 방어 우선순위 등급으로 변환함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <returns>클수록 먼저 차단해야 하는 현재 위협 등급.</returns>
    private int GetThreatPriority(ThreatAnalysis analysis)
    {
        if (analysis.OpenFourCount > 0)
        {
            return 4;
        }

        // 상대 열린 4가 없을 때만 four + 열린 3 복합 위협을 우선 차단함.
        if ((analysis.BlockedFourCount > 0 || analysis.GappedFourCount > 0) && analysis.OpenThreeCount > 0)
        {
            return 3;
        }

        if (analysis.BlockedFourCount > 0 || analysis.GappedFourCount > 0)
        {
            return 2;
        }

        // 순수 열린 3은 강제 방어가 아니라 Minimax/평가 경쟁에 맡김.
        return 0;
    }
}
