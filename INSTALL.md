# Installation / 安装说明

## 新手整合包

下载文件名包含 `with-BepInEx` 的 ZIP。

1. 在 Steam 中右键 Sephiria，选择“管理” -> “浏览本地文件”。
2. 打开 ZIP，把其中所有文件直接拖到游戏根目录。
3. 正确安装后，`winhttp.dll` 应当和 `Sephiria.exe` 位于同一个目录。
4. 启动游戏。第一次启动时 BepInEx 会创建配置和缓存，可能比平时稍慢。
5. 房主进入多人游戏后按 `F8` 打开 Sephiria Together 菜单。

不要把整个 ZIP 放进 `BepInEx/plugins`，也不要把文件放进 `Sephiria_Data`。

## 已安装 BepInEx

下载普通 ZIP 或单独的 `SephiriaTogether.dll`，把 DLL 放入：

```text
Sephiria/BepInEx/plugins/SephiriaTogether.dll
```

## Beginner bundle

Download the ZIP whose name contains `with-BepInEx`.

1. In Steam, right-click Sephiria and select **Manage > Browse local files**.
2. Extract every file from the ZIP directly into the game root directory.
3. `winhttp.dll` and `Sephiria.exe` should be in the same directory.
4. Start the game. The first launch can take longer while BepInEx creates its files.
5. As the multiplayer host, press `F8` to open the Sephiria Together menu.

Do not place the whole ZIP inside `BepInEx/plugins` or `Sephiria_Data`.
