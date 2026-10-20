# CLAUDE.md — 码头垂钓游戏 · 一键构建 v3

运行机：MacBook（M2 Pro / macOS 27 / 16 GB），项目根 `~/Warm`。在该目录开 Claude Code，只发一句：「按 CLAUDE.md 从 Phase 0 开始，不要停，不要问。」

## 0. 总目标
高角度俯视（俯角 60°，非 90°）低多边形绘本风游戏：角色在延伸至海的木码头上垂钓，海水绿松石色半透明、可见海底沙/珊瑚/水草/鱼。`ref/reference.jpg` 为唯一视觉标准。同一 Unity 工程双端交付：Windows 桌面（窗口/全屏）+ WebGL 网页，内容玩法一致，只允许第 4 节列出的差异。

## 1. 铁律（违反即回滚）
1. 分工锁定：Tripo 只产模型 + 角色绑骨/预设动画；Blender 只整理导出；Unity 6 搭世界 + 玩法 + 程序动画；你只编排、写脚本、跑测试。
2. 零提问：缺信息按本文默认值执行，并往 docs/decisions.md 追加一行。**唯一例外＝硬停**：以下任一发生，打印一行 `BLOCKED: <全部缺项>` 后停止，不重试、不绕路：Phase 0 前置检查任一项不过；Tripo 返回 401/402/429 或余额不足；同一外部服务连续 2 次 5xx/超时。
3. 单点修复：测试失败只修第一个失败项再重测；同一点 3 次不过 → `git reset --hard && git clean -fd -e assets_raw` 回上一 Phase 换方案（仍单点修改）。
4. 输出纪律：每 Phase 结束只打印一行 `PHASE-N PASS` 或 `PHASE-N FAIL: <首个原因>`；所有命令输出 `> /tmp/pN.log 2>&1`，失败只 `tail -20`；禁止打印文件全文、贴大段日志。
5. 上下文纪律：先 Glob/Grep（head_limit≤20，排除 Library/ Temp/ obj/ Logs/ *.meta）再读；同一文件只读一次；新文件一次 Write 成型；二进制只验大小+文件头；ref/reference.jpg 仅 Phase 1 读一次，色卡与构图要点记入 decisions.md，之后不再读图。
6. 验证纪律：验收全部程序化断言（脚本退出码 / 单行 PASS-FAIL / Unity 测试结果）；禁止截图验证循环；最终视觉由用户人工验收。
7. 批量脚本化：每 Phase 一个脚本一次执行。Blender 只用 `$BLENDER -b -P <脚本>`，**不用 MCP**（`BLENDER=~/Applications/Blender.app/Contents/MacOS/Blender`，不在 PATH）。Unity 只准 Editor 脚本 + `-batchmode -nographics`，禁止开 Editor GUI、禁止 Package Manager 窗口。macOS 没有 `timeout` 命令，限时用 `cmd & pid=$!; sleep N; kill $pid`。
8. 文件锁定：只准创建第 5 节所列文件；禁止新增 README/文档/第三方包/依赖；每个 C# ≤150 行。
9. 检查点：每 Phase 通过即 `git add -A && git commit -m "phase-N"`；Phase 1 每下载完一件模型立即单独 commit（Tripo 花额度，不许被回滚删掉）。中断恢复：git log 定 Phase + decisions.md 末行定状态，从下一 Phase 继续；禁止重读已完成内容。

## 2. 目录与命名（Phase 0 建）
```
~/Warm/
  CLAUDE.md   ref/reference.jpg   docs/decisions.md
  scripts/    assets_raw/         blender/   assets/   unity/
```
.gitignore：`Library/ Temp/ obj/ Logs/ UserSettings/ Build/ *.csproj *.sln`。
命名：SM_ 静态模型、SK_ 骨骼模型、M_ 材质、AN_ 动画、PF_ 预制体；资源路径全小写连字符、无空格无中文；C# 文件名＝类名（PascalCase，Unity 要求）。

