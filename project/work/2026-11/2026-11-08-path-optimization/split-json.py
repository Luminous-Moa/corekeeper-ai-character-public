from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs');s=p.read_text(encoding='utf-8-sig')
a=s.index('    private static void CompletePreparation(');b=s.index('    private static void HandleResponse(',a)
q=s[a:b].replace('private static void CompletePreparation(', 'private static IEnumerable<int> CompletePreparationSteps(')
q=q.replace('            return;', '            yield break;').replace('        })) return;', '        })) yield break;')
q=q.replace('        string input = BuildInput(server, player, o, trigger);','        string input = $"관측 시점: 게임 시간 {o.time:F2}초, 경로·후보는 이 관측 위치 기준(준비 경과 {Time.realtimeSinceStartup - o.time:F2}초). 실행 시 실제 대상·경로를 다시 확인한다.\\n" + BuildInput(server, player, o, trigger);')
q=q.replace('        string json = BuildJson(input, cands);','        string json = null;\n        foreach (var part in BuildJsonSteps(input, cands, value => json = value)) yield return part;\n        if (!CostAllows(now) || !CallRateOk(now)) yield break;')
s=s[:a]+q+s[b:]
a=s.index('    private static string BuildJson(');b=s.index('    private static string Q(',a)
q=s[a:b]
q=q.replace('private static string BuildJson(string input, List<AICandidate> cands)', 'private static IEnumerable<int> BuildJsonSteps(string input, List<AICandidate> cands, Action<string> complete)')
q=q.replace(".Append('}');", ".Append('}');\n            yield return 0;")
q=q.replace('        return sb.ToString();', '        complete(sb.ToString());')
wrapper='''    private static string BuildJson(string input, List<AICandidate> cands)
    {
        string result = null;
        foreach (var step in BuildJsonSteps(input, cands, value => result = value)) { }
        return result;
    }

'''
s=s[:a]+wrapper+q+s[b:]
p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('job.Steps = BuildCandidateSteps(server, player, job.Observation, job.Prefetch, breach, value => job.Candidates = value).GetEnumerator();','job.Steps = PreparationSteps(job, breach).GetEnumerator();')
s=s.replace('                CompletePreparation(server, player, now, job);\n','')
pos=s.index('    // The expensive candidate loops')
s=s[:pos]+'''    private static IEnumerable<int> PreparationSteps(Preparation job, bool breach)
    {
        foreach (var step in BuildCandidateSteps(job.World, job.Player, job.Observation, job.Prefetch, breach, value => job.Candidates = value)) yield return step;
        foreach (var step in CompletePreparationSteps(job.World, job.Player, Time.realtimeSinceStartup, job)) yield return step;
    }

'''+s[pos:]
p.write_text(s,encoding='utf-8')
