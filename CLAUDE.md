# CLAUDE.md — 码头垂钓游戏 · 一键构建 v3.2（Windows 版）

> v3.2（2026-10-21）：用户拍板不充 Tripo、不用参考图；优先做光影（Phase 6）与动作（Phase 7），**建模（Phase 1 程序化建模）之后做**，场景里的 rock/boat/cabin 暂用占位模型。

运行机：Windows 11 算力机 desktop-ahpct75（i3-12100F / RTX 4060 / 16 GB），项目根 `D:\warm`（MacBook `~/Warm` 只留作 v3 备份，不再改）。在该目录开 Claude Code，只发一句：「按 CLAUDE.md 从 Phase 0 开始，不要停，不要问。」

工具变量（PowerShell 写法；均不在 PATH，一律写全路径）：
```
$BLENDER = "D:\tools\blender-4.5.9\blender.exe"
$PY      = "D:\tools\blender-4.5.9\4.5\python\bin\python.exe"   # Blender 自带 Python，只用标准库；系统 python 是商店占位，禁用
$GIT     = "D:\tools\mingit\cmd\git.exe"                          # 文中 git 一律指它；tripo_gen.py 读环境变量 GIT
$UNITY   = "D:\Unity\Editors\<版本>\Editor\Unity.exe"
$EDGE    = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
```
C: 只剩约 11 GB：一切安装、缓存、日志、构建都放 D:（临时目录 `D:\tmp`）。外网走系统代理 `http://127.0.0.1:10808`（Unity 下载直连会被 302 到 unitychina 再 404；`$PY` 的 urllib 默认读系统代理，`curl.exe` 要显式 `-x`）。

## 0. 总目标
高角度俯视（俯角 60°，非 90°）低多边形绘本风游戏：角色在延伸至海的木码头上垂钓，海水绿松石色半透明、可见海底沙/珊瑚/水草/鱼。`ref/reference.jpg` 为唯一视觉标准。同一 Unity 工程双端交付：Windows 桌面（窗口/全屏）+ WebGL 网页，内容玩法一致，只允许第 4 节列出的差异。

## 1. 铁律（违反即回滚）
1. 分工锁定（v3.2 用户拍板改）：模型由 Blender 代码程序化建模（`blender/model.py`，art-1/3 已做）；角色骨架 + 简模人偶 + 关键帧动作由 `blender/rig.py` 产出；Blender 负责整理导出；Unity 6 搭世界 + 玩法 + 程序动画；你只编排、写脚本、跑测试。Tripo 不再使用（`scripts/tripo_gen.py` 留存备用）。
2. 零提问：缺信息按本文默认值执行，并往 docs/decisions.md 追加一行。**唯一例外＝硬停**：以下任一发生，打印一行 `BLOCKED: <全部缺项>` 后停止，不重试、不绕路：Phase 0 前置检查任一项不过；Tripo 返回 401/402/429 或余额不足；同一外部服务连续 2 次 5xx/超时。
3. 单点修复：测试失败只修第一个失败项再重测；同一点 3 次不过 → `& $GIT reset --hard; & $GIT clean -fd -e assets_raw` 回上一 Phase 换方案（仍单点修改）。
4. 输出纪律：每 Phase 结束只打印一行 `PHASE-N PASS` 或 `PHASE-N FAIL: <首个原因>`；所有命令输出 `*> D:\tmp\pN.log`，失败只 `tail -20`；禁止打印文件全文、贴大段日志。
5. 上下文纪律：先 Glob/Grep（head_limit≤20，排除 Library/ Temp/ obj/ Logs/ *.meta）再读；同一文件只读一次；新文件一次 Write 成型；二进制只验大小+文件头；ref/reference.jpg 仅 Phase 1 读一次，色卡与构图要点记入 decisions.md，之后不再读图。
6. 验证纪律：验收全部程序化断言（脚本退出码 / 单行 PASS-FAIL / Unity 测试结果）；禁止截图验证循环；最终视觉由用户人工验收。例外：建模允许看渲染图自查，每个模型最多 2 轮；Phase 6 四时段截图、Phase 7 演示录屏是交给用户的验收物，各自最多 3 轮。
7. 批量脚本化：每 Phase 一个脚本一次执行。Blender 只用 `& $BLENDER -b -P <脚本>`，**不用 MCP**（本机那个开着 MCP 插件的 Blender 5.2 是远程入口，不许拿它干活、不许关它）。Unity 只准 Editor 脚本 + `-batchmode -nographics`，禁止开 Editor GUI、禁止 Package Manager 窗口。Windows 的 `timeout` 是 sleep 不是限时，限时用 `$p = Start-Process <exe> <args> -PassThru; if (-not $p.WaitForExit(N*1000)) { Stop-Process $p -Force }`。
8. 文件锁定：只准创建第 5 节所列文件；禁止新增 README/文档/第三方包/依赖；每个 C# ≤150 行。
9. 检查点：每 Phase 通过即 `& $GIT add -A; & $GIT commit -m "phase-N"`；Phase 1 每下载完一件模型立即单独 commit（Tripo 花额度，不许被回滚删掉）。中断恢复：git log 定 Phase + decisions.md 末行定状态，从下一 Phase 继续；禁止重读已完成内容。