## 3. 默认值（缺省即用）
- Blender：4.5.9 LTS（本机已装，路径见铁律 7）。
- Unity：6.3 LTS（6000.3.x，以 Hub 已装的为准，Phase 0 读出完整版本号记入 decisions.md）；模块 WebGL Build Support + Windows Build Support (Mono)。`UNITY=/Applications/Unity/Hub/Editor/<版本>/Unity.app/Contents/MacOS/Unity`。
- URP：版本＝编辑器自带 core package 版本（读 `Unity.app/Contents/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.universal/package.json` 的 version），禁止自选或「换最新」。
- Tripo：key 在 `~/.tripo_key`（禁入库）；脚本只用 `/usr/bin/python3` + 标准库（urllib），不装 requests。`POST https://api.tripo3d.ai/v2/openapi/task`（`Authorization: Bearer <key>`）→ `GET .../task/{id}` 轮询 → 下载结果。建模 `type=text_to_model` 并传 `face_limit`＝面上限；角色再走 `animate_rig` → `animate_retarget`（预设 idle、walk）。字段名或结果地址（`output.model` / `output.pbr_model`）不符以官方文档为准，只查一次并记 decisions.md。
- 提示词统一后缀：`stylized low-poly game asset, warm storybook palette, soft turquoise sea, clean topology, no text, no watermark`。一律纯文本生成，不用参考图裁剪做图生模型。
- 导出：Blender → Unity 一律 FBX（Unity 不能原生导入 .glb）；1 单位=1m；Apply Scale/Rotation；原点=模型底部中心。判据：导入 Unity 后 localScale=1、无多余 ±90° 旋转。
- 材质：保留 Tripo 贴图，只把材质改名 `M_<模型名>`、贴图缩到 1024；不按木/布/石重分材质。
- 输入：内置 Input Manager（`activeInputHandler=Old`，manifest 里若有 `com.unity.inputsystem` 就删）；触屏用 `Input.touches`，不弹虚拟摇杆。移动＝WASD，或按住鼠标/手指 → 角色朝按点方向走、松开停；在钓点范围内 Space/单击＝抛竿/收杆。
- UI：一律 OnGUI（鱼饵选择、鱼获计数、F3 性能 HUD），不用 UGUI。
- 本版不做：存档、音频（以后要存档只用 PlayerPrefs）。

## 4. 双端策略（单工程双 Target）
1. 桌面 Windows x64 **Mono 后端**（Mac 上打不了 Windows IL2CPP）；网页 WebGL 2.0 IL2CPP（WebGPU 不默认）。差异只走 PlayerSettings + `#if UNITY_WEBGL`；禁止两份代码或两份场景。
2. 网页包体：剥离 High；Brotli + `decompressionFallback=true`（这样 `python3 -m http.server` 可直接跑）；贴图 WebGL 平台覆盖 maxSize 1024、格式 DXT；从 manifest.json 删未用引擎模块（video/vr/xr/cloth/vehicles/wind/umbra 等，删后包解析失败就只把报错的那一个加回）；不引 Addressables。目标压缩后 ≤60MB；超标砍序：贴图 1024→512 → 关 MSAA → 复查误引模块。
3. 画质：两个 URP Asset——`URP_High`（桌面：MSAA 4×、主光实时阴影）/ `URP_Medium`（网页：renderScale 0.8、MSAA 关、关实时阴影，角色脚下放半透明圆片当影子）；QualitySettings 两档，Windows 默认 High、WebGL 默认 Medium。F3 切换性能 HUD（FPS/内存）。
4. 帧率与窗口：桌面 `vSyncCount=1`、`targetFrameRate=60`，默认窗口 1920×1080、可调整大小、`allowFullscreenSwitch=true`（Alt+Enter 系统自带）；网页 `targetFrameRate=-1`（交给浏览器）。BuildPlayer 打完包后往 index.html 注入两处：canvas 满窗 CSS、`config.devicePixelRatio = Math.min(devicePixelRatio, 2)`；不建自定义 WebGL 模板。
5. 兼容红线：水面 shader 手写 URP HLSL，只做顶点正弦 + 透明 + `_Color` 属性；禁 compute/geometry shader；自己的代码不用 Burst/Job；鱼群/浮漂/水花一律对象池。
6. 启动标记：GameBootstrap 场景就绪时 `Debug.Log("SCENE-READY")`，网页/桌面冒烟测试都 grep 这一行。

