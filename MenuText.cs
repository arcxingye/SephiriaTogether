using System.Collections.Generic;

namespace SephiriaTogether
{
    internal static class MenuText
    {
        private static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>
        {
            ["Title"] = new[] { "Sephiria Together", "Sephiria Together", "Sephiria Together", "Sephiria Together", "Sephiria Together" },
            ["HostSettings"] = new[] { "房主设置", "房主設定", "Host settings", "호스트 설정", "ホスト設定" },
            ["HostOnly"] = new[] { "仅房主可配置。请先创建或主持多人游戏。", "僅房主可設定。請先建立或主持多人遊戲。", "Host only. Start or host a multiplayer session to configure it.", "호스트만 설정할 수 있습니다. 먼저 멀티플레이를 호스트하세요.", "ホストのみ設定できます。先にマルチプレイをホストしてください。" },
            ["NextSpawn"] = new[] { "部分设置对新连接、新房间或新生成敌人生效。", "部分設定會套用至新連線、新房間或新生成的敵人。", "Some settings affect new connections, lobbies, or newly spawned enemies.", "일부 설정은 새 연결, 로비 또는 새로 생성된 적부터 적용됩니다.", "一部の設定は新規接続、ロビー、新しく出現する敵から適用されます。" },
            ["Multiplayer"] = new[] { "多人联机", "多人連線", "Multiplayer", "멀티플레이", "マルチプレイ" },
            ["PlayerLimit"] = new[] { "房间人数（2-250，下次建房生效）", "房間人數（2-250，下次建房生效）", "Player limit (2-250, next lobby)", "방 인원 (2-250, 다음 로비)", "ルーム人数 (2-250、次のロビー)" },
            ["Apply"] = new[] { "应用人数", "套用人數", "Apply player limit", "인원 적용", "人数を適用" },
            ["LowerProgress"] = new[] { "允许低进度玩家加入", "允許低進度玩家加入", "Allow lower-progress players", "진행도가 낮은 플레이어 허용", "進行度が低いプレイヤーを許可" },
            ["MidRun"] = new[] { "允许中途加入", "允許中途加入", "Allow fresh mid-run joining", "게임 중 신규 참가 허용", "途中からの新規参加を許可" },
            ["StageTransition"] = new[] { "关卡转场", "關卡轉場", "Stage transition", "스테이지 이동", "ステージ移動" },
            ["PendingStage"] = new[] { "待进入关卡", "待進入關卡", "Pending stage", "이동할 스테이지", "移動先ステージ" },
            ["NoPendingStage"] = new[] { "请先在关卡入口正常尝试一次", "請先在關卡入口正常嘗試一次", "Try the stage entrance once first", "먼저 스테이지 입구를 한 번 사용하세요", "先にステージ入口を一度使用してください" },
            ["ForceNextStage"] = new[] { "强制全员进入下一关", "強制全員進入下一關", "Force everyone to next stage", "모두 다음 스테이지로 강제 이동", "全員を次のステージへ強制移動" },
            ["WeaponCatchup"] = new[] { "补偿武器强化阶数", "補償武器強化階級", "Catch up weapon tier", "무기 강화 단계 보정", "武器強化段階を補正" },
            ["Catchup"] = new[] { "经验追赶", "經驗追趕", "EXP catch-up", "경험치 따라잡기", "経験値キャッチアップ" },
            ["CycleCatchup"] = new[] { "切换 0 / 50 / 75 / 100%", "切換 0 / 50 / 75 / 100%", "Cycle 0 / 50 / 75 / 100%", "0 / 50 / 75 / 100% 전환", "0 / 50 / 75 / 100% 切替" },
            ["EnemyScaling"] = new[] { "敌人缩放", "敵人縮放", "Enemy scaling", "적 스케일링", "敵スケーリング" },
            ["ScalingHelp"] = new[] { "选择一个难度即可，无需计算百分比。倍率 1.00x 表示不额外增强；游戏原有的多人难度仍然保留。", "選擇一個難度即可，無需計算百分比。倍率 1.00x 表示不額外增強；遊戲原有的多人難度仍然保留。", "Choose a difficulty without calculating percentages. 1.00x means no extra modifier; the game's multiplayer scaling still applies.", "백분율 계산 없이 난이도를 선택하세요. 1.00x는 추가 보정이 없음을 뜻하며 게임의 멀티플레이 보정은 유지됩니다.", "割合を計算せず難易度を選べます。1.00xは追加補正なしを意味し、ゲーム本来のマルチプレイ補正は維持されます。" },
            ["CurrentPreset"] = new[] { "当前难度", "目前難度", "Current difficulty", "현재 난이도", "現在の難易度" },
            ["PresetOriginal"] = new[] { "原版（不额外增强）", "原版（不額外增強）", "Original (no extra)", "원본 (추가 없음)", "原版（追加なし）" },
            ["PresetLight"] = new[] { "轻度", "輕度", "Light", "가벼움", "ライト" },
            ["PresetStandard"] = new[] { "标准", "標準", "Standard", "표준", "標準" },
            ["PresetHigh"] = new[] { "高压", "高壓", "High pressure", "고압", "高圧" },
            ["PresetCustom"] = new[] { "自定义", "自訂", "Custom", "사용자 설정", "カスタム" },
            ["ScalingPreviewPlayers"] = new[] { "当前队伍：{0} 人", "目前隊伍：{0} 人", "Current party: {0} players", "현재 파티: {0}명", "現在のパーティー：{0}人" },
            ["PreviewHealth"] = new[] { "新敌人血量（本 Mod 倍率）", "新敵人血量（本 Mod 倍率）", "New enemy HP (mod multiplier)", "새 적 체력 (모드 배율)", "新規敵HP（Mod倍率）" },
            ["PreviewCount"] = new[] { "新波次怪物数（本 Mod 倍率）", "新波次怪物數（本 Mod 倍率）", "New wave size (mod multiplier)", "새 웨이브 규모 (모드 배율)", "新規ウェーブ数（Mod倍率）" },
            ["ScalingTiming"] = new[] { "修改后只影响新生成的敌人和之后计算的波次，当前已经出现的怪物不变。", "修改後只影響新生成的敵人和之後計算的波次，目前已出現的怪物不變。", "Changes affect newly spawned enemies and future waves. Existing enemies are unchanged.", "변경 사항은 새로 생성되는 적과 이후 웨이브에만 적용됩니다.", "変更は新しく出現する敵と以降のウェーブにのみ反映されます。" },
            ["ShowAdvanced"] = new[] { "展开高级设置", "展開進階設定", "Show advanced settings", "고급 설정 표시", "詳細設定を表示" },
            ["HideAdvanced"] = new[] { "收起高级设置", "收起進階設定", "Hide advanced settings", "고급 설정 숨기기", "詳細設定を隠す" },
            ["Baseline"] = new[] { "基准人数", "基準人數", "Baseline players", "기준 인원", "基準人数" },
            ["ExtraHp"] = new[] { "每名额外玩家增加血量", "每名額外玩家增加血量", "Extra HP per player", "추가 인원당 체력", "追加人数ごとのHP" },
            ["HpCap"] = new[] { "额外血量倍率上限", "額外血量倍率上限", "Extra HP multiplier cap", "추가 체력 배율 상한", "追加HP倍率上限" },
            ["EnemyCount"] = new[] { "增加程序化波次敌人数量", "增加程序化波次敵人數量", "Scale procedural wave enemy count", "절차형 웨이브 적 수 증가", "自動生成ウェーブの敵数を増加" },
            ["CountPerPlayer"] = new[] { "每名额外玩家增加怪物", "每名額外玩家增加怪物", "Extra enemies per player", "추가 인원당 적 수", "追加人数ごとの敵数" },
            ["CountCap"] = new[] { "怪物数量倍率上限", "怪物數量倍率上限", "Enemy-count multiplier cap", "적 수 배율 상한", "敵数倍率上限" },
            ["Players"] = new[] { "玩家状态", "玩家狀態", "Player status", "플레이어 상태", "プレイヤー状態" },
            ["Connected"] = new[] { "在线", "在線", "Connected", "접속", "接続中" },
            ["Dead"] = new[] { "已倒下", "已倒下", "Down", "쓰러짐", "ダウン" },
            ["Loading"] = new[] { "加载中", "載入中", "Loading", "로딩 중", "ロード中" },
            ["Host"] = new[] { "房主", "房主", "Host", "호스트", "ホスト" },
            ["Level"] = new[] { "等级", "等級", "Lv", "레벨", "Lv" },
            ["Floor"] = new[] { "楼层", "樓層", "Floor", "층", "フロア" },
            ["Kick"] = new[] { "踢出", "踢出", "Kick", "추방", "キック" },
            ["Ban"] = new[] { "禁止重连暂未启用", "禁止重連暫未啟用", "Rejoin ban is disabled", "재접속 금지는 비활성화됨", "再接続禁止は無効" },
            ["Save"] = new[] { "保存设置", "儲存設定", "Save settings", "설정 저장", "設定を保存" },
            ["Close"] = new[] { "关闭（F8）", "關閉（F8）", "Close (F8)", "닫기 (F8)", "閉じる (F8)" }
        };

        internal static string Get(string key)
        {
            int language = 2;
            string current = LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguage : "en-US";
            if (current == "zh-CN") language = 0;
            else if (current == "zh-TW") language = 1;
            else if (current == "ko-KR") language = 3;
            else if (current == "ja-JP") language = 4;
            return Text.TryGetValue(key, out string[] values) ? values[language] : key;
        }
    }
}
