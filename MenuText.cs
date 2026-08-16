using System.Collections.Generic;

namespace SephiriaTogether
{
    internal static class MenuText
    {
        private static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>
        {
            ["Title"] = new[] { "Sephiria Together", "Sephiria Together", "Sephiria Together", "Sephiria Together", "Sephiria Together" },
            ["HostSettings"] = new[] { "房主设置", "房主設定", "Host settings", "호스트 설정", "ホスト設定" },
            ["TabRules"] = new[] { "规则", "規則", "Rules", "규칙", "ルール" },
            ["TabCompensation"] = new[] { "补偿", "補償", "Compensation", "보상", "補償" },
            ["TabDiagnostics"] = new[] { "诊断", "診斷", "Diagnostics", "진단", "診断" },
            ["TabHistory"] = new[] { "记录", "記錄", "History", "기록", "履歴" },
            ["HostMultiplayerTab"] = new[] { "多人设置", "多人設定", "Multiplayer", "멀티플레이", "マルチプレイ" },
            ["HostScalingTab"] = new[] { "敌人增强", "敵人增強", "Enemy scaling", "적 강화", "敵強化" },
            ["HostPlayersTab"] = new[] { "玩家状态", "玩家狀態", "Players", "플레이어", "プレイヤー" },
            ["MenuShortcut"] = new[] { "菜单快捷键", "選單快速鍵", "Menu shortcut", "메뉴 단축키", "メニューショートカット" },
            ["ChangeShortcut"] = new[] { "修改快捷键", "修改快速鍵", "Change shortcut", "단축키 변경", "ショートカット変更" },
            ["PressNewShortcut"] = new[] { "请按下新的快捷键（可带 Ctrl、Alt、Shift）。", "請按下新的快速鍵（可帶 Ctrl、Alt、Shift）。", "Press the new shortcut (Ctrl, Alt, or Shift may be included).", "새 단축키를 누르세요 (Ctrl, Alt, Shift 조합 가능).", "新しいショートカットを押してください（Ctrl、Alt、Shift対応）。" },
            ["CancelShortcut"] = new[] { "取消修改", "取消修改", "Cancel change", "변경 취소", "変更をキャンセル" },
            ["RescueShortcut"] = new[] { "请求救援快捷键", "請求救援快速鍵", "Rescue request shortcut", "구조 요청 단축키", "救援要請ショートカット" },
            ["ChangeRescueShortcut"] = new[] { "修改救援快捷键", "修改救援快速鍵", "Change rescue shortcut", "구조 단축키 변경", "救援ショートカット変更" },
            ["PressNewRescueShortcut"] = new[] { "请按下新的请求救援快捷键。", "請按下新的請求救援快速鍵。", "Press the new rescue request shortcut.", "새 구조 요청 단축키를 누르세요.", "新しい救援要請ショートカットを押してください。" },
            ["PlayerDown"] = new[] { "{0} 已倒地，需要救援！", "{0} 已倒地，需要救援！", "{0} is down and needs rescue!", "{0}님이 쓰러졌습니다. 구조가 필요합니다!", "{0}がダウンしました。救援が必要です！" },
            ["RescueRequested"] = new[] { "紧急：{0} 正在请求救援！", "緊急：{0} 正在請求救援！", "URGENT: {0} is requesting rescue!", "긴급: {0}님이 구조를 요청합니다!", "緊急：{0}が救援を要請しています！" },
            ["RescueFloor"] = new[] { "所在楼层：", "所在樓層：", "Floor:", "현재 층:", "フロア：" },
            ["UnknownPlayer"] = new[] { "未知玩家", "未知玩家", "Unknown player", "알 수 없는 플레이어", "不明なプレイヤー" },
            ["NoData"] = new[] { "暂无数据。", "暫無資料。", "No data yet.", "아직 데이터가 없습니다.", "データはまだありません。" },
            ["DownloadHelp"] = new[] { "从 GitHub Release 获取最新版。打开链接后可查看版本说明和校验值。", "從 GitHub Release 取得最新版。開啟連結後可查看版本說明和校驗值。", "Get the latest build from GitHub Releases. The release page includes notes and checksums.", "GitHub Release에서 최신 버전을 받으세요. 릴리스 페이지에 변경 사항과 해시가 있습니다.", "GitHub Releaseから最新版を取得できます。リリースページに変更内容とハッシュがあります。" },
            ["OpenReleasePage"] = new[] { "打开 Release 页面", "開啟 Release 頁面", "Open release page", "릴리스 페이지 열기", "リリースページを開く" },
            ["OpenPluginDownload"] = new[] { "下载插件 ZIP", "下載插件 ZIP", "Download plugin ZIP", "플러그인 ZIP 다운로드", "プラグインZIPをダウンロード" },
            ["HostCompensation"] = new[] { "客机自选权益由房主持久化并验证。未安装 Mod 的客机不会收到自定义消息，权益会保留。", "客機自選權益由房主持久化並驗證。未安裝 Mod 的客機不會收到自訂訊息，權益會保留。", "Client choice entitlements are persisted and validated by the host. Unmodded clients receive no custom messages and keep their entitlements.", "클라이언트 선택 권리는 호스트가 저장하고 검증합니다. 모드가 없는 클라이언트에는 사용자 지정 메시지를 보내지 않으며 권리는 유지됩니다.", "クライアントの選択権はホストが保存・検証します。Mod未導入クライアントには独自メッセージを送らず、権利は保持されます。" },
            ["HostOnly"] = new[] { "仅房主可配置。请先创建或主持多人游戏。", "僅房主可設定。請先建立或主持多人遊戲。", "Host only. Start or host a multiplayer session to configure it.", "호스트만 설정할 수 있습니다. 먼저 멀티플레이를 호스트하세요.", "ホストのみ設定できます。先にマルチプレイをホストしてください。" },
            ["ClientWaiting"] = new[] { "正在等待本地玩家和房间数据。", "正在等待本地玩家和房間資料。", "Waiting for the local player and session data.", "로컬 플레이어와 세션 데이터를 기다리는 중입니다.", "ローカルプレイヤーとセッションデータを待っています。" },
            ["ClientCompensation"] = new[] { "中途加入补偿", "途中加入補償", "Mid-run join compensation", "중도 참가 보상", "途中参加補償" },
            ["ClientAutoGranted"] = new[] { "房主自动补发经验、金钱、骰子和错过的背包扩充；首次加入还会发放维度口袋物品。以下权益由房主保存并验证。", "房主自動補發經驗、金錢、骰子和錯過的背包擴充；首次加入還會發放維度口袋物品。以下權益由房主保存並驗證。", "The host grants EXP, money, dice, and missed inventory expansions; first-time joins also receive Dimension Pocket items. The host saves and validates the choices below.", "호스트가 경험치, 돈, 주사위와 놓친 인벤토리 확장을 지급하며, 최초 참가 시 차원 주머니 아이템도 지급합니다. 아래 선택 권리는 호스트가 저장하고 검증합니다.", "ホストが経験値、お金、ダイス、未取得のインベントリ拡張を補償し、初回参加時は次元ポケットのアイテムも付与します。以下の権利はホストが保存・検証します。" },
            ["WeaponCredits"] = new[] { "可选武器升级", "可選武器升級", "Weapon upgrade choices", "무기 강화 선택", "武器強化選択" },
            ["EnchantCredits"] = new[] { "可选附魔", "可選附魔", "Enchant choices", "인챈트 선택", "エンチャント選択" },
            ["MiracleCredits"] = new[] { "可选奇迹", "可選奇蹟", "Miracle choices", "기적 선택", "奇跡選択" },
            ["CharmCredits"] = new[] { "护符奖励", "護符獎勵", "Charm rewards", "부적 보상", "護符報酬" },
            ["TabletCredits"] = new[] { "石板奖励", "石板獎勵", "Tablet rewards", "석판 보상", "石板報酬" },
            ["BossCredits"] = new[] { "Boss 自选奖励", "Boss 自選獎勵", "Boss reward choices", "보스 선택 보상", "ボス選択報酬" },
            ["CreateTabletReward"] = new[] { "生成石板奖励", "生成石板獎勵", "Create tablet reward", "석판 보상 생성", "石板報酬を生成" },
            ["CreateCharmReward"] = new[] { "生成护符五选一", "生成護符五選一", "Create five-choice charm reward", "부적 5개 선택 보상 생성", "護符5択報酬を生成" },
            ["BossCharmReward"] = new[] { "生成大型护符奖励", "生成大型護符獎勵", "Create major charm reward", "대형 부적 보상 생성", "大型護符報酬を生成" },
            ["BossTabletReward"] = new[] { "生成 Boss 石板奖励", "生成 Boss 石板獎勵", "Create boss tablet reward", "보스 석판 보상 생성", "ボス石板報酬を生成" },
            ["ClaimPending"] = new[] { "正在等待房主确认选择……", "正在等待房主確認選擇……", "Waiting for the host to confirm the choice...", "호스트의 선택 확인을 기다리는 중...", "ホストの選択確認を待っています…" },
            ["ClaimSuccess"] = new[] { "房主已确认并保存上一次领取。", "房主已確認並儲存上一次領取。", "The host confirmed and saved the last claim.", "호스트가 마지막 수령을 확인하고 저장했습니다.", "ホストが前回の受取を確認して保存しました。" },
            ["ClaimRejected"] = new[] { "领取被房主拒绝：权益不足、物品状态已变化或选择无效。", "領取被房主拒絕：權益不足、物品狀態已變更或選擇無效。", "The host rejected the claim: no entitlement remains, the item changed, or the choice is invalid.", "호스트가 수령을 거부했습니다: 권리가 없거나 아이템 상태가 변경되었거나 선택이 유효하지 않습니다.", "受取が拒否されました：権利不足、アイテム状態の変更、または無効な選択です。" },
            ["ClaimHistory"] = new[] { "本局已领取：武器 {0}，附魔 {1}，奇迹 {2}，石板 {3}，Boss {4}，护符 {5}", "本局已領取：武器 {0}，附魔 {1}，奇蹟 {2}，石板 {3}，Boss {4}，護符 {5}", "Claimed: weapon {0}, enchant {1}, miracle {2}, tablet {3}, boss {4}, charm {5}", "수령: 무기 {0}, 인챈트 {1}, 기적 {2}, 석판 {3}, 보스 {4}, 부적 {5}", "受取済み：武器 {0}、付与 {1}、奇跡 {2}、石板 {3}、ボス {4}、護符 {5}" },
            ["ClientMissingRewards"] = new[] { "最大生命、蓝宝石和背包扩充由房主自动补偿；未安装 Mod 的客机不会收到自定义消息，自选权益会保留。", "最大生命、藍寶石和背包擴充由房主自動補償；未安裝 Mod 的客機不會收到自訂訊息，自選權益會保留。", "The host grants max HP, sapphire, and inventory expansions automatically. Unmodded clients receive no custom messages; choice entitlements remain saved.", "최대 HP, 사파이어와 인벤토리 확장은 호스트가 자동 지급합니다. 모드가 없는 클라이언트에는 사용자 지정 메시지를 보내지 않으며 선택 권리는 저장됩니다.", "最大HP、サファイア、インベントリ拡張はホストが自動補償します。Mod未導入のクライアントには独自メッセージを送らず、選択権は保存されます。" },
            ["NextSpawn"] = new[] { "部分设置对新连接、新房间或新生成敌人生效。", "部分設定會套用至新連線、新房間或新生成的敵人。", "Some settings affect new connections, lobbies, or newly spawned enemies.", "일부 설정은 새 연결, 로비 또는 새로 생성된 적부터 적용됩니다.", "一部の設定は新規接続、ロビー、新しく出現する敵から適用されます。" },
            ["Multiplayer"] = new[] { "多人联机", "多人連線", "Multiplayer", "멀티플레이", "マルチプレイ" },
            ["PlayerLimit"] = new[] { "房间人数（2-250，下次建房生效）", "房間人數（2-250，下次建房生效）", "Player limit (2-250, next lobby)", "방 인원 (2-250, 다음 로비)", "ルーム人数 (2-250、次のロビー)" },
            ["Apply"] = new[] { "应用人数", "套用人數", "Apply player limit", "인원 적용", "人数を適用" },
            ["LowerProgress"] = new[] { "允许低进度玩家加入", "允許低進度玩家加入", "Allow lower-progress players", "진행도가 낮은 플레이어 허용", "進行度が低いプレイヤーを許可" },
            ["MidRun"] = new[] { "允许中途加入", "允許中途加入", "Allow fresh mid-run joining", "게임 중 신규 참가 허용", "途中からの新規参加を許可" },
            ["UngroupedTransition"] = new[] { "进入下一关无需全员集合", "進入下一關無需全員集合", "Do not require everyone at the entrance", "다음 스테이지에서 전원 집합 불필요", "次のステージで全員集合を不要にする" },
            ["UngroupedTransitionHelp"] = new[] { "开启后，房主在正确入口按交互键即可正常带全队转场；不会猜测或强制加载关卡。", "開啟後，房主在正確入口按互動鍵即可正常帶全隊轉場；不會猜測或強制載入關卡。", "When enabled, the host can use the correct entrance normally without nearby players. It never guesses or force-loads a stage.", "활성화하면 호스트가 올바른 입구를 사용해 주변에 없는 플레이어와 함께 이동할 수 있습니다.", "有効時、ホストが正しい入口を通常通り使えば、近くにいないプレイヤーも一緒に移動します。" },
            ["BreathingHeal"] = new[] { "受伤后延迟回血", "受傷後延遲回血", "Delayed healing after damage", "피해 후 지연 회복", "ダメージ後の遅延回復" },
            ["BreathingHealHelp"] = new[] { "房主计算，客机无需安装。受到伤害后 10 秒内不回血，之后固定每秒恢复 1 点；战斗中也有效，倒下时停止。", "房主計算，客機無需安裝。受到傷害後 10 秒內不回血，之後固定每秒恢復 1 點；戰鬥中也有效，倒下時停止。", "Host-calculated; clients need no plugin. No healing for 10 seconds after damage, then recover 1 HP/s. Works in combat and stops when down.", "호스트가 계산하며 클라이언트는 플러그인이 필요 없습니다. 피해 후 10초 동안 회복하지 않고 이후 1 HP/s로 회복합니다. 전투 중에도 적용되며 쓰러지면 멈춥니다.", "ホストが計算するためクライアントにプラグインは不要です。ダメージ後10秒間は回復せず、その後1 HP/sで回復します。戦闘中も有効で、ダウン時は停止します。" },
            ["AutoReviveWhenClear"] = new[] { "无敌人时自动复活所有人", "無敵人時自動復活所有人", "Auto-revive everyone when clear", "적이 없을 때 모두 자동 부활", "敵がいない時に全員自動復活" },
            ["AutoReviveWhenClearHelp"] = new[] { "房主确认没有存活的敌对单位且存活玩家已脱战 2 秒后，以 50% 最大生命复活所有倒地玩家。客机无需安装 Mod。", "房主確認沒有存活的敵對單位且存活玩家已脫戰 2 秒後，以 50% 最大生命復活所有倒地玩家。客機無需安裝 Mod。", "Host-side. After no living hostile units remain and living players have been out of combat for 2 seconds, revive every downed player at 50% max HP. Clients need no plugin.", "호스트가 처리합니다. 살아 있는 적대 유닛이 없고 생존 플레이어가 2초 동안 전투에서 벗어나면 쓰러진 모든 플레이어를 최대 체력 50%로 부활시킵니다. 클라이언트 모드는 필요 없습니다.", "ホスト側で処理します。生存する敵対ユニットがなく、生存プレイヤーが2秒間戦闘外になった後、ダウン中の全員を最大HP50%で復活させます。クライアントModは不要です。" },
            ["FriendlyFire"] = new[] { "开启友伤", "開啟友傷", "Friendly fire", "아군 피해", "フレンドリーファイア" },
            ["FriendlyFireHelp"] = new[] { "仅玩家攻击其他玩家时生效。伤害除以 100，单次最低 1、最高 5 点；格挡仍可完全挡住。", "僅玩家攻擊其他玩家時生效。傷害除以 100，單次最低 1、最高 5 點；格擋仍可完全擋住。", "Only player attacks against other players. Damage is divided by 100, with 1 minimum and 5 maximum; guarding still blocks it.", "플레이어가 다른 플레이어를 공격할 때만 적용됩니다. 피해는 100으로 나누며 최소 1, 최대 5이고 방어로 완전히 막을 수 있습니다.", "プレイヤーが他のプレイヤーを攻撃した場合のみ有効。ダメージを100で割り、最低1、最大5。ガードで完全に防げます。" },
            ["WeaponCatchup"] = new[] { "补偿武器强化阶数", "補償武器強化階級", "Catch up weapon tier", "무기 강화 단계 보정", "武器強化段階を補正" },
            ["Catchup"] = new[] { "经验追赶（100%）", "經驗追趕（100%）", "EXP catch-up (100%)", "경험치 따라잡기 (100%)", "経験値キャッチアップ（100%）" },
            ["EnemyScaling"] = new[] { "敌人增强", "敵人增強", "Enemy enhancement", "적 강화", "敵強化" },
            ["ScalingHelp"] = new[] { "选择一个难度即可，无需计算百分比。倍率 1.00x 表示不额外增强；游戏原有的多人难度仍然保留。", "選擇一個難度即可，無需計算百分比。倍率 1.00x 表示不額外增強；遊戲原有的多人難度仍然保留。", "Choose a difficulty without calculating percentages. 1.00x means no extra modifier; the game's multiplayer scaling still applies.", "백분율 계산 없이 난이도를 선택하세요. 1.00x는 추가 보정이 없음을 뜻하며 게임의 멀티플레이 보정은 유지됩니다.", "割合を計算せず難易度を選べます。1.00xは追加補正なしを意味し、ゲーム本来のマルチプレイ補正は維持されます。" },
            ["VanillaScaling"] = new[] { "原版与困难模式实际参数", "原版與困難模式實際參數", "Vanilla and hard-mode values", "원본 및 하드 모드 실제 수치", "原版・ハードモード実測値" },
            ["CurrentPreset"] = new[] { "当前难度", "目前難度", "Current difficulty", "현재 난이도", "現在の難易度" },
            ["PresetOriginal"] = new[] { "原版", "原版", "Original", "원본", "原版" },
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
            ["Baseline"] = new[] { "不额外增强的人数（单人测试设为 0）", "不額外增強的人數（單人測試設為 0）", "Players without extra scaling (0 to test solo)", "추가 보정 없는 인원 (솔로 테스트는 0)", "追加補正なしの人数（ソロテストは0）" },
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
            ["Close"] = new[] { "关闭", "關閉", "Close", "닫기", "閉じる" }
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