## 2. 目录与命名（Phase 0 建）
```
D:\warm\
  CLAUDE.md   ref/reference.jpg   docs/decisions.md
  scripts/    assets_raw/         blender/   assets/   unity/
```
.gitignore：`Library/ Temp/ obj/ Logs/ UserSettings/ Build/ *.csproj *.sln`。
命名：SM_ 静态模型、SK_ 骨骼模型、M_ 材质、AN_ 动画、PF_ 预制体；资源路径全小写连字符、无空格无中文；C# 文件名＝类名（PascalCase，Unity 要求）。

## 3. 默认值（缺省即用）
- Blender：4.5.9 LTS 便携版（本机已装，路径见开头 `$BLENDER`）。
- Unity：6.3 LTS（6000.3.26f1，Hub 装在 `D:\Unity\Editors`；Phase 0 读出完整版本号记入 decisions.md）；模块 WebGL Build Support；Windows Build Support (Mono) 是 Windows 编辑器自带的。不装 Windows IL2CPP（要 VS C++ 工具链，C: 放不下）。
- URP：版本＝编辑器自带 core package 版本（读 `Editor\Data\Resources\PackageManager\BuiltInPackages\com.unity.render-pipelines.universal\package.json` 的 version），禁止自选或「换最新」。
- Tripo：key 在 `%USERPROFILE%\.tripo_key`（禁入库、禁打印）；脚本只用 `$PY` + 标准库（urllib），不装 requests。**API v3**（v2 于 2026-11-01 停用）：base `https://openapi.tripo3d.com/v3`（`Authorization: Bearer <key>`），每种任务一个端点、请求体不带 `type`：建模 `POST /generation/text-to-model`（传 `face_limit`＝面上限）；角色再走 `POST /animations/rig` → `POST /animations/retarget`（上一步 task_id 统一放 `input` 字段；预设 idle、walk）；`GET /tasks/{id}` 轮询 → 下载结果。字段名或结果地址（`output.model` / `output.pbr_model`）不符以官方文档为准，只查一次并记 decisions.md。
- 提示词统一后缀：`stylized low-poly game asset, warm storybook palette, soft turquoise sea, clean topology, no text, no watermark`。一律纯文本生成，不用参考图裁剪做图生模型。
- 导出：Blender → Unity 一律 FBX（Unity 不能原生导入 .glb）；1 单位=1m；Apply Scale/Rotation；原点=模型底部中心。判据：导入 Unity 后 localScale=1、无多余 ±90° 旋转。
- 材质：保留 Tripo 贴图，只把材质改名 `M_<模型名>`、贴图缩到 1024；不按木/布/石重分材质。
- 输入：内置 Input Manager（`activeInputHandler=Old`，manifest 里若有 `com.unity.inputsystem` 就删）；触屏用 `Input.touches`，不弹虚拟摇杆。移动＝WASD，或按住鼠标/手指 → 角色朝按点方向走、松开停；在钓点范围内 Space/单击＝抛竿/收杆。
- UI：一律 OnGUI（鱼饵选择、鱼获计数、F3 性能 HUD），不用 UGUI。
- 本版不做：存档、音频（以后要存档只用 PlayerPrefs）。

