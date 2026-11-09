// AIPressQueue — the body's one interact-with-object press (E) in flight (코어-안전지대 단계 2, 6-문-여닫기.md 4.3), moved
// out of AIBody so its rules are checked offline. The bit is set on every frame of one server tick and cleared from the
// next; one press per asked id; a new press is taken only when the last one is sent.
// 실측결함수정3 N5: a press waits for a player state that takes it (Release after a swing). 합치기 전 수정 C2
// (Review/fix3-review.md C2): a press not sent yet is dropped when the skill that asked it ends, a new skill starts, the
// AI is stopped or reset, or it waited WaitMax — before, it stayed and went out later at another time and place (a door
// opened behind a retreat, an E on the core after a respawn), or blocked every later press (a state that never takes one).
// The drop is logged and kept for the asker (DroppedWhy).
public sealed class PressQueue
{
    public int asked, done;      // the id asked, the id sent
    public uint tick;            // the server tick it is being sent on (0 = not yet)
    public float askedAt = -1f;  // when it was asked
    public int lastDropId; public string lastDropWhy = "";
    // 2.5 s: the Release wait of 실측결함수정3 N5 took 1.4 s (test N5a) and the door step's wait for the state change is
    // WaitOpen (1 s); an estimate, the longest state that takes no press is not measured
    public const float WaitMax = 2.5f;

    public bool Waiting => asked > done && tick == 0;

    // a new press (id = asked + 1, AIBody.NextUseId); taken only when none is in flight
    public bool Ask(int id, float now)
    {
        if (id <= asked || asked != done) return false;
        asked = id; tick = 0; askedAt = now;
        return true;
    }

    // the input sender, with the tick the input goes to: whether to set the bit on it (first: it starts now)
    public bool Bit(uint t, bool canInteract, out bool first)
    {
        first = false;
        if (asked <= done) return false;
        if (tick == 0 && !canInteract) return false;   // N5: not in a state the game takes it in (wait)
        if (tick == 0) { tick = t; first = true; }
        if (t == tick) return true;
        done = asked; tick = 0;   // the tick moved on: that press is sent
        return false;
    }

    // drops the press not sent yet; the log line, or null when there was none (a press being sent goes on)
    public string Drop(string why, float now)
    {
        if (!Waiting) return null;
        lastDropId = asked;
        lastDropWhy = $"누름 #{asked}을 보내지 못하고 버림({why}, {now - askedAt:F1}초 기다림)";
        done = asked;
        return $"BODY: interact-with-object press #{asked} dropped unsent: {why} (waited {now - askedAt:F1}s)";
    }

    // the wait's limit (state = what the body was in, for the fact)
    public string CheckWait(float now, string state)
        => Waiting && now - askedAt > WaitMax ? Drop($"{WaitMax:F1}초 동안 게임이 누름을 받는 상태가 아님: {state}", now) : null;

    public string DroppedWhy(int id) => id != 0 && id == lastDropId ? lastDropWhy : "";
}
