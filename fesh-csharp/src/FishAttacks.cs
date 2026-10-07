namespace Fesh;

partial class Game
{
    // During the telegraph, freeze the ordinary reel minigame: ducking must never force a line-loss.
    // Release for the last 0.2 seconds to dodge; a held reel takes damage and a small progress penalty.
    bool UpdateFishAttack(ReelState r, float dt, bool hold)
    {
        int damage = Data.FishById.GetValueOrDefault(r.Roll.Id)?.Attack ?? 0;
        if (damage == 0) return false;
        if (r.AttackWarning <= 0)
        {
            r.AttackTimer -= dt;
            if (r.AttackTimer > 0 || r.Leap > 0 || r.Running > 0) return false;
            r.AttackWarning = 1.25f; r.DuckTime = 0;
            Sfx.Play("splash");
            Floater("Watch out!", player.X, player.Y - 29, "#ff9a8a");
        }
        r.AttackWarning = Math.Max(0, r.AttackWarning - dt);
        r.DuckTime = hold ? 0 : r.DuckTime + dt;
        SetPrompt("Fish attack! Release [<act>] / the mouse to duck until the warning passes!", null, true);
        if (r.AttackWarning > 0) return true;
        r.AttackTimer = Rand(3.5f, 5f);
        if (r.DuckTime >= .2f)
        {
            Floater("Dodged!", player.X, player.Y - 24, "#7fd36b"); Sfx.Play("coin");
        }
        else
        {
            // No knockback or monster-specific line cancellation. The fish remains hooked unless you faint.
            float taken = damage * (Has("shell_armor") > 0 ? .65f : 1);
            state.hp = Math.Max(0, state.hp - taken);
            lastHitT = time; hurtFlash = .35f; iframes = .9f;
            // The hit costs progress but never loses the fish by itself, and never adds any either.
            r.Progress = Math.Min(r.Progress, Math.Max(.05f, r.Progress - .08f));
            Sfx.Play("hurt"); Floater($"-{taken:0} health", player.X, player.Y - 24, "#ff9a8a");
            if (state.hp <= 0) Faint();
        }
        return true;
    }
}
