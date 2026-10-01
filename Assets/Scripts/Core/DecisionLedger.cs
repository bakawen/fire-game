using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 疏散决策记账（办公楼"活火场"专用）：每个动作带着当时的火场快照写入时间轴，
/// WinTrigger 结算时生成"时间轴归因 + 分段小结"；死亡验尸文案也从这里取材。
/// 仅办公楼场景有组件调用记账；宿舍/公寓不产生任何条目（BuildReview 返回空串，不受影响）。
/// 90秒火场版：新增规则牌组合行、设施联动（卷帘/喷淋）、事件（背燃门/手机诱饵/照明故障）、
/// 结构失效死线余量等记账；时间轴展示压缩到 6 行。
/// </summary>
public static class DecisionLedger
{
    private class Entry
    {
        public float t;
        public string text;
        public bool good;
        public string Context;
        public Entry(float time, string txt, bool isGood) { t = time; text = txt; good = isGood; }
    }

    private static readonly List<Entry> timeline = new List<Entry>();

    private static bool alarmDone;
    private static float alarmTime = -1f;
    private static bool powerCutDone;
    private static bool quizGatePassed;
    private static bool extinguisherPicked;
    private static float bigFireSpraySeconds;
    private static bool ineffectiveHintShown;
    private static bool routeShortcut;
    private static bool routeRecorded;
    private static bool spraySuppressed;       // 灭火器压制过起源火
    private static bool originRooted;          // 断电根除（不再复燃）
    private static bool originReignited;        // 未断电→复燃
    private static bool zoneCriticalRecorded;   // 有区域接近轰燃
    private static bool doorOpenedRecorded;     // 首次推门（时间轴只记一次）
    private static int turnbackCount;           // 中途折返次数（改选另一条路线）
    private static bool blockedExitTried;       // 撞上被堵死的"安全出口"
    private static bool distressHeard;          // 听到门后呼救
    private static bool rescued;                // 破门救出被困者
    private static string originLabel;

    // —— 90秒火场新记账 ——
    private static string runRulesText;          // 规则牌组合（复盘头行）
    private static bool sprinklerRecorded;       // 喷淋联动亲历
    private static bool shutterSealed;           // 卷帘分区亲历
    private static bool backdraftChecked;        // 摸门柄识破蓄热门
    private static bool backdraftBlown;          // 强行开门吃了回燃
    private static bool phoneLured;              // 为手机铃声折返
    private static bool lightingFaultRecorded;   // 照明故障区
    private static bool corridorSealedRecorded;  // 坍塌开始封路
    private static float collapseMargin = -1f;   // 结构失效前余量（≥0=带着余量通关）

    private static readonly List<string> closedDoorNames = new List<string>();

    public static void Reset()
    {
        timeline.Clear();
        alarmDone = false; alarmTime = -1f;
        powerCutDone = quizGatePassed = extinguisherPicked = false;
        bigFireSpraySeconds = 0f; ineffectiveHintShown = false;
        routeShortcut = false; routeRecorded = false;
        spraySuppressed = originRooted = originReignited = zoneCriticalRecorded = false;
        doorOpenedRecorded = false;
        turnbackCount = 0;
        blockedExitTried = distressHeard = rescued = false;
        originLabel = null;
        runRulesText = null;
        sprinklerRecorded = shutterSealed = backdraftChecked = backdraftBlown = false;
        phoneLured = lightingFaultRecorded = corridorSealedRecorded = false;
        collapseMargin = -1f;
        closedDoorNames.Clear();
    }

    // ————— 记录（context=当时的火场快照，如"该区烟浓度0.42"） —————

    public static void RecordOriginSelected(string label)
    {
        if (string.IsNullOrEmpty(originLabel)) { originLabel = label; Add($"火从「{label}」烧了起来", false); }
    }

    /// <summary>导演抽签后的规则牌组合（复盘头行展示，不进时间轴）。</summary>
    public static void RecordRunCombo(string rulesText) => runRulesText = rulesText;

    public static void RecordAlarm(string context = null)
    {
        if (alarmDone) return;
        alarmDone = true;
        alarmTime = Time.timeSinceLevelLoad;
        Add("按下手动报警按钮，警铃联动全楼", true, context);
    }