## 4. 双端策略（单工程双 Target）
1. 桌面 Windows x64 **Mono 后端**（IL2CPP 要 VS C++，不装）；网页 WebGL 2.0 IL2CPP（WebGPU 不默认）。差异只走 PlayerSettings + `#if UNITY_WEBGL`；禁止两份代码或两份场景。
2. 网页包体：剥离 High；Brotli + `decompressionFallback=true`（这样 `& $PY -m http.server` 可直接跑）；贴图 WebGL 平台覆盖 maxSize 1024、格式 DXT；从 manifest.json 删未用引擎模块（video/vr/xr/cloth/vehicles/wind/umbra 等，删后包解析失败就只把报错的那一个加回）；不引 Addressables。目标压缩后 ≤60MB；超标砍序：贴图 1024→512 → 关 MSAA → 复查误引模块。
3. 画质：两个 URP Asset——`URP_High`（桌面：MSAA 4×、主光实时阴影）/ `URP_Medium`（网页：renderScale 0.8、MSAA 关、关实时阴影，角色脚下放半透明圆片当影子）；QualitySettings 两档，Windows 默认 High、WebGL 默认 Medium。F3 切换性能 HUD（FPS/内存）。
4. 帧率与窗口：桌面 `vSyncCount=1`、`targetFrameRate=60`，默认窗口 1920×1080、可调整大小、`allowFullscreenSwitch=true`（Alt+Enter 系统自带）；网页 `targetFrameRate=-1`（交给浏览器）。BuildPlayer 打完包后往 index.html 注入两处：canvas 满窗 CSS、`config.devicePixelRatio = Math.min(devicePixelRatio, 2)`；不建自定义 WebGL 模板。
5. 兼容红线：水面 shader 手写 URP HLSL，只做顶点正弦 + 透明 + `_Color` 属性；禁 compute/geometry shader；自己的代码不用 Burst/Job；鱼群/浮漂/水花一律对象池。
6. 启动标记：GameBootstrap 场景就绪时 `Debug.Log("SCENE-READY")`，网页/桌面冒烟测试都 grep 这一行。

