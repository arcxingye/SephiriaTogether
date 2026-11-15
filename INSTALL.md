# Installation / 安装说明

## 游戏 1.1.3 / 创意工坊更新

本版适配游戏 1.1.3，提供 BepInEx 与原生工坊两种构建。BepInEx 版安装位置为 `BepInEx/plugins/SephiriaTogether.dll`；原生版通过游戏的工坊加载器运行。
其他创意工坊订阅在游戏原生面板管理。Steam 联机保留原版 Mod 列表、版本及加载顺序检查；“允许低进度加入”只放宽主线章节要求。
IP 联机前请手动统一双方所需的工坊 Mod、版本与加载顺序；原版对已加载本地工坊包的联机限制仍然生效。

## 原生工坊版 / 本地上传包

运行 `scripts/build-workshop.ps1 -GameDir "游戏目录"` 生成 `artifacts/WorkshopLocal/SephiriaTogether`。
将整个 `SephiriaTogether` 文件夹放入游戏目录的 `WorkshopLocal`，在标题界面的创意工坊面板选中本地条目并上传。

- 原生版无需 BepInEx，使用游戏自带 Harmony 与原生配置存储。
- 使用前停用旧的 `BepInEx/plugins/SephiriaTogether.dll` 并重启；不要同时启用两种构建。
- 进入游戏后按 F8 设置。工坊配置与旧 `.cfg` 独立保存，首次切换需要重新确认参数。
- 与背包自动整理共用 F8 时，Together 会临时使用 F9，并显示提示；诊断页可查看实际菜单键。只启用一个整理版本，避免旧 BepInEx 整理与工坊整理的同名程序集冲突。
- Star's SephiriaMod 必须全员安装并统一版本和顺序。Together 的“仅房主需要安装”不代表其他内容 Mod 也可以只装房主端。详见 `COMPATIBILITY.md`。
- 游戏 1.1.3 在进入存档时才激活工坊 DLL，因此刚启动的首次主菜单不能使用工坊版 F8。激活后运行组件会常驻；BepInEx 版则在启动阶段初始化。
- 工坊直连准备：进入一次存档 → F8 保存 IP 模式和默认端口 → 点击“退出当前会话，回标题应用”并按原版确认退出 → 在标题页确认“当前实际模式”已匹配 → 再进存档建房或加入。无需重启程序；客机在地牢中会走原版结算流程。
- 每次冷启动都会重新经历工坊晚加载。若原版自身无法进入存档，原生工坊代码也无法提前启用直连，这类启动需求需使用 BepInEx 版。
- 上传工具会保存 `uploadticket.json`，后续更新请保留它。DLL 包需要经过游戏的代码审核流程。
- 本地包适合单机加载检查，正式联机测试使用审核后可加载的订阅版，并停用本地副本后重启。

详细说明见 `workshop/README.md`。

## 新手整合包

下载文件名包含 `with-BepInEx` 的 ZIP。

1. 在 Steam 中右键 Sephiria，选择“管理” -> “浏览本地文件”。
2. 打开 ZIP，把其中所有文件直接拖到游戏根目录。
3. 正确安装后，`winhttp.dll` 应当和 `Sephiria.exe` 位于同一个目录。
4. 启动游戏。第一次启动时 BepInEx 会创建配置和缓存，可能比平时稍慢。
5. 房主进入多人游戏后按 `F8` 打开 Sephiria Together 菜单。

离线环境会自动使用 IP 传输。选择存档进入大厅后，在原版多人面板确认创建房间；客机可刷新局域网列表，或点击加入并输入 `IP:端口`。默认游戏端口为 TCP `7777`，发现端口为 UDP `7780`，请在防火墙中放行。

不要把整个 ZIP 放进 `BepInEx/plugins`，也不要把文件放进 `Sephiria_Data`。

## 已安装 BepInEx

下载普通 ZIP 或单独的 `SephiriaTogether.dll`，把 DLL 放入：

```text
Sephiria/BepInEx/plugins/SephiriaTogether.dll
```

## Beginner bundle

Both builds target Sephiria 1.1.3. The beginner bundle below uses BepInEx; keep `SephiriaTogether.dll` in `BepInEx/plugins`. The native package uses `SephiriaTogether.Workshop.dll` with `metadata.json` and loads through the official Workshop UI. Enable only one build and restart when switching. Steam joining preserves the native Workshop compatibility checks. For IP sessions, coordinate required Workshop packages, versions, and load order manually.

Download the ZIP whose name contains `with-BepInEx`.

1. In Steam, right-click Sephiria and select **Manage > Browse local files**.
2. Extract every file from the ZIP directly into the game root directory.
3. `winhttp.dll` and `Sephiria.exe` should be in the same directory.
4. Start the game. The first launch can take longer while BepInEx creates its files.
5. As the multiplayer host, press `F8` to open the Sephiria Together menu.

Offline environments use the IP transport automatically. After selecting a save, confirm room creation in the original multiplayer panel. Clients can refresh the LAN list or press Join and enter `IP:port`. The default game port is TCP `7777`; discovery uses UDP `7780`. Allow both through the firewall.

Do not place the whole ZIP inside `BepInEx/plugins` or `Sephiria_Data`.
