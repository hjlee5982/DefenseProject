using System;
using System.Collections.Generic;
using System.Text;

// Fast stage-by-stage balance simulator (stand-alone approximation of the game's combat rules).
public static class BalanceSim
{
    // ---------- geometry ----------
    public static double SpawnY = 5.65, BarrierTop = -1.385, PlayerX = 0, PlayerY = -2.39, MinLineY = 1.5;
    public static double SpawnXRange = 1.5;
    public static double SpawnInterval = 0.6;

    // ---------- monster table ----------
    // 0 Slime 1 Crab 2 Spider 3 Bat 4 Skeleton 5 Demon 6 Ghost
    public static string[] Names = { "Slime", "Crab", "Spider", "Bat", "Skeleton", "Demon", "Ghost" };
    public static double[] Speed = { 1.0, 1.2, 1.5, 1.3, 1.1, 1.4, 2.0 };
    public static double[] HpMul = { 1.0, 1.5, 0.8, 0.7, 1.8, 1.3, 0.6 };
    public static double[] AtkMul = { 1.0, 1.5, 1.0, 1.0, 2.0, 2.0, 1.0 };
    public static double[] Weight = { 4, 3, 3, 3, 2, 1.5, 0 };
    public static bool[] SelfDestruct = { true, false, true, false, false, false, true };
    public static int[] UnlockStage = { 1, 21, 41, 56, 71, 81, 999 }; // first stage each type can appear

    // ---------- tunables (set from outside) ----------
    public static double CountA = 0.8, CountB = 1.2, CountC = 1.8;
    public static int MinCount = 6;
    public static double HpBase = 8, HpLin = 0.25, HpPow = 1.0, HpExp = 0.0;       // hp(s) = HpBase * (1 + HpLin*s)^HpPow   (times type multiplier)
    public static double AtkBase = 1, AtkPerStage = 0.04;             // atk(s) = round(AtkBase + AtkPerStage*s) * type multiplier
    public static double ExpPerKill = 17;
    public static double BarrierOverride = 0; public static double Eta = 1.0;                                   // player effectiveness knob (calibrate against the real game)
    public static double PickRate = 2.0, MergeEff = 0.55;
    public static double WeaponCells = 5.0, WeaponCellShare = 0.7;
    public static double EnhanceGainPerLevel3 = 0.022, EnhanceGainPerLevel5 = 0.026;

    // ---------- player model ----------
    static readonly double[] WRange = { 3.98, 3.9, 3.0, 4.1, 3.7, 3.35 };
    static readonly double[] WDmg = { 11.28, 21.67, 15.13, 18.84, 27.62, 44.16 };
    static readonly double[] WInt = { 0.85, 2.04, 1.4, 1.67, 2.58, 2.72 };
    static readonly double[] WSpd = { 8, 3, 10, 6, 13, 6 };
    // preference: which weapon kinds a player would typically keep (index into above)
    static readonly int[] WeaponPriority = { 5, 0, 3, 2, 4, 1 };

    public static int[] LevelExp = BuildLevelCurve();
    static int[] BuildLevelCurve()
    {
        var a = new int[200];
        for (int i = 0; i < a.Length; i++) a[i] = 460 + 60 * Math.Min(i, 79);
        return a;
    }

    public static int CountFor(int stage)
    {
        double slope = stage <= 40 ? CountA : (stage <= 70 ? CountB : CountC);
        return Math.Max(MinCount, (int)Math.Round(slope * stage));
    }

    public static double HpAt(int stage) { return HpBase * Math.Pow(1.0 + HpLin * stage, HpPow) * Math.Exp(HpExp * stage); }
    public static int AtkAt(int stage) { return Math.Max(1, (int)Math.Round(AtkBase + AtkPerStage * stage)); }

    class Weapon { public double range, dmg, interval, speed, timer; public int grade; public int kind; }

    public class StageLog { public int stage, count, kills, leaks; public double hpTotal, dps, barrierLeft, grade, weapons, level; public double dmgTaken; }

    class Mon { public double x, y, hp, reserved, speed; public int atk, type; public bool atBarrier, dead; public double atkTimer, slowUntil, stunUntil; public double shotTimer; }
    class Ev { public double t; public Mon target; public double dmg; public int kind; public int barrierAtk; public Weapon w; }

    // Build the loadout of a "competent player" at the start of a stage.
    public static string LastTokens = ""; public static double LastAttackPct, LastSpeedPct;
    static readonly string[] KindToken = { "Water", "Ice", "Rock", "Grimoire", "Wind", "Fire" };