## 5. 文件清单（写死，禁止增删）
```
scripts/tripo_gen.py   scripts/check_glb.py   scripts/web_check.sh
blender/clean.py
unity/Assets/Editor/: ProjectSetup.cs BuildScene.cs BuildNature.cs BuildPlayer.cs
unity/Assets/Shaders/Water.shader
unity/Assets/Scripts/: Game.asmdef Config.cs PlayerController.cs TopDownCamera.cs CharacterRig.cs
                       FishingStateMachine.cs BaitSelector.cs TimeOfDay.cs Floater.cs FishSchool.cs GameBootstrap.cs
unity/Assets/Tests/EditMode/: Tests.asmdef BuildSceneTests.cs SystemsTests.cs
```
Tests.asmdef 只在 Editor 平台编译，引用 Game + 测试框架（没有 asmdef 的话，测试代码会编进游戏本体，直接编译失败）。

## Phase 0 — 前置检查（只检查不安装）
逐项查完再汇总，有缺即一次性 BLOCKED：① `$BLENDER --version` 为 4.5.x ② `$UNITY` 存在且 `PlaybackEngines/` 下有 WebGL 与 Windows 两个模块 ③ 许可证：`$UNITY -batchmode -nographics -quit -logFile -` 退出码 0 且日志无 license 报错 ④ `~/.tripo_key` 存在 ⑤ `df` 可用 ≥15 GB ⑥ `ref/reference.jpg` 存在 ⑦ `/Applications/Google Chrome.app` 存在。
通过后建第 2 节目录 + git init + decisions.md + 首次提交。
测试：`$BLENDER -b --python-expr "import bpy;print('OK')"` 输出 OK。

## Phase 1 — Tripo 4 模型 → assets_raw/
| 文件 | 要点 | 面上限 |
|---|---|---|
| rock | 海岸岩石（多尺寸靠缩放） | 3k |
| boat | 小木船，舱内空 | 5k |
| cabin | 海边小屋 | 8k |
| character | 草帽渔夫，A-pose → rig → idle/walk 预设 | 10k |

码头、梯、箱、桶、鱼竿、鱼、珊瑚、水草、树一律 Unity 原生几何体（码头是玩法几何：可走范围、碰撞、钓点坐标必须精确且可纵向拼接）。单件重试 ≤3。角色可能是 1 个含 2 段动画的 glb，也可能每段动画 1 个 glb，后续脚本两种都要接得住。绑骨失败 → 退回静态角色 + CharacterRig 纯程序动画（起伏/摇摆），记 decisions.md，不算 FAIL。
测试：`python3 scripts/check_glb.py`（文件头 glTF + JSON 可解析；character 合计 ≥2 段 animation）。
验收：4 个模型齐全且总大小 <50MB。

## Phase 2 — Blender 整理（`$BLENDER -b -P blender/clean.py` 一次跑完）
导入 → Apply 变换 → 原点移底部中心 → 材质改名 `M_*`、贴图缩 1024 → 角色动作合并并改名 `AN_idle` / `AN_walk` → 导出 `assets/SM_rock.fbx SM_boat.fbx SM_cabin.fbx SK_character.fbx`。
测试：同一脚本末尾清空场景、重新导入全部 FBX，断言命名合规、无孤立/重名材质、角色恰有 2 个动作且首尾帧位姿一致（只有这两段循环动画要闭合；抛竿/收线是 Unity 程序动画，不在此测），单行输出。