## 5. 文件清单（写死，禁止增删）
```
scripts/tripo_gen.py（停用，留存）   scripts/check_glb.py   scripts/web_check.py
blender/clean.py   blender/rig.py   blender/model.py
unity/Assets/Editor/: ProjectSetup.cs BuildScene.cs BuildNature.cs BuildPlayer.cs BuildFx.cs BuildCharacter.cs
unity/Assets/Shaders/Water.shader
unity/Assets/Scripts/: Game.asmdef Config.cs PlayerController.cs TopDownCamera.cs CharacterRig.cs Tackle.cs
                       FishingStateMachine.cs BaitSelector.cs TimeOfDay.cs Floater.cs FishSchool.cs GameBootstrap.cs
unity/Assets/Tests/EditMode/: Tests.asmdef BuildSceneTests.cs SystemsTests.cs
双人合作子阶段新增（用户逐阶段下达）：
  stage-0  unity/Assets/Scripts/: ConfigCoop.cs Gender.cs ItemType.cs PlayerContext.cs PlayerManager.cs
  stage-1  unity/Assets/Scripts/: KeyMap.cs InputManager2P.cs
  stage-2  unity/Assets/Tests/EditMode/CoopTests.cs（双人相关的纯逻辑测试，SystemsTests 保持只测钓鱼/时段）
  stage-3  unity/Assets/Scripts/: Stock.cs PierSave.cs
  stage-4  unity/Assets/Scripts/: Pause.cs Bricks.cs
美术打磨（art-1~4）新增：unity/Assets/Art/SM_{tree,crate,barrel,lantern,bedframe,bedding,plank}.fbx（model.py 产出）；
  unity/Assets/Fonts/WarmCJK-Regular.otf（思源黑体子集，按 OFL 保留字体名条款改名）+ WarmCJK-OFL.txt（原许可证）
自迭代环新增：loop-1  unity/Assets/Scripts/Trees.cs（砍树 + E 的就近交互）  unity/Assets/Tests/EditMode/PlayTests.cs（玩法纯逻辑测试，CoopTests 已满）
  loop-2  unity/Assets/Scripts/ScriptedPlay.cs（-script 计划实玩：游戏自己注入按键、截图，锁屏也能测）
  loop-3  unity/Assets/Scripts/Hints.cs（头顶就近提示：现在按了就会成功的动作）
  loop-4  unity/Assets/Tests/EditMode/PlayTests2.cs（玩法纯逻辑测试，PlayTests 满了接着放）
  loop-5  unity/Assets/Editor/BuildProps.cs（道具和床的摆放，从 BuildScene 拆出）  unity/Assets/Scripts/Sleep.cs（床和睡觉，从 PierSave 拆出）
  loop-7  unity/Assets/Scripts/BrickPath.cs（放砖不许切断去码头的路，BFS）  unity/Assets/Tests/EditMode/PlayTests3.cs（玩法纯逻辑测试，PlayTests2 快满了接着放）
  loop-10 unity/Assets/Scripts/FishHud.cs（角上的钓鱼字和收线时的两根条，从 FishingStateMachine 拆出）  unity/Assets/Scripts/ScriptedPlan.cs（ScriptedPlay 的另一半 partial：计划的词、解析、键码映射）
  loop-9  unity/Assets/Tests/EditMode/PlayTests4.cs（篝火烤鱼、熟鱼换砖的纯逻辑测试）  unity/Assets/Art/SM_campfire.fbx（篝火，model.py 产出；assets/SM_campfire.fbx、assets_raw/campfire.glb 同管线）
  loop-11 unity/Assets/Scripts/BrickWhy.cs（按 B 放不下的原因码和要说的那句，Bricks.cs 满了单放）
  loop-13 unity/Assets/Scripts/Hug.cs（两人凑近按 H 抱一下：能不能抱、面对面朝向、计时与双臂权重、读键）  unity/Assets/Tests/EditMode/PlayTests5.cs（拥抱的纯逻辑测试，PlayTests 满了另开）
  loop-15 unity/Assets/Tests/EditMode/SceneTests2.cs（场景断言，BuildSceneTests 满了接着放）  unity/Assets/Scripts/BrickBuild.cs（Bricks 的 partial：按记录生成方块、按 B 放砖）  unity/Assets/Scripts/HintsDraw.cs（Hints 的 partial：OnGUI 字条画法）
  loop-16 unity/Assets/Tests/EditMode/PlayTests6.cs（钓上说中文鱼名、黄昏前不列睡觉、天亮重置树、Toast 改说在字条上的纯逻辑测试，PlayTests5 快满了另开）
```
Tests.asmdef 只在 Editor 平台编译，引用 Game + 测试框架（没有 asmdef 的话，测试代码会编进游戏本体，直接编译失败）。