    static List<Weapon> BuildLoadout(int stage, bool title, double levelGainMult, out double avgGrade)
    {
        double cells = Math.Min(64, 24 + 6 * ((stage - 1) / 4));
        int cap = (int)Math.Floor(cells * WeaponCellShare / WeaponCells);
        double items = PickRate * stage;
        int n = (int)Math.Max(1, Math.Min(cap, Math.Floor(items)));
        double eff = Math.Max(1.0, items * MergeEff / n);
        double g = 1.0 + Math.Log(eff, 2);
        if (title) g += 0.8; // productivity: better grades in the shop
        g = Math.Max(1.0, Math.Min(5.0, g));
        avgGrade = g;

        var list = new List<Weapon>();
        var tokens = new List<string>();
        for (int i = 0; i < n; i++)
        {
            int kind = WeaponPriority[i % WeaponPriority.Length];
            // distribute grades: floor/ceil mix so the mean equals g
            int lo = (int)Math.Floor(g);
            double frac = g - lo;
            int grade = (i / (double)n) < frac ? Math.Min(5, lo + 1) : lo;
            double dmgMul = Math.Pow(1.22, grade - 1);
            double intMul = 1.0 / Math.Pow(1.04, grade - 1);
            double rngMul = Math.Pow(1.1, grade - 1);
            var w = new Weapon { kind = kind, grade = grade, range = WRange[kind] * rngMul, dmg = WDmg[kind] * dmgMul, interval = WInt[kind] * intMul, speed = WSpd[kind] };
            list.Add(w);
            tokens.Add(KindToken[kind] + ":" + grade);
        }

        // buffs / enhance / title multipliers
        double dmgBuff = 1.0 + Math.Min(0.25, 0.01 * stage); // scrolls etc.
        double speedBuff = 1.0 + Math.Min(0.25, 0.01 * stage);
        double enh = levelGainMult;
        double magic = title ? 2.0 : 1.0; // title "magic": +100% damage and +100% speed
        LastTokens = string.Join(",", tokens);
        LastAttackPct = (dmgBuff * enh - 1.0) * 100.0;
        LastSpeedPct = (speedBuff * enh - 1.0) * 100.0;
        foreach (var w in list)
        {
            w.dmg *= dmgBuff * enh * magic * Eta;
            w.interval /= speedBuff * enh * magic;
            w.interval = Math.Max(0.05, w.interval);
        }
        return list;
    }

    // Returns the stage a run fails at (101 = survives everything).
    public static int RunGame(bool title, Random rng, List<StageLog> log)
    {
        double barrierMax = BarrierOverride > 0 ? BarrierOverride : 100.0 * (title ? 2.0 : 1.0);
        double barrier = barrierMax;
        double expTotal = 0;
        int level = 1;
        double expNeeded = LevelExp[0], expInLevel = 0;
        double gainPerLevel = title ? EnhanceGainPerLevel5 : EnhanceGainPerLevel3;
        double expMult = title ? 1.5 : 1.0;

        for (int stage = 1; stage <= 100; stage++)
        {
            double levelMult = Math.Pow(1.0 + gainPerLevel, level - 1);
            double avgGrade;
            var weapons = BuildLoadout(stage, title, levelMult, out avgGrade);
            var rec = new StageLog { stage = stage, grade = avgGrade, weapons = weapons.Count, level = level };
            double dps = 0; foreach (var w in weapons) dps += w.dmg / w.interval; rec.dps = dps;

            int kills; int leaks; double taken;
            SimulateStage(stage, weapons, ref barrier, rng, out kills, out leaks, out taken, out rec.count, out rec.hpTotal);
            rec.kills = kills; rec.leaks = leaks; rec.dmgTaken = taken; rec.barrierLeft = barrier;
            if (log != null) log.Add(rec);
            if (barrier <= 0) return stage;

            expInLevel += kills * ExpPerKill * expMult;
            while (expInLevel >= LevelExp[Math.Min(level - 1, LevelExp.Length - 1)])
            {
                expInLevel -= LevelExp[Math.Min(level - 1, LevelExp.Length - 1)];
                level++;
            }
        }
        return 101;
    }

