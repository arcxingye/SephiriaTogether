using System.Collections.Generic;

namespace SephiriaTogether
{
    internal static class MenuText
    {
        private static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>
        {
            ["Title"] = new[] { "Sephiria Together", "Sephiria Together", "Sephiria Together", "Sephiria Together", "Sephiria Together" },
            ["Subtitle"] = new[] { "by arcxingye", "by arcxingye", "by arcxingye", "by arcxingye", "by arcxingye" },
            ["HostSettings"] = new[] { "联机与自动游玩", "連線與自動遊玩", "Multiplayer and autoplay", "멀티플레이 및 자동 플레이", "マルチプレイ・自動プレイ" },
            ["TabRules"] = new[] { "规则", "規則", "Rules", "규칙", "ルール" },
            ["TabAutoPilot"] = new[] { "自动游玩", "自動遊玩", "Autoplay", "자동 플레이", "自動プレイ" },
            ["TabCompensation"] = new[] { "补偿", "補償", "Compensation", "보상", "補償" },
            ["TabDiagnostics"] = new[] { "诊断", "診斷", "Diagnostics", "진단", "診断" },
            ["TabHistory"] = new[] { "补偿记录", "補償記錄", "Claim history", "보상 기록", "補償履歴" },
            ["TabSaves"] = new[] { "存档", "存檔", "Saves", "저장", "セーブ" },
            ["TabTransfer"] = new[] { "转账", "轉帳", "Transfer", "송금", "送金" },
            ["LeafTransfer"] = new[] { "叶子转账", "葉子轉帳", "Leaf transfer", "잎 송금", "葉の送金" },
            ["LeafTransferHelp"] = new[] { "选择在线玩家并输入正整数金额。房主会验证余额与目标后同时扣除和增加叶子；收款人不需要安装 Mod。", "選擇線上玩家並輸入正整數金額。房主會驗證餘額與目標後同時扣除和增加葉子；收款人不需要安裝 Mod。", "Choose an online player and enter a positive whole amount. The host validates the balance and recipient before moving Leaves atomically. The recipient does not need the mod.", "온라인 플레이어와 양의 정수 금액을 선택하세요. 호스트가 잔액과 대상을 확인한 뒤 잎을 동시에 차감하고 지급합니다. 받는 사람은 모드가 없어도 됩니다.", "オンラインの相手と正の整数額を選びます。ホストが残高と対象を検証して葉を同時に移動します。受取側にModは不要です。" },
            ["TransferBalance"] = new[] { "当前叶子", "目前葉子", "Current Leaves", "현재 잎", "現在の葉" },
            ["TransferAmount"] = new[] { "转账金额", "轉帳金額", "Amount", "송금액", "送金額" },
            ["TransferRecipientBalance"] = new[] { "对方叶子：", "對方葉子：", "Recipient Leaves:", "상대 잎:", "相手の葉：" },
            ["TransferSend"] = new[] { "转账给此玩家", "轉帳給此玩家", "Transfer to player", "이 플레이어에게 송금", "このプレイヤーへ送金" },
            ["TransferConfirm"] = new[] { "确认转账", "確認轉帳", "Confirm transfer", "송금 확인", "送金を確定" },
            ["TransferCancel"] = new[] { "取消", "取消", "Cancel", "취소", "キャンセル" },
            ["TransferConfirmHelp"] = new[] { "确认向 {0} 转账 {1} 叶子？", "確認向 {0} 轉帳 {1} 葉子？", "Transfer {1} Leaves to {0}?", "{0}님에게 잎 {1}개를 송금할까요?", "{0}へ葉を{1}送金しますか？" },
            ["TransferNoRecipients"] = new[] { "没有其他在线玩家。", "沒有其他線上玩家。", "No other players are online.", "다른 온라인 플레이어가 없습니다.", "他のオンラインプレイヤーはいません。" },
            ["TransferPending"] = new[] { "正在等待房主确认转账……", "正在等待房主確認轉帳……", "Waiting for the host to confirm the transfer...", "호스트의 송금 확인을 기다리는 중...", "ホストの送金確認を待っています…" },
            ["TransferSuccess"] = new[] { "已向 {0} 转账 {1} 叶子，剩余 {2}。", "已向 {0} 轉帳 {1} 葉子，剩餘 {2}。", "Transferred {1} Leaves to {0}. Balance: {2}.", "{0}님에게 잎 {1}개를 보냈습니다. 잔액: {2}.", "{0}へ葉を{1}送金しました。残高：{2}。" },
            ["TransferReceived"] = new[] { "收到 {0} 转来的 {1} 叶子，当前 {2}。", "收到 {0} 轉來的 {1} 葉子，目前 {2}。", "Received {1} Leaves from {0}. Balance: {2}.", "{0}님에게서 잎 {1}개를 받았습니다. 잔액: {2}.", "{0}から葉を{1}受け取りました。残高：{2}。" },
            ["TransferInvalidAmount"] = new[] { "转账金额必须是有效的正整数。", "轉帳金額必須是有效的正整數。", "The transfer amount must be a valid positive whole number.", "송금액은 유효한 양의 정수여야 합니다.", "送金額は有効な正の整数で指定してください。" },
            ["TransferInsufficient"] = new[] { "叶子不足，当前只有 {0}。", "葉子不足，目前只有 {0}。", "Not enough Leaves. Current balance: {0}.", "잎이 부족합니다. 현재 잔액: {0}.", "葉が不足しています。現在の残高：{0}。" },
            ["TransferTargetLimit"] = new[] { "对方持有的叶子过多，无法完成转账。", "對方持有的葉子過多，無法完成轉帳。", "The recipient holds too many Leaves to complete this transfer.", "상대의 잎이 너무 많아 송금할 수 없습니다.", "相手の葉が多すぎるため送金できません。" },
            ["TransferRateLimited"] = new[] { "操作过快，请稍后再试。", "操作過快，請稍後再試。", "Too many requests. Try again shortly.", "요청이 너무 빠릅니다. 잠시 후 다시 시도하세요.", "操作が速すぎます。少し待ってから再試行してください。" },
            ["TransferUnavailable"] = new[] { "当前无法转账；请确认已联机且房主版本支持此功能。", "目前無法轉帳；請確認已連線且房主版本支援此功能。", "Transfers are unavailable. Check the connection and host version.", "현재 송금할 수 없습니다. 연결과 호스트 버전을 확인하세요.", "現在は送金できません。接続とホスト版を確認してください。" },
            ["TransferTimeout"] = new[] { "房主确认较慢，为防止重复扣款已锁定本次请求；请等待结果或重新连接后检查余额。", "房主確認較慢，為防止重複扣款已鎖定本次請求；請等待結果或重新連線後檢查餘額。", "Host confirmation is delayed. This request remains locked to prevent a duplicate charge; wait for the result or reconnect and check the balance.", "중복 차감을 막기 위해 이 요청을 잠갔습니다. 결과를 기다리거나 재접속 후 잔액을 확인하세요.", "重複引落し防止のため、この要求をロックしています。結果を待つか、再接続後に残高を確認してください。" },
            ["SaveManager"] = new[] { "存档管理", "存檔管理", "Save manager", "저장 관리", "セーブ管理" },
            ["SaveManagerHelp"] = new[] { "手动备份保存在独立目录，不受游戏自动删除旧备份的逻辑影响。使用存档会先安全返回标题并等待保存结束，再替换文件并立即重新载入；恢复前会自动创建一份独立快照。", "手動備份保存在獨立目錄，不受遊戲自動刪除舊備份的邏輯影響。使用存檔會先安全返回標題並等待保存結束，再替換檔案並立即重新載入；恢復前會自動建立一份獨立快照。", "Manual backups are kept in a separate directory and are not removed by the game's backup rotation. Activating a save safely returns to the title, waits for saving to finish, replaces the files, and immediately reloads. A separate snapshot is created before every restore.", "수동 백업은 별도 폴더에 보관되어 게임의 오래된 백업 삭제 대상이 아닙니다. 저장을 사용하면 안전하게 타이틀로 돌아가 저장 완료를 기다린 뒤 파일을 교체하고 즉시 다시 불러옵니다. 복원 전 별도 스냅샷을 자동 생성합니다.", "手動バックアップは別フォルダに保存され、ゲームの古いバックアップ削除対象になりません。使用時は安全にタイトルへ戻り、保存完了後にファイルを置換して直ちに再読込します。復元前には独立スナップショットを自動作成します。" },
            ["SaveManagerDirectory"] = new[] { "存档目录", "存檔目錄", "Save directory", "저장 폴더", "セーブフォルダ" },
            ["SaveManagerCurrent"] = new[] { "当前槽位", "目前槽位", "Current slot", "현재 슬롯", "現在のスロット" },
            ["SaveManagerBackupNow"] = new[] { "立即备份当前存档", "立即備份目前存檔", "Back up current save", "현재 저장 백업", "現在のセーブをバックアップ" },
            ["SaveManagerRefresh"] = new[] { "刷新列表", "重新整理列表", "Refresh list", "목록 새로고침", "一覧を更新" },
            ["SaveManagerUse"] = new[] { "立即使用", "立即使用", "Use now", "지금 사용", "今すぐ使用" },
            ["SaveManagerConfirmUse"] = new[] { "确认切换", "確認切換", "Confirm switch", "전환 확인", "切替を確認" },
            ["SaveManagerCancel"] = new[] { "取消", "取消", "Cancel", "취소", "キャンセル" },
            ["SaveManagerConfirmHelp"] = new[] { "将离开当前联机或游戏并载入此存档。", "將離開目前連線或遊戲並載入此存檔。", "This leaves the current session and loads this save.", "현재 세션을 나가고 이 저장을 불러옵니다.", "現在のセッションを終了してこのセーブを読み込みます。" },
            ["SaveManagerActiveSave"] = new[] { "活动存档", "活動存檔", "Active save", "활성 저장", "通常セーブ" },
            ["SaveManagerGameBackup"] = new[] { "游戏备份", "遊戲備份", "Game backup", "게임 백업", "ゲームバックアップ" },
            ["SaveManagerModBackup"] = new[] { "独立备份", "獨立備份", "Independent backup", "독립 백업", "独立バックアップ" },
            ["SaveManagerWithRun"] = new[] { "可恢复到地牢", "可恢復到地牢", "resumes inside the dungeon", "던전에서 계속 가능", "ダンジョン内から再開" },
            ["SaveManagerMainOnly"] = new[] { "恢复后从大厅开始", "恢復後從大廳開始", "starts from the lobby after restore", "복원 후 로비에서 시작", "復元後はロビーから開始" },
            ["SaveManagerFound"] = new[] { "找到 {0} 个可用存档。", "找到 {0} 個可用存檔。", "Found {0} available saves.", "사용 가능한 저장 {0}개를 찾았습니다.", "利用可能なセーブが{0}件あります。" },
            ["SaveManagerSaving"] = new[] { "正在保存并创建独立备份……", "正在儲存並建立獨立備份……", "Saving and creating an independent backup...", "저장 및 독립 백업 생성 중...", "保存して独立バックアップを作成中…" },
            ["SaveManagerBackupCreated"] = new[] { "独立备份已创建：{0}", "獨立備份已建立：{0}", "Independent backup created: {0}", "독립 백업 생성됨: {0}", "独立バックアップを作成しました：{0}" },
            ["SaveManagerSwitching"] = new[] { "正在安全退出当前会话……", "正在安全離開目前工作階段……", "Safely leaving the current session...", "현재 세션을 안전하게 종료 중...", "現在のセッションを安全に終了中…" },
            ["SaveManagerLoading"] = new[] { "存档已恢复，正在重新载入……", "存檔已恢復，正在重新載入……", "Save restored; reloading...", "저장 복원 완료, 다시 불러오는 중...", "セーブを復元し、再読込中…" },
            ["SaveManagerSwitchTimeout"] = new[] { "等待标题或保存结束超时，未替换任何文件。", "等待標題或儲存結束逾時，未取代任何檔案。", "Timed out waiting for the title or save completion; no files were replaced.", "타이틀 또는 저장 완료 대기 시간이 초과되어 파일을 교체하지 않았습니다.", "タイトルまたは保存完了の待機がタイムアウトし、ファイルは置換されませんでした。" },
            ["SaveManagerTitleUnavailable"] = new[] { "已恢复文件，但标题界面尚未就绪；请从标题开始游戏。", "已恢復檔案，但標題介面尚未就緒；請從標題開始遊戲。", "Files were restored, but the title UI was not ready. Start the game from the title screen.", "파일은 복원되었지만 타이틀 UI가 준비되지 않았습니다. 타이틀에서 게임을 시작하세요.", "ファイルは復元されましたがタイトルUIが未準備です。タイトルからゲームを開始してください。" },
            ["SaveManagerError"] = new[] { "存档操作失败：{0}", "存檔操作失敗：{0}", "Save operation failed: {0}", "저장 작업 실패: {0}", "セーブ操作に失敗：{0}" },
            ["HostMultiplayerTab"] = new[] { "多人设置", "多人設定", "Multiplayer", "멀티플레이", "マルチプレイ" },
            ["HostScalingTab"] = new[] { "敌人难度", "敵人難度", "Enemy difficulty", "적 난이도", "敵難易度" },
            ["HostPlayersTab"] = new[] { "玩家状态", "玩家狀態", "Players", "플레이어", "プレイヤー" },
            ["MenuShortcut"] = new[] { "菜单快捷键", "選單快速鍵", "Menu shortcut", "메뉴 단축키", "メニューショートカット" },
            ["ChangeShortcut"] = new[] { "修改快捷键", "修改快速鍵", "Change shortcut", "단축키 변경", "ショートカット変更" },
            ["PressNewShortcut"] = new[] { "请按下新的快捷键（可带 Ctrl、Alt、Shift）。", "請按下新的快速鍵（可帶 Ctrl、Alt、Shift）。", "Press the new shortcut (Ctrl, Alt, or Shift may be included).", "새 단축키를 누르세요 (Ctrl, Alt, Shift 조합 가능).", "新しいショートカットを押してください（Ctrl、Alt、Shift対応）。" },
            ["CancelShortcut"] = new[] { "取消修改", "取消修改", "Cancel change", "변경 취소", "変更をキャンセル" },
            ["RescueShortcut"] = new[] { "请求救援快捷键", "請求救援快速鍵", "Rescue request shortcut", "구조 요청 단축키", "救援要請ショートカット" },
            ["ChangeRescueShortcut"] = new[] { "修改救援快捷键", "修改救援快速鍵", "Change rescue shortcut", "구조 단축키 변경", "救援ショートカット変更" },
            ["PressNewRescueShortcut"] = new[] { "请按下新的请求救援快捷键。", "請按下新的請求救援快速鍵。", "Press the new rescue request shortcut.", "새 구조 요청 단축키를 누르세요.", "新しい救援要請ショートカットを押してください。" },
            ["AutoPilotShortcut"] = new[] { "自动游玩快捷键", "自動遊玩快速鍵", "Autoplay shortcut", "자동 플레이 단축키", "自動プレイキー" },
            ["AutoPilotLocalSettings"] = new[] { "自动游玩", "自動遊玩", "Autoplay", "자동 플레이", "自動プレイ" },
            ["AutoPilotNamePrefix"] = new[] { "（自动游玩）", "（自動遊玩）", "(Autoplay) ", "(자동 플레이) ", "（自動プレイ）" },
            ["ChangeAutoPilotShortcut"] = new[] { "修改自动游玩快捷键", "修改自動遊玩快速鍵", "Change autoplay shortcut", "자동 플레이 단축키 변경", "自動プレイキー変更" },
            ["PressNewAutoPilotShortcut"] = new[] { "请按下新的自动游玩快捷键。", "請按下新的自動遊玩快速鍵。", "Press the new autoplay shortcut.", "새 자동 플레이 단축키를 누르세요.", "新しい自動プレイキーを押してください。" },
            ["AutoPilotHelp"] = new[] { "开启后自动移动、战斗、拾取和前进，并按当前武器及其强化调整攻击方式和距离：远程保持距离持续射击，近战进入各自有效范围。优先救援可到达的队友，按快捷键可随时关闭。", "開啟後自動移動、戰鬥、拾取和前進，並依目前武器及其強化調整攻擊方式與距離：遠程保持距離持續射擊，近戰進入各自有效範圍。優先救援可到達的隊友，按快速鍵可隨時關閉。", "Automatically move, fight, collect, and advance, adapting range and input to the equipped weapon and its upgrades. Ranged weapons maintain distance and keep firing; melee weapons enter their effective reach. Reachable teammates are rescued first.", "자동 이동, 전투, 수집과 진행을 하며 현재 무기와 강화에 맞춰 공격 방식과 거리를 조정합니다. 원거리 무기는 거리를 유지하며 계속 사격하고, 근접 무기는 각자의 유효 사거리에 진입합니다. 도달 가능한 팀원을 우선 구조합니다.", "移動、戦闘、回収、進行を自動化し、現在の武器と強化に応じて攻撃方法と距離を調整します。遠距離武器は間合いを保って射撃し、近接武器は固有の有効範囲まで接近します。到達可能な仲間を優先して救助します。" },
            ["EnableAutoPilot"] = new[] { "开启自动游玩", "開啟自動遊玩", "Enable autoplay", "자동 플레이 켜기", "自動プレイを有効化" },
            ["DisableAutoPilot"] = new[] { "关闭自动游玩", "關閉自動遊玩", "Disable autoplay", "자동 플레이 끄기", "自動プレイを無効化" },
            ["AutoPilotEnabled"] = new[] { "自动游玩已开启。", "自動遊玩已開啟。", "Autoplay enabled.", "자동 플레이가 켜졌습니다.", "自動プレイを有効にしました。" },
            ["AutoPilotDisabled"] = new[] { "自动游玩已关闭。", "自動遊玩已關閉。", "Autoplay disabled.", "자동 플레이가 꺼졌습니다.", "自動プレイを無効にしました。" },
            ["AutoPilotBanner"] = new[] { "自动游玩中 · {0} 关闭", "自動遊玩中 · {0} 關閉", "AUTOPLAY · {0} TO STOP", "자동 플레이 · {0}로 중지", "自動プレイ中 · {0}で停止" },
            ["AutoAttackMode"] = new[] { "主要攻击方式", "主要攻擊方式", "Primary attack mode", "주 공격 방식", "主な攻撃方法" },
            ["AutoAttackLeftOnly"] = new[] { "仅左键", "僅左鍵", "Left only", "왼쪽만", "左のみ" },
            ["AutoAttackPrimary"] = new[] { "主左键", "主左鍵", "Prefer left", "왼쪽 우선", "左優先" },
            ["AutoAttackRightOnly"] = new[] { "仅右键", "僅右鍵", "Right only", "오른쪽만", "右のみ" },
            ["AutoAttackSecondary"] = new[] { "主右键", "主右鍵", "Prefer right", "오른쪽 우선", "右優先" },
            ["AutoAttackModeHelp"] = new[] { "“仅”模式不会使用另一种武器攻击；“主左键”会以左键持续输出并穿插可用右键，“主右键”会优先右键，在蓝量、武器资源或状态不足时改用左键。自动技能、格挡与躲避不受此设置影响。", "「僅」模式不會使用另一種武器攻擊；「主左鍵」以左鍵持續輸出並穿插可用右鍵，「主右鍵」優先右鍵，在魔力、武器資源或狀態不足時改用左鍵。自動技能、格擋與閃避不受此設定影響。", "Only modes never use the other weapon attack. Prefer left sustains primary attacks and inserts ready specials; Prefer right uses specials first and falls back to primary while MP, weapon resources, or state are unavailable. Skills, defense, and evasion are independent.", "'전용' 모드는 반대쪽 무기 공격을 사용하지 않습니다. 왼쪽 우선은 기본 공격 중 가능한 특수 공격을 섞고, 오른쪽 우선은 특수 공격을 먼저 쓰며 MP, 무기 자원 또는 상태가 부족하면 기본 공격을 사용합니다. 스킬, 방어, 회피는 별도입니다.", "「のみ」は反対側の武器攻撃を使いません。左優先は通常攻撃を続けつつ使用可能な特殊攻撃を挟み、右優先は特殊攻撃を優先してMP・武器資源・状態不足時に通常攻撃へ切り替えます。スキル、防御、回避は別設定です。" },
            ["AutoArrangeInventory"] = new[] { "自动整理背包", "自動整理背包", "Auto-arrange inventory", "인벤토리 자동 정리", "インベントリ自動整理" },
            ["AutoArrangeInventoryHelp"] = new[] { "脱离战斗且背包稳定 2 秒后，自动调整物品和石板位置，优先提高生效神器的等级。", "脫離戰鬥且背包穩定 2 秒後，自動調整物品和石板位置，優先提高生效神器的等級。", "After two seconds out of combat with a stable inventory, rearrange items and Tablets to improve enabled Charm levels.", "전투가 끝나고 인벤토리가 2초 동안 안정되면 아이템과 석판을 정리해 활성 부적 레벨을 높입니다.", "戦闘外でインベントリが2秒安定すると、アイテムと石板を整理して有効な護符レベルを高めます。" },
            ["AutoDefend"] = new[] { "自动格挡与弹反", "自動格擋與彈反", "Automatic guard and parry", "자동 방어 및 패링", "自動ガード・パリィ" },
            ["AutoDefendHelp"] = new[] { "剑盾、匕首、刀和长棍会使用各自的格挡、弹反或反击动作；巨剑没有防御型右键，会优先冲刺或走位。炸药、激光和范围攻击均优先躲避。", "劍盾、匕首、刀和長棍會使用各自的格擋、彈反或反擊動作；巨劍沒有防禦型右鍵，會優先衝刺或走位。炸藥、雷射和範圍攻擊均優先躲避。", "Sword-and-shield, dagger, katana, and staff use their guard, parry, or counter actions. Greatsword has no defensive secondary action and evades instead. Dynamite, lasers, and area attacks are always avoided first.", "검방패, 단검, 도와 장봉은 각자의 방어, 패링 또는 반격을 사용합니다. 대검은 방어형 보조 공격이 없어 이동과 대시로 피합니다. 다이너마이트, 레이저와 범위 공격은 항상 우선 회피합니다.", "剣盾、短剣、刀、長棍は各自のガード、パリィ、反撃を使用します。大剣には防御型サブ攻撃がないため移動・ダッシュで回避します。ダイナマイト、レーザー、範囲攻撃は常に優先回避します。" },
            ["AutoChoiceStrategy"] = new[] { "奖励自动选择方式", "獎勵自動選擇方式", "Reward selection", "보상 선택 방식", "報酬選択方式" },
            ["AutoChoicePresetFirst"] = new[] { "优先预设", "優先預設", "Prefer presets", "프리셋 우선", "プリセット優先" },
            ["AutoChoiceFavoriteFirst"] = new[] { "优先收藏", "優先收藏", "Prefer favorites", "즐겨찾기 우선", "お気に入り優先" },
            ["AutoChoiceWait"] = new[] { "不自动选择", "不自動選擇", "Never choose automatically", "자동 선택 안 함", "自動選択しない" },
            ["FullInventoryStrategy"] = new[] { "背包满时", "背包滿時", "When inventory is full", "인벤토리가 가득 찼을 때", "インベントリ満杯時" },
            ["FullInventoryWait"] = new[] { "等待处理", "等待處理", "Wait", "대기", "待機" },
            ["FullInventoryCharm"] = new[] { "仅替换未生效低品质神器", "僅替換未生效低品質神器", "Inactive low-rarity Charms", "비활성 낮은 등급 부적", "未発動・低レア護符のみ" },
            ["FullInventoryOrdinary"] = new[] { "必要时替换其他物品", "必要時替換其他物品", "Replace other items if needed", "필요하면 다른 아이템 교체", "必要なら他のアイテムも交換" },
            ["FullInventoryHelp"] = new[] { "仅在领取神器或石板且背包已满时生效。优先替换未收藏、未生效且品质较低的神器；选择“必要时替换其他物品”后，可能丢出任意可丢弃的单件物品。本层不会自动捡回。", "僅在領取神器或石板且背包已滿時生效。優先替換未收藏、未生效且品質較低的神器；選擇「必要時替換其他物品」後，可能丟出任意可丟棄的單件物品。本層不會自動撿回。", "Applies only when claiming a Charm or Tablet with a full inventory. It prefers unfavorited, inactive, lower-rarity Charms; Replace other items may drop any legal single item. Autoplay will not pick it up again on that floor.", "인벤토리가 가득 찬 상태에서 부적이나 석판을 받을 때만 적용됩니다. 즐겨찾기가 아니며 비활성인 낮은 등급 부적을 우선하고, 다른 아이템 교체는 버릴 수 있는 단일 아이템을 떨어뜨릴 수 있습니다. 해당 층에서는 다시 줍지 않습니다.", "満杯時に護符または石板を受け取る場合のみ適用します。お気に入りでない未発動の低レア護符を優先し、他アイテム交換では廃棄可能な単品を落とす場合があります。そのフロアでは拾い直しません。" },
            ["RewardPresets"] = new[] { "奖励优先级", "獎勵優先級", "Reward priority", "보상 우선순위", "報酬優先順位" },
            ["RewardPresetsHelp"] = new[] { "选择物品或分类并调整顺序；没有匹配时，从最高品质选项中随机选择。", "選擇物品或分類並調整順序；沒有符合時，從最高品質選項中隨機選擇。", "Choose items or categories and order them by priority; without a match, choose randomly from the highest rarity.", "아이템이나 분류를 선택하고 우선순위를 정하세요. 일치하지 않으면 최고 등급 중 무작위로 선택합니다.", "アイテムまたはカテゴリを選び優先順を設定します。一致しない場合は最高レアリティからランダムに選びます。" },
            ["WeaponPresets"] = new[] { "武器强化目标", "武器強化目標", "Weapon upgrade targets", "무기 강화 목표", "武器強化目標" },
            ["WeaponPresetsHelp"] = new[] { "可直接选择最终强化目标，自动游玩会选择对应的前置强化。当前选项未出现目标时，将消耗可用骰子刷新；仍未出现则放弃本次强化。", "可直接選擇最終強化目標，自動遊玩會選擇對應的前置強化。目前選項未出現目標時，將消耗可用骰子刷新；仍未出現則放棄本次強化。", "Choose a final upgrade target directly; autoplay follows its prerequisite branch. If it is missing, available dice are spent on rerolls before giving up that Anvil.", "최종 강화 목표를 바로 선택할 수 있으며 자동 플레이가 선행 강화를 선택합니다. 목표가 없으면 사용 가능한 주사위로 재추첨한 뒤 포기합니다.", "最終強化目標を直接選べ、自動プレイが前提強化を選びます。目標が出なければ使用可能なダイスで再抽選し、それでも無ければ諦めます。" },
            ["MiraclePresets"] = new[] { "奇迹优先级", "奇蹟優先級", "Miracle priority", "기적 우선순위", "奇跡優先順位" },
            ["MiraclePresetsHelp"] = new[] { "按顺序选择奇迹；列表为空时跳过。当前选项没有匹配时会消耗可用骰子刷新，骰子耗尽仍未出现则放弃本次奇迹。", "依順序選擇奇蹟；清單為空時略過。目前選項沒有符合時會消耗可用骰子刷新，骰子耗盡仍未出現則放棄本次奇蹟。", "Choose Miracles in priority order; an empty list skips them. Missing presets consume available dice on rerolls, then leave the Miracle unclaimed if none appears.", "우선순위대로 기적을 선택하며 목록이 비어 있으면 건너뜁니다. 목표가 없으면 가능한 주사위를 모두 사용해 재추첨하고, 그래도 나오지 않으면 기적을 포기합니다.", "優先順に奇跡を選び、リストが空ならスキップします。目標がなければ使用可能なダイスで再抽選し、それでも出なければ奇跡を見送ります。" },
            ["WeaponPresetCurrent"] = new[] { "当前武器：{0} · 全部后续强化", "目前武器：{0} · 全部後續強化", "Current weapon: {0} · all later upgrades", "현재 무기: {0} · 모든 후속 강화", "現在の武器：{0} · 全後続強化" },
            ["WeaponPresetTier"] = new[] { "第 {0} 阶强化 · {1}", "第 {0} 階強化 · {1}", "Upgrade {0} · {1}", "{0}차 강화 · {1}", "第{0}段階強化 · {1}" },
            ["WeaponPresetNoWeapon"] = new[] { "装备武器后显示可选强化。", "裝備武器後顯示可選強化。", "Equip a weapon to show its upgrades.", "무기를 장착하면 강화 선택지가 표시됩니다.", "武器を装備すると強化候補が表示されます。" },
            ["WeaponPresetMaxed"] = new[] { "当前武器已强化至最高阶。", "目前武器已強化至最高階。", "The current weapon is fully enhanced.", "현재 무기는 최대 강화입니다.", "現在の武器は最大強化済みです。" },
            ["FloorPresets"] = new[] { "下一楼层优先级", "下一樓層優先級", "Next-floor priority", "다음 층 우선순위", "次フロア優先順位" },
            ["FloorPresetsHelp"] = new[] { "有多个可前往楼层时按此处顺序选择；没有匹配时随机选择。武器满强化后避开铁砧，除非只能前往铁砧。", "有多個可前往樓層時依此處順序選擇；沒有符合時隨機選擇。武器滿強化後避開鐵砧，除非只能前往鐵砧。", "When several floors are available, choose in this order; without a match, choose randomly. A fully enhanced weapon avoids Anvils unless they are the only way forward.", "여러 층으로 갈 수 있으면 이 순서로 선택하며 일치하지 않으면 무작위로 선택합니다. 최대 강화 후에는 모루만 가능할 때를 제외하고 피합니다.", "複数の行き先がある場合この順で選び、一致しなければランダムに選びます。最大強化後は金床しか進めない場合を除き避けます。" },
            ["AddPreset"] = new[] { "添加", "新增", "Add", "추가", "追加" },
            ["NoPresetSelected"] = new[] { "尚未选择", "尚未選擇", "Nothing selected", "선택 없음", "未選択" },
            ["PresetDataUnavailable"] = new[] { "载入游戏后显示可选内容。", "載入遊戲後顯示可選內容。", "Choices appear after loading the game.", "게임을 불러오면 선택지가 표시됩니다.", "ゲームを読み込むと選択肢が表示されます。" },
            ["CategoryPrefix"] = new[] { "分类：", "分類：", "Category: ", "분류: ", "カテゴリ：" },
            ["RewardPresetTab"] = new[] { "奖励", "獎勵", "Rewards", "보상", "報酬" },
            ["WeaponPresetTab"] = new[] { "武器升级", "武器升級", "Weapon", "무기 강화", "武器強化" },
            ["MiraclePresetTab"] = new[] { "奇迹", "奇蹟", "Miracle", "기적", "奇跡" },
            ["FloorPresetTab"] = new[] { "下一楼层", "下一樓層", "Next floor", "다음 층", "次フロア" },
            ["PlayerDown"] = new[] { "{0} 已倒地，需要救援！", "{0} 已倒地，需要救援！", "{0} is down and needs rescue!", "{0}님이 쓰러졌습니다. 구조가 필요합니다!", "{0}がダウンしました。救援が必要です！" },
            ["RescueRequested"] = new[] { "紧急：{0} 正在请求救援！", "緊急：{0} 正在請求救援！", "URGENT: {0} is requesting rescue!", "긴급: {0}님이 구조를 요청합니다!", "緊急：{0}が救援を要請しています！" },
            ["UnknownPlayer"] = new[] { "未知玩家", "未知玩家", "Unknown player", "알 수 없는 플레이어", "不明なプレイヤー" },
            ["NoData"] = new[] { "暂无数据。", "暫無資料。", "No data yet.", "아직 데이터가 없습니다.", "データはまだありません。" },
            ["DownloadHelp"] = new[] { "从 GitHub Release 获取最新版。打开链接后可查看版本说明和校验值。", "從 GitHub Release 取得最新版。開啟連結後可查看版本說明和校驗值。", "Get the latest build from GitHub Releases. The release page includes notes and checksums.", "GitHub Release에서 최신 버전을 받으세요. 릴리스 페이지에 변경 사항과 해시가 있습니다.", "GitHub Releaseから最新版を取得できます。リリースページに変更内容とハッシュがあります。" },
            ["OpenReleasePage"] = new[] { "打开 Release 页面", "開啟 Release 頁面", "Open release page", "릴리스 페이지 열기", "リリースページを開く" },
            ["OpenPluginDownload"] = new[] { "下载插件 ZIP", "下載插件 ZIP", "Download plugin ZIP", "플러그인 ZIP 다운로드", "プラグインZIPをダウンロード" },
            ["ClientModOutdated"] = new[] { "你的 Sephiria Together 版本过旧：{0}\n房主版本：{1}。请更新后重新加入。", "你的 Sephiria Together 版本過舊：{0}\n房主版本：{1}。請更新後重新加入。", "Your Sephiria Together is outdated: {0}\nHost version: {1}. Update and rejoin.", "Sephiria Together 버전이 오래되었습니다: {0}\n호스트 버전: {1}. 업데이트 후 다시 참가하세요.", "Sephiria Togetherが古いです：{0}\nホスト版：{1}。更新後に再参加してください。" },
            ["HostModOutdated"] = new[] { "房主的 Sephiria Together 版本较旧：{1}\n你的版本：{0}。自定义联机功能已停用。", "房主的 Sephiria Together 版本較舊：{1}\n你的版本：{0}。自訂連線功能已停用。", "The host has an older Sephiria Together: {1}\nYour version: {0}. Custom network features are disabled.", "호스트의 Sephiria Together 버전이 오래되었습니다: {1}\n내 버전: {0}. 사용자 지정 네트워크 기능이 비활성화됩니다.", "ホストのSephiria Togetherが古いです：{1}\n自分の版：{0}。独自ネットワーク機能は無効です。" },
            ["HostCompensation"] = new[] { "错过的可选奖励由房主记录。未安装本 Mod 的玩家暂时无法领取，但次数会保留。", "錯過的可選獎勵由房主記錄。未安裝本 Mod 的玩家暫時無法領取，但次數會保留。", "The host records missed choice rewards. Players without the mod cannot claim them yet, but their uses are preserved.", "놓친 선택 보상은 호스트가 기록합니다. 모드가 없는 플레이어는 당장 받을 수 없지만 횟수는 유지됩니다.", "取り逃した選択報酬はホストが記録します。Mod未導入では受取できませんが、回数は保持されます。" },
            ["ClientWaiting"] = new[] { "正在等待玩家与联机信息……", "正在等待玩家與連線資訊……", "Waiting for player and session information...", "플레이어와 세션 정보를 기다리는 중...", "プレイヤーとセッション情報を待っています…" },
            ["ClientCompensation"] = new[] { "中途加入补偿", "途中加入補償", "Mid-run join compensation", "중도 참가 보상", "途中参加補償" },
            ["ClientAutoGranted"] = new[] { "加入时自动补发经验、金币、骰子和错过的背包扩充；首次加入还会补发维度口袋物品。下方显示仍可领取的奖励。", "加入時自動補發經驗、金幣、骰子和錯過的背包擴充；首次加入還會補發維度口袋物品。下方顯示仍可領取的獎勵。", "Joining grants missed EXP, money, dice, and inventory expansions; first-time joins also receive Dimension Pocket items. Remaining rewards appear below.", "참가 시 놓친 경험치, 돈, 주사위와 인벤토리 확장을 지급하며, 최초 참가에는 차원 주머니 아이템도 지급합니다. 남은 보상은 아래에 표시됩니다.", "参加時に経験値、お金、ダイス、未取得の拡張を補償し、初回は次元ポケット品も付与します。残りの報酬は以下に表示されます。" },
            ["WeaponCredits"] = new[] { "待领取武器强化", "待領取武器強化", "Weapon upgrades available", "받을 무기 강화", "受取可能な武器強化" },
            ["WeaponAnvilHelp"] = new[] { "附近会出现一座仅你可用的铁砧，可正常消耗骰子刷新。", "附近會出現一座僅你可用的鐵砧，可正常消耗骰子刷新。", "A personal Anvil appears nearby and can reroll normally with dice.", "근처에 본인 전용 모루가 나타나며 주사위로 정상 재추첨할 수 있습니다.", "近くに本人専用の金床が現れ、ダイスで通常どおり再抽選できます。" },
            ["EnchantObjectHelp"] = new[] { "附近会出现一座仅你可用的附魔祭坛。", "附近會出現一座僅你可用的附魔祭壇。", "A personal Enchant altar appears nearby.", "근처에 본인 전용 인챈트 제단이 나타납니다.", "近くに本人専用のエンチャント祭壇が現れます。" },
            ["MiracleObjectHelp"] = new[] { "附近会出现一个仅你可用的奇迹选择装置，可正常刷新。", "附近會出現一個僅你可用的奇蹟選擇裝置，可正常刷新。", "A personal Miracle choice appears nearby and can reroll normally.", "근처에 본인 전용 기적 선택 장치가 나타나며 재추첨할 수 있습니다.", "近くに本人専用の奇跡選択装置が現れ、再抽選できます。" },
            ["SephiriteObjectHelp"] = new[] { "附近会出现一个仅你可用的奖励选择物。", "附近會出現一個僅你可用的獎勵選擇物。", "A personal reward choice appears nearby.", "근처에 본인 전용 보상 선택지가 나타납니다.", "近くに本人専用の報酬選択が現れます。" },
            ["BossObjectHelp"] = new[] { "将为你补发首领奖励选项。", "將為你補發首領獎勵選項。", "Boss reward choices will be restored for you.", "보스 보상 선택지가 지급됩니다.", "ボス報酬の選択肢が補償されます。" },
            ["FusionObjectHelp"] = new[] { "附近会出现一个仅你可用的石板合成装置；仍会正常消耗金币，成功后扣除一次补偿。", "附近會出現一個僅你可用的石板合成裝置；仍會正常消耗金幣，成功後扣除一次補償。", "A personal Tablet combiner appears nearby. It still costs money and consumes one use only after success.", "근처에 본인 전용 석판 합성 장치가 나타납니다. 비용은 그대로이며 성공 후 1회 차감됩니다.", "近くに本人専用の石板合成装置が現れます。費用は通常どおりで、成功後に1回消費します。" },
            ["EnchantCredits"] = new[] { "待领取附魔", "待領取附魔", "Enchants available", "받을 인챈트", "受取可能なエンチャント" },
            ["MiracleCredits"] = new[] { "待领取奇迹", "待領取奇蹟", "Miracles available", "받을 기적", "受取可能な奇跡" },
            ["CharmCredits"] = new[] { "待领取{CHARM}", "待領取{CHARM}", "{CHARM} rewards available", "받을 {CHARM}", "受取可能な{CHARM}" },
            ["TabletCredits"] = new[] { "待领取{TABLET}", "待領取{TABLET}", "{TABLET} rewards available", "받을 {TABLET}", "受取可能な{TABLET}" },
            ["BossCredits"] = new[] { "待领取首领奖励", "待領取首領獎勵", "Boss rewards available", "받을 보스 보상", "受取可能なボス報酬" },
            ["FusionCredits"] = new[] { "待使用石板合成", "待使用石板合成", "Tablet combinations available", "사용할 석판 합성", "使用可能な石板合成" },
            ["ClaimPending"] = new[] { "正在确认领取……", "正在確認領取……", "Confirming claim...", "수령 확인 중...", "受取を確認中…" },
            ["ClaimSuccess"] = new[] { "上一次领取已确认。", "上一次領取已確認。", "Last claim confirmed.", "마지막 수령이 확인되었습니다.", "前回の受取を確認しました。" },
            ["ClaimRejected"] = new[] { "领取失败：剩余次数不足、物品状态已变化或选择无效。", "領取失敗：剩餘次數不足、物品狀態已變更或選擇無效。", "Claim failed: no uses remain, the item changed, or the choice is invalid.", "수령 실패: 남은 횟수가 없거나 아이템 상태가 바뀌었거나 선택이 잘못되었습니다.", "受取失敗：残り回数不足、アイテム状態変更、または無効な選択です。" },
            ["ClaimHistory"] = new[] { "本局已领取：武器强化 {0}，附魔 {1}，奇迹 {2}，{TABLET} {3}，首领 {4}，{CHARM} {5}，石板合成 {6}", "本局已領取：武器強化 {0}，附魔 {1}，奇蹟 {2}，{TABLET} {3}，首領 {4}，{CHARM} {5}，石板合成 {6}", "Claimed: weapon {0}, enchant {1}, miracle {2}, {TABLET} {3}, boss {4}, {CHARM} {5}, Tablet combine {6}", "수령: 무기 강화 {0}, 인챈트 {1}, 기적 {2}, {TABLET} {3}, 보스 {4}, {CHARM} {5}, 석판 합성 {6}", "受取済み：武器強化{0}、付与{1}、奇跡{2}、{TABLET}{3}、ボス{4}、{CHARM}{5}、石板合成{6}" },
            ["ClientMissingRewards"] = new[] { "最大生命与蓝宝石奖励也会自动补发。未安装本 Mod 时，可选奖励次数会保留。", "最大生命與藍寶石獎勵也會自動補發。未安裝本 Mod 時，可選獎勵次數會保留。", "Max HP and Sapphire rewards are also granted automatically. Choice rewards remain saved without the mod.", "최대 체력과 사파이어 보상도 자동 지급됩니다. 모드가 없어도 선택 보상 횟수는 유지됩니다.", "最大HPとサファイア報酬も自動補償されます。Mod未導入でも選択報酬回数は保持されます。" },
            ["NextSpawn"] = new[] { "房间人数下次建房生效；敌人难度只影响新生成的敌人与后续波次。", "房間人數下次建房生效；敵人難度只影響新生成的敵人與後續波次。", "Player limit applies to the next lobby; enemy difficulty affects only new enemies and later waves.", "방 인원은 다음 방부터 적용되며 적 난이도는 새 적과 이후 웨이브에만 적용됩니다.", "人数上限は次のルームから、敵難易度は新規敵と以降のウェーブにのみ適用されます。" },
            ["Multiplayer"] = new[] { "多人联机", "多人連線", "Multiplayer", "멀티플레이", "マルチプレイ" },
            ["ResumeLobby"] = new[] { "为当前进度建房", "為目前進度建房", "Host the current run", "현재 진행도로 방 만들기", "現在の進行でルーム作成" },
            ["ResumeLobbyHelp"] = new[] { "继续地牢存档后，可从游戏的建房界面开放当前进度；建房不会离开当前楼层。", "繼續地牢存檔後，可從遊戲的建房介面開放目前進度；建房不會離開目前樓層。", "After continuing a dungeon save, host the current run from the game lobby screen without leaving the floor.", "던전 저장을 이어한 뒤 현재 층을 떠나지 않고 게임 방 생성 화면에서 공개할 수 있습니다.", "ダンジョン再開後、現在のフロアを離れずゲームのルーム画面から公開できます。" },
            ["PlayerLimit"] = new[] { "房间人数上限（2-250，下次建房生效）", "房間人數上限（2-250，下次建房生效）", "Player limit (2-250, next lobby)", "방 인원 상한 (2-250, 다음 방)", "ルーム人数上限（2-250、次回）" },
            ["Apply"] = new[] { "设置上限", "設定上限", "Set limit", "상한 설정", "上限を設定" },
            ["LowerProgress"] = new[] { "允许低进度玩家加入", "允許低進度玩家加入", "Allow lower-progress players", "진행도가 낮은 플레이어 허용", "進行度が低いプレイヤーを許可" },
            ["MidRun"] = new[] { "允许中途加入", "允許中途加入", "Allow fresh mid-run joining", "게임 중 신규 참가 허용", "途中からの新規参加を許可" },
            ["UngroupedTransition"] = new[] { "进入下一关无需全员集合", "進入下一關無需全員集合", "Do not require everyone at the entrance", "다음 스테이지에서 전원 집합 불필요", "次のステージで全員集合を不要にする" },
            ["UngroupedTransitionHelp"] = new[] { "开启后，只需房主在入口互动，即可让全队前往下一关。", "開啟後，只需房主在入口互動，即可讓全隊前往下一關。", "Only the host needs to use the entrance to move the whole party onward.", "호스트만 입구를 사용하면 파티 전체가 다음 구역으로 이동합니다.", "ホストだけが入口を使用すれば、パーティー全員が次へ進みます。" },
            ["BreathingHeal"] = new[] { "受伤后延迟恢复生命", "受傷後延遲恢復生命", "Delayed healing after damage", "피해 후 지연 회복", "ダメージ後の遅延回復" },
            ["BreathingHealHelp"] = new[] { "受伤后暂停恢复 10 秒，之后每秒恢复 1 点生命；战斗中仍会恢复，倒地时停止。", "受傷後暫停恢復 10 秒，之後每秒恢復 1 點生命；戰鬥中仍會恢復，倒地時停止。", "After taking damage, healing pauses for 10 seconds, then restores 1 HP each second, including during combat. It stops while down.", "피해 후 10초 동안 회복이 멈추고 이후 전투 중에도 초당 1 체력을 회복합니다. 쓰러지면 중단됩니다.", "被ダメージ後10秒間停止し、その後は戦闘中も毎秒1HP回復します。ダウン中は停止します。" },
            ["AutoReviveWhenClear"] = new[] { "战斗结束后自动复活队友", "戰鬥結束後自動復活隊友", "Auto-revive after combat", "전투 종료 후 자동 부활", "戦闘終了後に自動蘇生" },
            ["AutoReviveWhenClearHelp"] = new[] { "没有存活的敌对单位，且存活玩家脱离战斗 2 秒后，以 50% 最大生命复活所有倒地玩家。", "沒有存活的敵對單位，且存活玩家脫離戰鬥 2 秒後，以 50% 最大生命復活所有倒地玩家。", "When no hostile units remain and living players are out of combat for two seconds, revive everyone downed at 50% max HP.", "적대 유닛이 없고 생존자가 2초 동안 전투에서 벗어나면 쓰러진 모두를 최대 체력 50%로 부활시킵니다.", "敵対ユニットが残らず、生存者が2秒間戦闘外になると、ダウン中の全員を最大HP50%で蘇生します。" },
            ["FriendlyFire"] = new[] { "允许伤害队友", "允許傷害隊友", "Allow friendly fire", "아군 피해 허용", "味方への攻撃を許可" },
            ["FriendlyFireHelp"] = new[] { "仅玩家攻击其他玩家时生效。伤害除以 100，单次最低 1、最高 5 点；格挡仍可完全挡住。", "僅玩家攻擊其他玩家時生效。傷害除以 100，單次最低 1、最高 5 點；格擋仍可完全擋住。", "Only player attacks against other players. Damage is divided by 100, with 1 minimum and 5 maximum; guarding still blocks it.", "플레이어가 다른 플레이어를 공격할 때만 적용됩니다. 피해는 100으로 나누며 최소 1, 최대 5이고 방어로 완전히 막을 수 있습니다.", "プレイヤーが他のプレイヤーを攻撃した場合のみ有効。ダメージを100で割り、最低1、最大5。ガードで完全に防げます。" },
            ["Catchup"] = new[] { "中途加入经验补偿（100%）", "中途加入經驗補償（100%）", "Mid-run EXP catch-up (100%)", "중도 참가 경험치 보상 (100%)", "途中参加EXP補償（100%）" },
            ["EnemyScaling"] = new[] { "敌人难度", "敵人難度", "Enemy difficulty", "적 난이도", "敵難易度" },
            ["ScalingHelp"] = new[] { "选择一个预设难度。增加敌人数时，每波会同时出现更多敌人，但不会增加波数。", "選擇一個預設難度。增加敵人數時，每波會同時出現更多敵人，但不會增加波數。", "Choose a preset difficulty. Enemy-count scaling puts more enemies in each wave without adding phases.", "난이도 프리셋을 선택하세요. 적 수 증가는 페이즈를 늘리지 않고 각 웨이브의 적을 늘립니다.", "プリセット難易度を選択します。敵数増加はフェーズを増やさず、各ウェーブの敵を増やします。" },
            ["VanillaScaling"] = new[] { "游戏原有难度加成", "遊戲原有難度加成", "Game difficulty bonuses", "게임 기본 난이도 보정", "ゲーム本来の難易度補正" },
            ["BossLifesteal"] = new[] { "首领与小首领吸血", "首領與小首領吸血", "Boss and miniboss lifesteal", "보스 및 미니보스 흡혈", "ボス・中ボスの吸血" },
            ["BossLifestealHelp"] = new[] { "控制困难模式“血祭”对首领和小首领的吸血效果。关闭后仅禁用此效果，不影响普通敌人或其他治疗。", "控制困難模式「血祭」對首領和小首領的吸血效果。關閉後僅停用此效果，不影響普通敵人或其他治療。", "Controls Blood Festival lifesteal for Bosses and Minibosses. Disabling it does not affect normal enemies or other healing.", "피의 축제 보스 및 미니보스 흡혈을 제어합니다. 꺼도 일반 적과 다른 회복은 영향을 받지 않습니다.", "血祭のボス・中ボス吸血を制御します。無効化しても通常敵や他の回復には影響しません。" },
            ["CurrentPreset"] = new[] { "当前难度", "目前難度", "Current difficulty", "현재 난이도", "現在の難易度" },
            ["PresetOriginal"] = new[] { "原版", "原版", "Original", "원본", "原版" },
            ["PresetLight"] = new[] { "轻度", "輕度", "Light", "가벼움", "ライト" },
            ["PresetStandard"] = new[] { "标准", "標準", "Standard", "표준", "標準" },
            ["PresetHigh"] = new[] { "高压", "高壓", "High pressure", "고압", "高圧" },
            ["PresetCustom"] = new[] { "自定义", "自訂", "Custom", "사용자 설정", "カスタム" },
            ["ScalingPreviewPlayers"] = new[] { "当前队伍：{0} 人", "目前隊伍：{0} 人", "Current party: {0} players", "현재 파티: {0}명", "現在のパーティー：{0}人" },
            ["PreviewHealth"] = new[] { "新生成敌人的额外生命倍率", "新生成敵人的額外生命倍率", "Extra HP multiplier for new enemies", "새 적 추가 체력 배율", "新規敵の追加HP倍率" },
            ["PreviewCount"] = new[] { "后续波次的敌人数倍率", "後續波次的敵人數倍率", "Enemy-count multiplier for later waves", "이후 웨이브 적 수 배율", "以降ウェーブの敵数倍率" },
            ["ScalingTiming"] = new[] { "修改后只影响新生成的敌人和之后计算的波次，当前已经出现的怪物不变。", "修改後只影響新生成的敵人和之後計算的波次，目前已出現的怪物不變。", "Changes affect newly spawned enemies and future waves. Existing enemies are unchanged.", "변경 사항은 새로 생성되는 적과 이후 웨이브에만 적용됩니다.", "変更は新しく出現する敵と以降のウェーブにのみ反映されます。" },
            ["ShowAdvanced"] = new[] { "展开高级设置", "展開進階設定", "Show advanced settings", "고급 설정 표시", "詳細設定を表示" },
            ["HideAdvanced"] = new[] { "收起高级设置", "收起進階設定", "Hide advanced settings", "고급 설정 숨기기", "詳細設定を隠す" },
            ["Baseline"] = new[] { "不触发额外增强的人数（0 = 单人也增强）", "不觸發額外增強的人數（0 = 單人也增強）", "Players before extra scaling (0 = include solo)", "추가 보정 기준 인원 (0 = 솔로 포함)", "追加補正の基準人数（0 = ソロ含む）" },
            ["ExtraHp"] = new[] { "每名超出基准人数的玩家增加生命", "每名超出基準人數的玩家增加生命", "Extra HP per player above baseline", "기준 초과 인원당 체력", "基準超過1人ごとのHP" },
            ["HpCap"] = new[] { "敌人生命倍率上限", "敵人生命倍率上限", "Enemy HP multiplier cap", "적 체력 배율 상한", "敵HP倍率上限" },
            ["EnemyCount"] = new[] { "增加每波敌人数（不增加波数）", "增加每波敵人數（不增加波數）", "Increase each wave (not phase count)", "웨이브 적 증가 (페이즈 유지)", "各ウェーブ増加（フェーズ数維持）" },
            ["CountPerPlayer"] = new[] { "每名超出基准人数的玩家增加敌人数", "每名超出基準人數的玩家增加敵人數", "Extra enemies per player above baseline", "기준 초과 인원당 적 수", "基準超過1人ごとの敵数" },
            ["CountCap"] = new[] { "怪物数量倍率上限", "怪物數量倍率上限", "Enemy-count multiplier cap", "적 수 배율 상한", "敵数倍率上限" },
            ["CycleHpCap"] = new[] { "切换上限：4x / 8x / 12x / 无上限", "切換上限：4x / 8x / 12x / 無上限", "Cycle cap: 4x / 8x / 12x / none", "상한 변경: 4x / 8x / 12x / 없음", "上限切替：4x / 8x / 12x / なし" },
            ["ToggleOn"] = new[] { "开启", "開啟", "ON", "켜기", "ON" },
            ["ToggleOff"] = new[] { "关闭", "關閉", "OFF", "끄기", "OFF" },
            ["Players"] = new[] { "玩家状态", "玩家狀態", "Player status", "플레이어 상태", "プレイヤー状態" },
            ["Connected"] = new[] { "在线", "在線", "Connected", "접속", "接続中" },
            ["Dead"] = new[] { "已倒地", "已倒地", "Down", "쓰러짐", "ダウン" },
            ["Loading"] = new[] { "加载中", "載入中", "Loading", "로딩 중", "ロード中" },
            ["Host"] = new[] { "房主", "房主", "Host", "호스트", "ホスト" },
            ["Level"] = new[] { "等级", "等級", "Lv", "레벨", "Lv" },
            ["Floor"] = new[] { "楼层", "樓層", "Floor", "층", "フロア" },
            ["RouteFloor"] = new[] { "路线第 {0} 层", "路線第 {0} 層", "Route floor {0}", "경로 {0}층", "ルート第{0}層" },
            ["CurrentRoom"] = new[] { "当前楼层", "目前樓層", "Current floor", "현재 층", "現在のフロア" },
            ["RoomBoss"] = new[] { "首领楼层", "首領樓層", "Boss floor", "보스 층", "ボスフロア" },
            ["RoomMiniBoss"] = new[] { "小首领楼层", "小首領樓層", "Miniboss floor", "미니보스 층", "中ボスフロア" },
            ["RoomHardBattle"] = new[] { "高难战斗楼层", "高難戰鬥樓層", "Hard battle floor", "고난도 전투 층", "高難度戦闘フロア" },
            ["RoomBattle"] = new[] { "战斗楼层", "戰鬥樓層", "Battle floor", "전투 층", "戦闘フロア" },
            ["RoomMoney"] = new[] { "金币奖励", "金幣獎勵", "Money reward", "골드 보상", "ゴールド報酬" },
            ["RoomExp"] = new[] { "经验奖励", "經驗獎勵", "EXP reward", "경험치 보상", "経験値報酬" },
            ["RoomHeal"] = new[] { "治疗房", "治療房", "Healing room", "회복방", "回復部屋" },
            ["RoomMerchant"] = new[] { "商店", "商店", "Merchant", "상점", "ショップ" },
            ["RoomMiracle"] = new[] { "奇迹楼层", "奇蹟樓層", "Miracle floor", "기적 층", "奇跡フロア" },
            ["RoomCharm"] = new[] { "{CHARM}奖励", "{CHARM}獎勵", "{CHARM} reward", "{CHARM} 보상", "{CHARM}報酬" },
            ["RoomTablet"] = new[] { "{TABLET}奖励", "{TABLET}獎勵", "{TABLET} reward", "{TABLET} 보상", "{TABLET}報酬" },
            ["RoomEnchant"] = new[] { "附魔楼层", "附魔樓層", "Enchant floor", "인챈트 층", "エンチャントフロア" },
            ["RoomEncounter"] = new[] { "随机事件", "隨機事件", "Random encounter", "무작위 이벤트", "ランダムイベント" },
            ["RoomAnvil"] = new[] { "铁砧楼层", "鐵砧樓層", "Anvil floor", "모루 층", "金床フロア" },
            ["RoomDice"] = new[] { "骰子奖励", "骰子獎勵", "Dice reward", "주사위 보상", "ダイス報酬" },
            ["RoomSapphire"] = new[] { "蓝宝石奖励", "藍寶石獎勵", "Sapphire reward", "사파이어 보상", "サファイア報酬" },
            ["RoomMaxHp"] = new[] { "最大生命奖励", "最大生命獎勵", "Max HP reward", "최대 HP 보상", "最大HP報酬" },
            ["RoomInventory"] = new[] { "背包扩充", "背包擴充", "Inventory expansion", "인벤토리 확장", "インベントリ拡張" },
            ["Kick"] = new[] { "踢出", "踢出", "Kick", "추방", "キック" },
            ["Save"] = new[] { "保存设置", "儲存設定", "Save settings", "설정 저장", "設定を保存" },
            ["Close"] = new[] { "关闭", "關閉", "Close", "닫기", "閉じる" }
            ,["RuleGameVersion"] = new[] { "游戏版本：{0}", "遊戲版本：{0}", "Game version: {0}", "게임 버전: {0}", "ゲームバージョン：{0}" }
            ,["RuleMidRunJoin"] = new[] { "允许中途加入：{0}", "允許中途加入：{0}", "Mid-run joining: {0}", "중도 참가: {0}", "途中参加：{0}" }
            ,["RuleLowerProgress"] = new[] { "允许低进度加入：{0}", "允許低進度加入：{0}", "Lower-progress joining: {0}", "낮은 진행도 참가: {0}", "低進行度参加：{0}" }
            ,["RuleUngrouped"] = new[] { "无需全员到场即可前往下一关：{0}", "無需全員到場即可前往下一關：{0}", "Entrance does not require everyone: {0}", "전원 집합 없이 진행: {0}", "全員集合なしで進行：{0}" }
            ,["RuleFriendlyFire"] = new[] { "允许伤害队友：{0}", "允許傷害隊友：{0}", "Friendly fire: {0}", "아군 피해: {0}", "味方への攻撃：{0}" }
            ,["RuleHealing"] = new[] { "受伤后延迟恢复生命：{0}", "受傷後延遲恢復生命：{0}", "Delayed healing: {0}", "피해 후 지연 회복: {0}", "ダメージ後の遅延回復：{0}" }
            ,["RuleAutoRevive"] = new[] { "战斗结束后自动复活：{0}", "戰鬥結束後自動復活：{0}", "Auto-revive after combat: {0}", "전투 후 자동 부활: {0}", "戦闘後の自動蘇生：{0}" }
            ,["RuleExpCatchup"] = new[] { "中途加入经验补偿：{0}", "中途加入經驗補償：{0}", "Mid-run EXP catch-up: {0}", "중도 참가 경험치 보상: {0}", "途中参加EXP補償：{0}" }
            ,["RuleEnemyHp"] = new[] { "敌人生命：每名超出 {0} 人的玩家 +{1}，上限 {2}", "敵人生命：每名超出 {0} 人的玩家 +{1}，上限 {2}", "Enemy HP: +{1} per player above {0}, cap {2}", "적 체력: {0}명 초과 인원당 +{1}, 상한 {2}", "敵HP：{0}人超過ごとに+{1}、上限{2}" }
            ,["RuleEnemyCount"] = new[] { "每波敌人数：{0}，每名超额玩家 +{1}，上限 {2}x", "每波敵人數：{0}，每名超額玩家 +{1}，上限 {2}x", "Enemies per wave: {0}, +{1} per extra player, cap {2}x", "웨이브 적 수: {0}, 초과 인원당 +{1}, 상한 {2}x", "各ウェーブ敵数：{0}、超過1人ごとに+{1}、上限{2}x" }
            ,["RuleBossLifesteal"] = new[] { "首领与小首领吸血：{0}", "首領與小首領吸血：{0}", "Boss/miniboss lifesteal: {0}", "보스/미니보스 흡혈: {0}", "ボス・中ボス吸血：{0}" }
            ,["RulePlayerLimit"] = new[] { "房间人数上限：{0}", "房間人數上限：{0}", "Player limit: {0}", "방 인원 상한: {0}", "ルーム人数上限：{0}" }
            ,["DiagnosticProtocol"] = new[] { "Mod 版本：{0}", "Mod 版本：{0}", "Mod version: {0}", "Mod 버전: {0}", "Modバージョン：{0}" }
            ,["DiagnosticServer"] = new[] { "房主服务：{0}", "房主服務：{0}", "Server active: {0}", "서버 활성: {0}", "サーバー稼働：{0}" }
            ,["DiagnosticClient"] = new[] { "本地客户端：{0}", "本地客戶端：{0}", "Client active: {0}", "클라이언트 활성: {0}", "クライアント稼働：{0}" }
            ,["DiagnosticHandshake"] = new[] { "Mod 联机确认：{0}", "Mod 連線確認：{0}", "Mod handshake: {0}", "Mod 연결 확인: {0}", "Mod接続確認：{0}" }
            ,["DiagnosticConnection"] = new[] { "连接编号：{0}", "連線編號：{0}", "Connection ID: {0}", "연결 번호: {0}", "接続番号：{0}" }
            ,["DiagnosticPlayer"] = new[] { "玩家标识：{0}", "玩家標識：{0}", "Player ID: {0}", "플레이어 식별자: {0}", "プレイヤー識別子：{0}" }
            ,["DiagnosticFloor"] = new[] { "当前楼层：{0}", "目前樓層：{0}", "Current floor: {0}", "현재 층: {0}", "現在のフロア：{0}" }
            ,["DiagnosticPlayers"] = new[] { "当前玩家数：{0}", "目前玩家數：{0}", "Players: {0}", "현재 플레이어: {0}", "現在のプレイヤー数：{0}" }
            ,["VanillaHpSummary"] = new[] { "游戏多人生命加成：普通敌人每人 +{0}%，小首领 +{1}%，首领 +{2}%", "遊戲多人生命加成：普通敵人每人 +{0}%，小首領 +{1}%，首領 +{2}%", "Game multiplayer HP: normal +{0}%, miniboss +{1}%, boss +{2}% per player", "게임 멀티 체력: 일반 +{0}%, 미니보스 +{1}%, 보스 +{2}%", "ゲームのマルチHP：通常+{0}%、中ボス+{1}%、ボス+{2}%" }
            ,["HardModeSummary"] = new[] { "困难模式：{0} 点，{1} +{2}% 生命，{3} +{4}% 伤害", "困難模式：{0} 點，{1} +{2}% 生命，{3} +{4}% 傷害", "Hard mode: {0} points, {1} +{2}% HP, {3} +{4}% damage", "하드 모드: {0}점, {1} +{2}% 체력, {3} +{4}% 피해", "ハードモード：{0}点、{1}+{2}%HP、{3}+{4}%ダメージ" }
            ,["BloodFestivalSummary"] = new[] { "{0}：等级 {1}，首领基础 {2}%，当前每次命中玩家恢复 {3}% 最大生命", "{0}：等級 {1}，首領基礎 {2}%，目前每次命中玩家恢復 {3}% 最大生命", "{0}: level {1}, boss base {2}%, current heal {3}% max HP per player hit", "{0}: 레벨 {1}, 보스 기본 {2}%, 플레이어 적중당 최대 체력 {3}% 회복", "{0}：Lv{1}、ボス基礎{2}%、プレイヤー命中ごとに最大HP{3}%回復" }
            ,["ScalingDataUnavailable"] = new[] { "暂时无法读取游戏难度数据。", "暫時無法讀取遊戲難度資料。", "Game difficulty data is unavailable.", "게임 난이도 데이터를 읽을 수 없습니다.", "ゲーム難易度データを取得できません。" }
            ,["NoLimit"] = new[] { "无上限", "無上限", "none", "없음", "なし" }
            ,["HardModeTenacious"] = new[] { "坚韧之躯", "堅韌之軀", "Tenacious Body", "끈질긴 육체", "強靭な肉体" }
            ,["HardModeFerocious"] = new[] { "凶猛利爪", "兇猛利爪", "Ferocious Claws", "사나운 발톱", "獰猛な爪" }
            ,["HardModeBloodFestival"] = new[] { "血祭", "血祭", "Blood Festival", "피의 축제", "血祭" }
        };

        internal static string Get(string key)
        {
            int language = 2;
            string current = LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguage : "en-US";
            if (current == "zh-CN") language = 0;
            else if (current == "zh-TW") language = 1;
            else if (current == "ko-KR") language = 3;
            else if (current == "ja-JP") language = 4;
            string value = Text.TryGetValue(key, out string[] values) ? values[language] : key;
            string[] charmFallback = { "神器", "神器", "Charm", "부적", "護符" };
            string[] tabletFallback = { "石板", "石板", "Stone Tablet", "석판", "石板" };
            return value.Replace("{CHARM}", OfficialItemType(EItemType.Charm, charmFallback[language]))
                .Replace("{TABLET}", OfficialItemType(EItemType.StoneTablet, tabletFallback[language]));
        }

        private static string OfficialItemType(EItemType type, string fallback)
        {
            try
            {
                string value = ItemDatabase.GetItemTypeName(type)?.ToString();
                return string.IsNullOrWhiteSpace(value) || value.StartsWith("ItemType_") ? fallback : value;
            }
            catch
            {
                return fallback;
            }
        }
    }
}