## Phase 3 — Unity 工程与场景（全 batchmode）
1. `$UNITY -batchmode -quit -createProject ~/Warm/unity`
2. 用 python 改 `Packages/manifest.json`：加 URP（版本见第 3 节），删未用模块与 inputsystem。
3. `cp assets/*.fbx unity/Assets/Art/`
4. `$UNITY -batchmode -nographics -quit -projectPath unity -executeMethod ProjectSetup.Run -logFile /tmp/p3a.log`：建两个 URP Asset 并挂到 Graphics/Quality；PlayerSettings 按第 4 节；内含 AssetPostprocessor（FBX 动画开 loop、根节点 XZ 位移 Bake Into Pose）。
5. `$UNITY ... -executeMethod BuildScene.Build -logFile /tmp/p3b.log` → `Assets/Scenes/Main.unity`：
   - BuildScene：海面（Water.shader）；海底＝沙面 + 珊瑚/水草（原生几何体）+ FishSchool；码头用方块木板沿 +Z 伸入海，末端为钓点 `Config.FishSpot`；右舷 boat 挂 Floater；cabin/rock/箱/桶/梯就位；角色挂 PlayerController + CharacterRig，手上挂鱼竿（圆柱）+ 鱼线（LineRenderer）+ 浮漂（球，挂 Floater）；相机俯角 `Config.CamPitch=60`、FOV 35、距离 18m，初始对准码头中点。
   - BuildNature：海滩、低多边形树群（圆锥+圆柱）、河流、瀑布（原生几何体，配色取 decisions.md 色卡）。
   - 所有颜色/坐标常量只写在 Config.cs，场景搭建与测试共用这一份。
测试：`$UNITY -runTests -batchmode -nographics -projectPath unity -testPlatform EditMode -testResults /tmp/p3.xml`；BuildSceneTests 断言对象数、关键坐标、材质色、相机俯角、FBX localScale=1 且无多余旋转，单行输出。本 Phase 不打包。

## Phase 4 — 玩法系统（严格按序，一次只加一个，每步 runTests）
① PlayerController：WASD/按住移动 + CharacterController 碰撞，码头与沙滩以外不可走；Animator 切 idle/walk，`applyRootMotion=false`。
② TopDownCamera：平滑跟随，限制在场景边界内。
③ FishingStateMachine：**纯 C# 类**（可注入 `System.Random` 种子）+ 很薄的 MonoBehaviour 外壳。流转：Idle →（钓点内按 Space/单击）Cast → Wait（2–6 s × 饵料系数）→ Bite（1.5 s 内要按，超时鱼跑掉 → Idle）→ Reel 张力条（按住拉、松开退；张力 >1 断线，进度 ≥1 钓上）→ Caught/Lost → Idle。CharacterRig 在 LateUpdate 叠加抛竿/收线的手臂骨骼与鱼竿旋转。
④ BaitSelector：面包/蠕虫/玉米，1/2/3 键或 OnGUI 按钮；每种饵影响等待系数与鱼种权重。
⑤ TimeOfDay：晨/午/昏/夜，插值主光颜色与强度、环境色、水面 `_Color`；一天＝`Config.DayLength`（默认 240 s），T 键跳下一时段。
测试：SystemsTests 全绿，只测纯 C# 逻辑（状态流转、超时、断线、饵料系数、时段插值端点）。

## Phase 5 — 打包收尾
`BuildPlayer.Windows` → `unity/Build/Desktop/Warm.exe`；`BuildPlayer.Web` → `unity/Build/Web/`（含 index.html 注入）；decisions.md 末尾汇总全部默认值。
测试：
- `scripts/web_check.sh`：起 `python3 -m http.server`；headless Chrome（`--headless=new --use-angle=metal --enable-logging=stderr --v=1`，GPU 不可用时改 `--enable-unsafe-swiftshader`）打开页面，60 s 后 kill；断言 `SCENE-READY` 恰出现 1 次、控制台 error 为 0；`du` 量 `Build/Web/Build` ≤60MB。
- 桌面：构建退出码 0，且 `Warm.exe` 与 `Warm_Data/` 都存在。
验收：网页端能进码头场景、角色可走动、画质档为 Medium、包 ≤60MB。Windows 实机启动与全屏切换由用户人工验收（Mac 上运行不了 exe）。