    public static void RecordPowerCut(string context = null)
    {
        if (powerCutDone) return;
        powerCutDone = true;
        Add("配电箱拉闸断电", true, context);
    }

    public static void RecordExtinguisherPicked()
    {
        if (extinguisherPicked) return;
        extinguisherPicked = true;
        Add("取用灭火器", true);
    }

    public static void RecordOriginSuppressed(string context = null)
    {
        spraySuppressed = true;
        Add("灭火器压制住初期火——蔓延暂停", true, context);
    }

    public static void RecordOriginRooted(string context = null)
    {
        if (originRooted) return;
        originRooted = true;
        Add("断电根除电气火，火场开始收缩", true, context);
    }

    public static void RecordOriginReignited(string context = null)
    {
        if (originReignited) return;
        originReignited = true;
        Add("电弧复燃——不断电无法根除电气火灾", false, context);
    }

    public static void RecordZoneCritical(string zoneId, string context = null)
    {
        if (zoneCriticalRecorded) return;
        zoneCriticalRecorded = true;
        Add($"「{zoneId}」火势接近轰燃——结构失效死线启动", false, context);
    }

    public static void RecordQuizGatePassed(string context = null)
    {
        if (quizGatePassed) return;
        quizGatePassed = true;
        Add("确认撤离要诀，通过知识防火门", true, context);
    }

    public static void RecordDoorClosed(string doorName, string context = null)
    {
        if (!string.IsNullOrEmpty(doorName) && !closedDoorNames.Contains(doorName))
            closedDoorNames.Add(doorName);
        Add($"随手关上「{doorName}」——把烟挡在身后", true, context);
    }

    public static void RecordDoorOpened(string doorName, string context = null)
    {
        // 开门只记第一次——门开是烟流的入口，反复推门不重复占时间轴
        if (doorOpenedRecorded) return;
        doorOpenedRecorded = true;
        Add($"推开「{doorName}」——烟会顺着敞开的门流动", false, context);
    }

    public static void RecordRoute(bool shortcut, string context = null)
    {
        if (routeRecorded) return;
        routeRecorded = true;
        routeShortcut = shortcut;
        Add(shortcut ? "选择穿火近路（快，但暴露在开放区）" : "选择防火门安全路线（慢，但有关门挡烟）", !shortcut, context);
    }

    public static bool HasRoute() => routeRecorded;
    public static bool RouteIsShortcut => routeShortcut;

    /// <summary>中途折返：已选过路线后又进入另一条路线——发现此路不通就回头，活命的关键判断。</summary>
    public static void RecordTurnback(bool nowShortcut, string context = null)
    {
        turnbackCount++;
        Add(nowShortcut ? "此路已恶化——果断折返，改走近路" : "此路已恶化——果断折返，改走防火门安全路线", true, context);
    }

    /// <summary>撞上被杂物堵死的"安全出口"——门永远推不开（安全出口通畅性教育点）。</summary>
    public static void RecordBlockedExitTry()
    {
        if (blockedExitTried) return;
        blockedExitTried = true;
        Add("试图推开「安全出口」——门后杂物堵死，此路不通", false);
    }

    /// <summary>听到门后有呼救声（支线开启标记）。</summary>
    public static void RecordDistressHeard()
    {
        if (distressHeard) return;
        distressHeard = true;
        Add("听到门后传来锤门与呼救声", false);
    }

    /// <summary>破门救出门后的被困者。</summary>
    public static void RecordRescue(string context = null)
    {
        if (rescued) return;
        rescued = true;
        Add("破门救出门后的被困者", true, context);
    }

    // ————— 设施联动 —————

    /// <summary>卷帘报警联动启动（时间轴只记首道，广播由卷帘自己播）。</summary>
    public static void RecordShutterArmed(string label)
    {
        if (shutterSealed || timeline.Exists(e => e.text != null && e.text.Contains("防火卷帘即将下降"))) return;
        Add($"报警联动：「{label}」即将下降", true);
    }