## Phase 0 — 前置检查（只检查不安装）
逐项查完再汇总，有缺即一次性 BLOCKED：① `& $BLENDER --version` 为 4.5.x ② `$UNITY` 存在且 `Editor\Data\PlaybackEngines\` 下有 `WebGLSupport` 与 `windowsstandalonesupport` ③ 许可证：`& $UNITY -batchmode -nographics -quit -logFile D:\tmp\lic.log` 退出码 0 且日志无 license 报错 ④ `%USERPROFILE%\.tripo_key` 存在（只验前缀 `tsk_`、长度 47）⑤ D: 可用 ≥15 GB ⑥ `ref/reference.jpg` 存在 ⑦ `$EDGE` 存在。
仓库已存在（从 MacBook 迁来，含 trial/win 提交）：只补齐第 2 节缺的目录 + decisions.md，再提交。
测试：`$BLENDER -b --python-expr "import bpy;print('OK')"` 输出 OK。

## Phase 1 — Blender 程序化建模 → assets_raw/*.glb（art-1 道具、art-3 小件已做，见 decisions.md「美术打磨」）

v3.2：改为 `& $BLENDER -b -P blender/model.py` 用代码搭 rock/boat/cabin（棱角低多边形、平直着色、每色一个纯色材质——URP Lit 不认顶点色），颜色按 decisions.md 文字色卡；角色由 Phase 7 的 rig.py 负责。下表的面上限照旧；下面 Tripo 的描述作废，仅留档。

### （旧）Tripo 4 模型
| 文件 | 要点 | 面上限 |
|---|---|---|
| rock | 海岸岩石（多尺寸靠缩放） | 3k |
| boat | 小木船，舱内空 | 5k |
| cabin | 海边小屋 | 8k |
| character | 草帽渔夫，A-pose → rig → idle/walk 预设 | 10k |

码头、梯、箱、桶、鱼竿、鱼、珊瑚、水草、树一律 Unity 原生几何体（码头是玩法几何：可走范围、碰撞、钓点坐标必须精确且可纵向拼接）。单件重试 ≤3。角色可能是 1 个含 2 段动画的 glb，也可能每段动画 1 个 glb，后续脚本两种都要接得住。绑骨失败 → 退回静态角色 + CharacterRig 纯程序动画（起伏/摇摆），记 decisions.md，不算 FAIL。
测试：`& $PY scripts/check_glb.py`（文件头 glTF + JSON 可解析；character 合计 ≥2 段 animation）。
验收：4 个模型齐全且总大小 <50MB。

## Phase 2 — Blender 整理（`& $BLENDER -b -P blender/clean.py` 一次跑完）
导入 → Apply 变换 → 原点移底部中心 → 材质改名 `M_*`、贴图缩 1024 → 角色动作合并并改名 `AN_idle` / `AN_walk` → 导出 `assets/SM_rock.fbx SM_boat.fbx SM_cabin.fbx SK_character.fbx`。
测试：同一脚本末尾清空场景、重新导入全部 FBX，断言命名合规、无孤立/重名材质、角色恰有 2 个动作且首尾帧位姿一致（只有这两段循环动画要闭合；抛竿/收线是 Unity 程序动画，不在此测），单行输出。

## Phase 3 — Unity 工程与场景（全 batchmode）
1. `unity/Assets` 下第 5 节的代码已写好（win-7，只过了 Roslyn 编译检查）：不再 `-createProject`，直接 `& $UNITY -batchmode -nographics -quit -projectPath D:\warm\unity -logFile D:\tmp\p3o.log` 打开一次，让 Unity 补齐 ProjectSettings/ 与 Packages/。之后先跑测试，只修第一个失败项。
2. 用 `$PY` 改 `Packages/manifest.json`：加 URP（版本见第 3 节），删未用模块与 inputsystem。
3. `Copy-Item assets\*.fbx unity\Assets\Art\`
4. `$UNITY -batchmode -nographics -quit -projectPath unity -executeMethod ProjectSetup.Run -logFile D:\tmp\p3a.log`：建两个 URP Asset 并挂到 Graphics/Quality；PlayerSettings 按第 4 节；内含 AssetPostprocessor（FBX 动画开 loop、根节点 XZ 位移 Bake Into Pose）。
5. `$UNITY ... -executeMethod BuildScene.Build -logFile D:\tmp\p3b.log` → `Assets/Scenes/Main.unity`：
   - BuildScene：海面（Water.shader）；海底＝沙面 + 珊瑚/水草（原生几何体）+ FishSchool；码头用方块木板沿 +Z 伸入海，末端为钓点 `Config.FishSpot`；右舷 boat 挂 Floater；cabin/rock/箱/桶/梯就位；角色挂 PlayerController + CharacterRig，手上挂鱼竿（圆柱）+ 鱼线（LineRenderer）+ 浮漂（球，挂 Floater）；相机俯角 `Config.CamPitch=60`、FOV 35、距离 18m，初始对准码头中点。
   - BuildNature：海滩、低多边形树群（圆锥+圆柱）、河流、瀑布（原生几何体，配色取 decisions.md 色卡）。
   - 所有颜色/坐标常量只写在 Config.cs，场景搭建与测试共用这一份。
测试：`$UNITY -runTests -batchmode -nographics -projectPath unity -testPlatform EditMode -testResults D:\tmp\p3.xml`；BuildSceneTests 断言对象数、关键坐标、材质色、相机俯角、FBX localScale=1 且无多余旋转，单行输出。本 Phase 不打包。

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
- `& $PY scripts/web_check.py`（标准库，单行 PASS/FAIL）：后台起 `$PY -m http.server`；headless Edge（`$EDGE --headless=new --user-data-dir=D:\tmp\edge --enable-logging=stderr --v=1`，GPU 不可用时加 `--enable-unsafe-swiftshader`）打开页面，60 s 后结束 Edge 与 http.server；断言 `SCENE-READY` 恰出现 1 次、控制台 error 为 0；量 `Build/Web/Build` 合计 ≤60MB。
- 桌面：构建退出码 0，且 `Warm.exe` 与 `Warm_Data/` 都存在；本机启动验收：`Start-Process unity\Build\Desktop\Warm.exe "-logFile D:\tmp\Player.log -screen-fullscreen 0" -PassThru`，30 s 后 `Stop-Process`，`D:\tmp\Player.log` 里 `SCENE-READY` 恰 1 次。
验收：网页端能进码头场景、角色可走动、画质档为 Medium、包 ≤60MB；桌面 exe 本机能起到 SCENE-READY。全屏切换（Alt+Enter）与手感由用户人工验收。

## Phase 6 — 光影（v3.2，提交 `light-N`）
1. TimeOfDay：晨（低角度暖橙、影长）/午（高角度白偏暖）/昏（低角度橙红）/夜（冷蓝月光、低强度）四个 TimeKey，smoothstep 插值太阳仰角/方位/颜色/强度；环境光 Trilight（天空/地平线/地面三色）随时段变。常量全在 Config.Times。
2. 阴影：URP_High 软阴影、阴影距离 40 m、2 级联、2048；URP_Medium 无实时阴影，只有角色脚下圆片（BuildFx.TuneUrp，由 ProjectSetup 调用）。
3. Water.shader：颜色随时段（`_Color` 由 TimeOfDay 写）、近岸浅远处深（按世界 z 距岸线）、太阳高光 + 菲涅耳、近岸更透明，海底始终可见。波形公式与 `Floater.Wave` 逐项一致（参数 Config.WaveAmp/Len/Speed）。
4. 夜灯：码头左舷 3 盏灯笼 + 小屋朝相机一面的两扇窗，共用发光材质 M_glow + 点光源，亮度 = 时段 Glow；网页端只留前 Config.WebLamps=2 盏点光源。
5. 后期：全局 Volume（Settings/PostFx.asset）：Neutral 色调映射、暖色 WhiteBalance/ColorFilter、柔和 Bloom、轻暗角；网页端运行时关 Bloom。
6. 验收：`Warm.exe -shots <dir>` 固定机位（Config.ShotFocus）四时段各截一张 light_{morning,noon,dusk,night}.png 后退出；图传 iMac `码头垂钓/preview/`。

## Phase 7 — 动作（v3.2，提交 `anim-N`）
1. 骨架（以后正式模型沿用）：hips/spine/chest/neck/head(+hat)，左右 shoulder/upperarm/forearm/hand，左右 thigh/shin/foot，rod_mount 挂在 hand.R。`blender/rig.py` 产出简模人偶（胶囊身体 + 大草帽）+ 8 段原地烘焙动作 AN_idle/walk/cast/wait/bite/reel/catch/lose → assets_raw/character.glb；clean.py 只要求 idle/walk/wait/reel 首尾帧一致。
2. Unity：BuildCharacter 建 8 态 Animator（idle↔walk 由 Speed 切换，walk 播放速度 = 实际速度 / Config.WalkAnimSpeed 防滑步）；CharacterRig 把 FishingStateMachine 的状态交叉淡入到对应动作，并按张力弯三节鱼竿；鱼竿挂在 rod_mount 上。
3. Tackle：贝塞尔下垂鱼线、抛物线抛投、落水/咬钩/挣扎水花（单个 ParticleSystem 反复 Emit＝对象池）、浮漂随浪/咬钩下沉、鱼游近浮漂/水下挣扎往外拖/钓上跃出；船与浮漂用 Floater.Wave（与水面 shader 同一公式）。
4. 演示：`Warm.exe -demo` 角色自动走到码头尽头完整钓一条鱼；`-frames <dir>` 让游戏以 30 fps 游戏时间逐帧截图 900 帧后退出，再用 ffmpeg 便携版（D:\tools\ffmpeg，BtbN 构建）合成 30 s → iMac `码头垂钓/preview/demo.mp4`。（gdigrab 抓窗口会被别的窗口遮挡/拿到旧画面，弃用。）
测试：EditMode 全绿（新增 CharacterClipsAndFacing：8 段动作齐全、循环设置正确、持竿姿势下挂点在角色前方）；照常 Phase 5 两端打包 + 冒烟。