    static int PickType(int stage, Random rng)
    {
        double total = 0;
        for (int i = 0; i < Names.Length; i++) if (stage >= UnlockStage[i]) total += Weight[i];
        double r = rng.NextDouble() * total;
        for (int i = 0; i < Names.Length; i++)
        {
            if (stage < UnlockStage[i]) continue;
            r -= Weight[i];
            if (r <= 0) return i;
        }
        return 0;
    }

    static void SimulateStage(int stage, List<Weapon> weapons, ref double barrier, Random rng, out int kills, out int leaks, out double taken, out int count, out double hpTotal)
    {
        count = CountFor(stage);
        kills = 0; leaks = 0; taken = 0; hpTotal = 0;
        double dt = 0.05, t = 0;
        double speedBonus = 1.0 + stage / 100.0;
        var mons = new List<Mon>();
        var events = new List<Ev>();
        int spawned = 0; double spawnTimer = 0;
        foreach (var w in weapons) w.timer = rng.NextDouble() * w.interval;
        double maxRange = 0; foreach (var w in weapons) if (w.range > maxRange) maxRange = w.range;
        double hp0 = HpAt(stage); int atk0 = AtkAt(stage);

        while (t < 900)
        {
            t += dt;
            spawnTimer += dt;
            while (spawned < count && spawnTimer >= SpawnInterval)
            {
                spawnTimer -= SpawnInterval;
                int ty = PickType(stage, rng);
                var m = new Mon { type = ty, x = (rng.NextDouble() * 2 - 1) * SpawnXRange, y = SpawnY };
                m.hp = Math.Max(1, Math.Round(hp0 * HpMul[ty])); m.atk = Math.Max(1, (int)Math.Round(atk0 * AtkMul[ty]));
                m.speed = Speed[ty] * speedBonus;
                hpTotal += m.hp;
                mons.Add(m); spawned++;
            }

            // events (projectile arrivals)
            for (int i = events.Count - 1; i >= 0; i--)
            {
                var e = events[i];
                if (e.t > t) continue;
                events.RemoveAt(i);
                if (e.kind == 1) { barrier -= e.barrierAtk; taken += e.barrierAtk; if (barrier <= 0) return; continue; } // demon fireball
                var tg = e.target;
                if (tg == null || tg.dead) continue;
                tg.reserved = Math.Max(0, tg.reserved - e.dmg);
                Damage(tg, e.dmg, e.w, t, mons, ref kills);
                // extra projectile from grade 4/5 weapons
                if (e.w.grade >= 4)
                {
                    Mon other = null; double best = 1.2;
                    foreach (var m2 in mons)
                    {
                        if (m2.dead || m2 == tg) continue;
                        double d = Math.Sqrt((m2.x - tg.x) * (m2.x - tg.x) + (m2.y - tg.y) * (m2.y - tg.y));
                        if (d < best) { best = d; other = m2; }
                    }
                    if (other != null) Damage(other, e.dmg * (e.w.grade >= 5 ? 0.7 : 0.5), e.w, t, mons, ref kills);
                }
            }

            // monster movement / attacks
            bool anyAlive = false;
            for (int i = 0; i < mons.Count; i++)
            {
                var m = mons[i];
                if (m.dead) continue;
                anyAlive = true;
                if (!m.atBarrier)
                {
                    if (m.type == 5) // ranged: stop once inside the player's best range
                    {
                        double dist = Math.Sqrt((m.x - PlayerX) * (m.x - PlayerX) + (m.y - PlayerY) * (m.y - PlayerY));
                        if (m.y <= MinLineY && dist <= maxRange) { m.atBarrier = true; m.shotTimer = 0.2; }
                        else if (m.y - BarrierTop <= 0.1) { m.atBarrier = true; m.shotTimer = 0.2; }
                    }
                    if (!m.atBarrier)
                    {
                        double f = 1.0;
                        if (t < m.stunUntil) f = 0.0; else if (t < m.slowUntil) f = 0.6;
                        m.y -= m.speed * f * dt;
                        if (m.y <= BarrierTop + 0.15) { m.atBarrier = true; m.y = BarrierTop + 0.15; }
                    }
                }
                if (m.atBarrier)
                {
                    if (SelfDestruct[m.type])
                    {
                        barrier -= m.atk; taken += m.atk; leaks++; m.dead = true;
                        if (barrier <= 0) return;
                    }
                    else if (m.type == 5)
                    {
                        m.shotTimer -= dt;
                        if (m.shotTimer <= 0) { m.shotTimer = 1.32; events.Add(new Ev { t = t + 0.65, kind = 1, barrierAtk = m.atk }); }
                    }
                    else
                    {
                        m.atkTimer += dt;
                        if (m.atkTimer >= 0.87) { m.atkTimer = 0; barrier -= m.atk; taken += m.atk; if (barrier <= 0) return; }
                    }
                }
            }

            // weapons fire
            foreach (var w in weapons)
            {
                w.timer -= dt;
                if (w.timer > 0) continue;
                Mon tg = null; double bestD = w.kind == 4 ? -1 : 1e9;
                foreach (var m in mons)
                {
                    if (m.dead || m.y > MinLineY || m.hp - m.reserved <= 0) continue;
                    double d = Math.Sqrt((m.x - PlayerX) * (m.x - PlayerX) + (m.y - PlayerY) * (m.y - PlayerY));
                    if (d > w.range) continue;
                    if (w.kind == 4) { if (d > bestD) { bestD = d; tg = m; } }
                    else if (d < bestD) { bestD = d; tg = m; }
                }
                if (tg == null) { w.timer = 0.05; continue; }
                w.timer = w.interval;
                tg.reserved += w.dmg;
                events.Add(new Ev { t = t + bestD / w.speed, target = tg, dmg = w.dmg, kind = 0, w = w });
            }

            if (spawned >= count && !anyAlive && events.Count == 0) break;
            if (spawned >= count && !anyAlive) break;
        }
        // leak count for mobs still alive is irrelevant after loop
    }