    /// <summary>卷帘落定分区隔烟（时间轴只记一次）。</summary>
    public static void RecordShutterSealed(string label)
    {
        if (shutterSealed) return;
        shutterSealed = true;
        Add("防火卷帘落下分区隔烟——楼在自己关门", true);
    }

    /// <summary>喷淋按区热激活（只记首次）。</summary>
    public static void RecordSprinklerActivated(string zoneId)
    {
        if (sprinklerRecorded) return;
        sprinklerRecorded = true;
        Add($"「{zoneId}」喷淋启动——控火不灭火，它在买时间", true);
    }

    // ————— 事件牌 —————

    /// <summary>摸门柄识破蓄热门（回燃预防，正面教学）。</summary>
    public static void RecordBackdraftChecked()
    {
        if (backdraftChecked) return;
        backdraftChecked = true;
        Add("摸门柄发烫——识破蓄热门，避免回燃", true);
    }

    /// <summary>强行开蓄热门吃了回燃火球（负面教学）。</summary>
    public static void RecordBackdraftBlown()
    {
        if (backdraftBlown) return;
        backdraftBlown = true;
        Add("强行开蓄热门——回燃火球喷出（重创）", false);
    }

    /// <summary>为遗留手机铃声折返（负面教学：不为财物回头）。</summary>
    public static void RecordPhoneLureApproached()
    {
        if (phoneLured) return;
        phoneLured = true;
        Add("循铃声走向遗留手机——为财物分心", false);
    }

    /// <summary>应急照明故障区（暗区低姿摸行）。</summary>
    public static void RecordLightingFault(string zoneId)
    {
        if (lightingFaultRecorded) return;
        lightingFaultRecorded = true;
        Add($"「{zoneId}」照明故障熄灭——黑暗中低姿摸行", false);
    }

    // ————— 结构失效死线 —————

    /// <summary>死线启动（时间轴由 RecordZoneCritical 覆盖，这里只留数据）。</summary>
    public static void RecordCollapseCountdown(float duration) { }

    /// <summary>坍塌开始封路（时间轴只记一次）。</summary>
    public static void RecordCorridorSealed(string label)
    {
        if (corridorSealedRecorded) return;
        corridorSealedRecorded = true;
        Add("坍塌横梁封住走廊——结构正在失效", false);
    }

    /// <summary>带着余量通关：结构失效前 remaining 秒冲出。</summary>
    public static void RecordCollapseEscaped(float remaining) => collapseMargin = Mathf.Max(0f, remaining);

    /// <summary>死于结构坍塌（死亡走验尸文案，不进复盘）。</summary>
    public static void RecordCollapseDeath() { }

    public static void AddBigFireSpray(float delta) => bigFireSpraySeconds += delta;
    public static void MarkIneffectiveHintShown() => ineffectiveHintShown = true;

    public static string GetOriginLabel() => string.IsNullOrEmpty(originLabel) ? "现场" : originLabel;
    public static bool HasRecords() => timeline.Count > 0;

    // ————— 生成 —————

