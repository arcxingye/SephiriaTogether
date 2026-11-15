# Sephiria Together — native Workshop build

Targets Sephiria **1.1.3**, Windows x64 / Unity Mono. Version **3.10.1**.

## 本地检查与上传

1. 将本文件所在的 `SephiriaTogether` 文件夹放到游戏目录的 `WorkshopLocal` 下。
2. 使用工坊版时，停用旧的 `BepInEx/plugins/SephiriaTogether.dll` 并重启游戏。两种版本共用补丁标识；重复加载会被拒绝。
3. 登录 Steam 后启动游戏，在标题界面打开创意工坊，选择本地 `SephiriaTogether`。
4. 检查标题、封面和版本，点击上传并按游戏提示提交。含 DLL 的内容会走游戏的代码审核流程；首次条目默认为 Unlisted。
5. 保留自动生成的 `uploadticket.json`。后续更新替换 DLL、封面与 metadata 后，仍通过同一个本地条目上传。

本地工坊包可以用于单机加载检查；游戏原生限制已加载本地包的联机。联机回归应使用审核后可加载的订阅版，并移走/停用本地测试副本后重启。

### 更新后提示审核失败 / F8 无反应

Steam 文件上传与游戏 DLL 审核提交是两个阶段。在此前已排查的 1.1.1 上传流程中，游戏先更新 Steam 文件，再向审核服务提交；第二步失败时可能出现“网页已更新，但订阅者无法加载”。本地测试成功不代表订阅版已通过审核。

- 保留原来的 `uploadticket.json`，不要手动修改版本、状态或批准证明。旧票据的 `approved` 只代表旧次提交。
- 在游戏工坊面板中选中**原本的本地 Sephiria Together**，确认待提交版本，重新点击上传/请求审核。等整个操作结束；若 Steam 上传完成但审核提交失败，记录完整错误后重试或联系开发者。
- 使用项目的只读检查工具，按**实际 DLL 内容指纹 + 原条目 ID**查询当前审核状态：

```powershell
.\scripts\check-workshop-review.ps1 -PackageRoot "E:\SteamLibrary\steamapps\common\Sephiria\WorkshopLocal\SephiriaTogether" | Format-List
```

若检查的是没有票据的构建包或订阅目录，请加 `-PublishedFileId 3831451965`。
`unknown` 表示当前文件无匹配审核记录，`waiting/checking` 表示已进入队列/审核，`approved` 表示该指纹已获批准，`rejected` 才是拒绝，`unavailable` 表示查询失败。

当前指纹批准后，用 Steam 下载的**订阅副本**验证：停用本地测试副本和旧 BepInEx Together，重启并进入存档，再按 F8。若仍无反应，提供游戏版本、Mod 版本、审核状态，以及 `%USERPROFILE%\AppData\LocalLow\TEAMHORAY\Sephiria\Player.log` 中 `WorkshopModRuntime`、`SephiriaTogether`、`OnModLoaded` 附近的报错。首次标题页尚未加载工坊 DLL，不能用它判断 F8 是否失效。

不要把“开启未审核模组运行”作为面向订阅者的修复方案。

## 使用

- 进入游戏后按 **F8** 打开菜单；默认求救键为 **R**，可在 F8 菜单修改。
- 背包整理也使用 F8 时，Together 菜单临时使用 **F9**，以游戏提示/诊断页为准；不会修改双方保存的快捷键。只启用一个背包整理版本。
- Star's SephiriaMod 2.7.4 需要全员安装。Together 保持创建时的房间人数上限；Star 的 Boss Rush／无限模式会暂停 Together 的主线进度切换。背包整理 3.0.0 使用交换/旋转命令，无需关闭 Together 反作弊；新增物品的评分由整理 Mod 决定。完整三 Mod 联机回归尚待完成。
- 游戏 1.1.3 的工坊 DLL 在进入存档时才加载。冷启动后的首次主菜单不会响应工坊版 F8；`OnTitleMenuReady` 事件不能让尚未加载的 DLL 提前运行。组件激活后会常驻，返回标题不会主动销毁它。需要首次标题页配置时应使用启动阶段加载的 BepInEx 版。
- 工坊版通过游戏的 `ModOptions` 保存设置，使用本机 `OptionsBinding.DeviceOptions`，不会往订阅目录写配置。
- BepInEx 的 `.cfg` 与工坊版配置相互独立，首次切换请在 F8 菜单重新确认设置。
- 主机功能不强制客机安装；转账、客机菜单、求救和 IP/LAN 功能仍要求相应端安装。双方游戏版本须一致，其他工坊包的依赖检查照常生效。
- 直连设置可在局内编辑，界面会区分“当前实际模式”和“已保存（下次会话）”。需要切换时，点击“退出当前会话，回标题应用”，走原版退出/结算流程；网络停止后才会应用模式和默认端口，再进入存档建房或加入。不会在当前连接中热切换。
- 每次冷启动，工坊版都必须先能进入一次存档。不要用重启程序来应用待切换设置，否则会重复晚加载阶段；原版无法进入存档的离线环境需要早期启动的 BepInEx 版。
- DLL 模组按原版规则需要重启才能完整停用；卸载回调会撤销本插件补丁、关闭监听、移除消息处理器并清理运行状态。

## Packaging / verification

Run `scripts/build-workshop.ps1 -GameDir "<Sephiria installation>"` from the source project.
The runtime DLL references the game's bundled Harmony, Unity and Mirror assemblies; players do not need BepInEx for this build. No game or loader DLLs are bundled.

The build tools perform metadata-only validation. Actual native loading and multiplayer behavior require in-game testing; no Steam item is created or uploaded by the build script.

Source and MIT license: https://github.com/arcxingye/SephiriaTogether