    static void Damage(Mon m, double dmg, Weapon w, double t, List<Mon> mons, ref int kills)
    {
        if (m.dead) return;
        m.hp -= dmg;
        if (w.kind == 1) m.slowUntil = t + 1.0;      // ice slows
        if (w.kind == 2) m.stunUntil = Math.Max(m.stunUntil, t + 0.35); // rock roots
        if (m.hp <= 0) { m.dead = true; kills++; }
    }

    public static string StageTest(int stage, bool title, int level, int seeds)
    {
        double gain = title ? EnhanceGainPerLevel5 : EnhanceGainPerLevel3;
        double avgGrade;
        double sumTaken = 0, sumLeaks = 0, sumKills = 0; int cnt = 0; double hp = 0;
        for (int i = 0; i < seeds; i++)
        {
            var weapons = BuildLoadout(stage, title, Math.Pow(1.0 + gain, level - 1), out avgGrade);
            double barrier = 1e9; int kills, leaks; double taken;
            SimulateStage(stage, weapons, ref barrier, new Random(77 + i), out kills, out leaks, out taken, out cnt, out hp);
            sumTaken += taken; sumLeaks += leaks; sumKills += kills;
        }
        return string.Format("stage={0} count={1} totalHp={2:F0} | loadout={3} | atk+{4:F1}% spd+{5:F1}% | simTaken={6:F1} simLeaks={7:F1} simKills={8:F1}",
            stage, cnt, hp, LastTokens, LastAttackPct, LastSpeedPct, sumTaken / seeds, sumLeaks / seeds, sumKills / seeds);
    }

    public static string Report(bool title, int seeds)
    {
        var sb = new StringBuilder();
        int sum = 0; var fails = new List<int>();
        for (int s = 0; s < seeds; s++) { int f = RunGame(title, new Random(1000 + s), null); fails.Add(f); sum += f; }
        fails.Sort();
        sb.Append((title ? "FULL" : "NONE") + " fail stages: " + string.Join(",", fails) + " | median " + fails[fails.Count / 2]);
        return sb.ToString();
    }

    public static string Detail(bool title, int seed)
    {
        var log = new List<StageLog>();
        int f = RunGame(title, new Random(seed), log);
        var sb = new StringBuilder();
        sb.AppendLine((title ? "FULL" : "NONE") + " fail=" + f);
        sb.AppendLine("stage count  hpEach  dps    grade  W  lvl  kills leaks taken barrierLeft");
        foreach (var r in log)
            if (r.stage % 5 == 0 || r.stage == f || r.stage <= 3)
                sb.AppendLine(string.Format("{0,5} {1,5} {2,7:F0} {3,6:F0} {4,5:F1} {5,2} {6,4} {7,5} {8,5} {9,5:F0} {10,8:F0}", r.stage, r.count, r.hpTotal / Math.Max(1, r.count), r.dps, r.grade, (int)r.weapons, (int)r.level, r.kills, r.leaks, r.dmgTaken, r.barrierLeft));
        return sb.ToString();
    }
}