    /// <summary>疏散时间轴+决策小结（无任何记录时返回空串——宿舍/公寓不受影响）。</summary>
    public static string BuildReview()
    {
        if (timeline.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine("—— 疏散时间轴 ——");
        if (!string.IsNullOrEmpty(runRulesText))
            sb.AppendLine(Grey($"本场规则：{runRulesText}"));
        int shown = 0;
        foreach (var e in timeline)
        {
            if (shown++ >= 6)
            {
                sb.AppendLine(Grey($"……（其余 {timeline.Count - shown} 条见小结）"));
                break;
            }
            sb.AppendLine((e.good ? Green("√ ") : Red("× ")) + Seconds(e.t) + "s  " + e.text
                + (string.IsNullOrEmpty(e.Context) ? "" : Grey("  " + e.Context)));
        }
        sb.AppendLine();

        // —— 小结：最多 6 行 ——
        // ① 报警
        if (alarmDone) sb.AppendLine(Green($"√ 第{Seconds(alarmTime)}秒触发全楼警报（越早报警，大家逃生时间越多）"));
        else sb.AppendLine(Red("× 全程未触发警报——没人知道这栋楼着火了"));

        // ② 初期处置（断电/灭火合并为一行）
        if (originRooted)
            sb.AppendLine(Green("√ 教科书处置：断电+灭火器，电气火被根除"));
        else if (spraySuppressed && originReignited)
            sb.AppendLine(Yellow("△ 灭火器压制过火但没断电——电气火复燃了（断电才能根除）"));
        else if (spraySuppressed)
            sb.AppendLine(Yellow("△ 灭火器压制过初期火（电气火要断电才不复发）"));
        else if (extinguisherPicked && bigFireSpraySeconds >= 6f)
            sb.AppendLine(Red("× 恋战扑救已成势的火——喷得越久，逃生时间越少"));
        else if (extinguisherPicked && (ineffectiveHintShown || bigFireSpraySeconds > 0f))
            sb.AppendLine(Green("√ 对着大火尝试后果断放弃——撤离判断正确"));
        else if (!extinguisherPicked && !powerCutDone)
            sb.AppendLine("· 未做处置直奔撤离（火已成势时不恋战也是对的；断电可减速蔓延）");
        else if (powerCutDone)
            sb.AppendLine(Green("√ 战术断电——电气火失去电源，蔓延减速"));
        else
            sb.AppendLine("· 取了灭火器但未恋战");

        // ③ 防火门+路线合并
        if (closedDoorNames.Count > 0)
            sb.AppendLine(Green($"√ 随手关了{closedDoorNames.Count}道门（{string.Join("、", closedDoorNames)}）——烟被挡在身后"));
        else if (quizGatePassed)
            sb.AppendLine(Red("× 通过问答门后没关门——浓烟跟着你灌进了前厅"));
        if (routeRecorded)
        {
            if (routeShortcut) sb.AppendLine(Yellow("△ 选了穿火近路：快，但全程暴露在开放区火场"));
            else sb.AppendLine(Green("√ 选了防火门安全路线：低姿+关门，稳步撤离"));
        }
        if (turnbackCount > 0)
            sb.AppendLine(Green("√ 中途折返重新选路——发现此路不通就回头，火场里这是活命的关键判断"));

        // ④ 设施联动亲历（卷帘/喷淋）
        if (shutterSealed) sb.AppendLine(Green("√ 亲历卷帘分区隔烟——敞开的边门是烟的入口，联动保护要靠随手关门"));
        if (sprinklerRecorded) sb.AppendLine(Green("√ 亲历喷淋联动——它控火不灭火，买的是疏散时间"));

        // ⑤ 事件与互救
        if (blockedExitTried)
            sb.AppendLine(Red("× 撞上被杂物堵死的「安全出口」——安全出口被占用是重大隐患，永远记住两条以上疏散路线"));
        if (backdraftBlown)
            sb.AppendLine(Red("× 强行开了发烫的门——回燃（开门前先摸门柄）"));
        else if (backdraftChecked)
            sb.AppendLine(Green("√ 摸门柄识破蓄热门——回燃预防"));
        if (phoneLured)
            sb.AppendLine(Red("× 为手机铃声折返——火场里不为财物回头"));
        if (distressHeard && rescued)
            sb.AppendLine(Green("√ 你破门救出了被困的同事（火场互救：先确保自身安全，量力而行）"));
        else if (distressHeard)
            sb.AppendLine(Yellow("△ 听到过门后的呼救声——值班安全员在组织撤离时也要留意他人的动静"));

        // ⑥ 死线余量（终局冲刺的成绩单）
        if (collapseMargin >= 0f)
            sb.AppendLine(Green($"√ 结构失效前 {Seconds(collapseMargin)} 秒冲出——越过轰燃临界，就是在和建筑抢时间"));

        return sb.ToString().TrimEnd();
    }

    private static void Add(string text, bool good, string context = null)
        => timeline.Add(new Entry(Time.timeSinceLevelLoad, text, good) { Context = context });

    private static string Yellow(string s) => "<color=#F5A623>" + s + "</color>";
    private static string Green(string s) => "<color=#6FDE78>" + s + "</color>";
    private static string Red(string s) => "<color=#F25940>" + s + "</color>";
    private static string Grey(string s) => "<color=#9A9AB0>" + s + "</color>";
    private static int Seconds(float t) => Mathf.Max(0, (int)t);
}
