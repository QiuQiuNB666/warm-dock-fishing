# decisions.md

## Phase 0 前置检查（2026-10-21，限额试跑，MacBook）
BLOCKED: Unity 未装 / license 无法检查 / ~/.tripo_key 缺 / 磁盘 13 GB<15 / ref/reference.jpg 缺

| # | 项 | 结果 |
|---|---|---|
| ① | Blender --version | PASS 4.5.9 LTS |
| ② | Unity + WebGL/Windows 模块 | FAIL 未装（/Applications/Unity/Hub/Editor 不存在） |
| ③ | Unity license | FAIL 无 Unity，无法检查 |
| ④ | ~/.tripo_key | FAIL 缺 |
| ⑤ | df ≥15 GB | FAIL 13 GB |
| ⑥ | ref/reference.jpg | FAIL 缺 |
| ⑦ | Google Chrome | PASS |
| 测试 | Blender -b 打印 OK | PASS |

- 试跑模式：统筹窗口授权绕过硬停，只做不依赖 Unity / Tripo key 的部分（Phase 0 目录 + tripo_gen.py dry-run + Blender 管线占位联测）；Phase 3–5 不碰。
- 源码在 iMac `~/社媒账号管理/码头垂钓/Warm/` 编写，`rsync -a` 单向推到 MacBook `~/Warm/`；git 只在 MacBook。
- 空目录不放 .gitkeep（第 5 节外不建文件），git 里暂时看不到空目录。

## Tripo API 要点（官方文档 platform.tripo3d.ai/docs 查一次，2026-10-21）
- text_to_model：`model_version` 默认 `v2.5-20250123`（脚本显式写死它）；`face_limit` 仅 ≥v2.0 有效；`texture`/`pbr` 默认 true。
- animate_rig：`original_model_task_id`、`out_format` glb|fbx、`rig_type` 默认 biped、`spec` tripo（retarget 只支持 tripo）、rig `model_version` 默认 `v1.0-20240301`（biped 推荐，不传）。
- animate_retarget：`original_model_task_id`＝rig 任务 id；`animation` 或 `animations`（数组≤5）二选一；预设名 `preset:idle` / `preset:walk`；`animate_in_place` 默认 false（脚本设 true，免根位移）；`bake_animation` 默认 true（仅 glb）。
- 结果：GET task → `data.status`（queued/running→success，其余视为失败）、`data.output.model`（rig/retarget 只有这个）/`pbr_model`；脚本优先 pbr_model 再 model。下载用签名 URL，不带 Authorization。
- 响应外层 `{"code":0,"data":{...}}`，code≠0 视为失败（含 balance/credit 字样→BLOCKED）。
- firecrawl 免费额度被限频，改用 exa 读文档（只查这一次）。

## Blender 管线占位联测（trial-3）
- 用法：`$BLENDER -b -P blender/clean.py -- [--src assets_raw] [--dst assets]`；`--placeholders <dir>` 只生成测试 glb（合并版 character.glb 在 <dir>，拆分版 character_idle/walk.glb 在 <dir>/split）。
- check_glb.py 接受目录参数（默认 assets_raw）；角色认 character.glb 或 character_*.glb，动画合计 ≥2。
- FBX 一律不开 bake_space_transform（开了的静态件回读带 +90°X）；**Phase 3 ProjectSetup 的 AssetPostprocessor 必须对所有 FBX 设 `ModelImporter.bakeAxisConversion = true`**，才能满足 Unity 侧「无多余 ±90°」。
- glTF 导入器会给骨骼加一个 icosphere 显示形状，clean.py 导入后删掉（否则被当成身体网格，原点算偏）。
- 拆分版 glb 的动作名是通用的「Animation」，按文件名 character_<tag>.glb 打标签再改名 AN_<tag>。
- 循环闭合：clean.py 把每条 fcurve 末帧值对齐首帧（Tripo 循环若有跳帧，再换交叉淡化）。
- 两种输入（合并/拆分）各一次：CLEAN PASS。占位文件未进 assets_raw、未入库，/tmp/warm_ph 已删。

## 迁到 Windows（v3.1，2026-10-21）
- 运行机改为 Windows 算力机 desktop-ahpct75，项目根 D:\warm；仓库由 MacBook ~/Warm `git bundle --all` 迁来（trial-1/2/3 历史保留，无 remote），MacBook 那份留作备份不再改。
- Tripo key 复制到 %USERPROFILE%\.tripo_key（只校验前缀 tsk_ + 长度 47，未打印）。
- 规格 v3.1：路径改 $BLENDER/$PY/$GIT/$UNITY/$EDGE（见 CLAUDE.md 开头）；python 一律用 Blender 自带的 3.11（系统 python 是商店占位）；日志/临时放 D:\tmp；限时改 Start-Process + WaitForExit。
- web_check.sh → web_check.py（Windows 无 bash，$PY 标准库实现），网页冒烟改 headless Edge。桌面包加本机启动验收：-logFile D:\tmp\Player.log 跑 30 s，SCENE-READY 恰 1 次。
- 不装 Windows IL2CPP（要 VS C++，C: 放不下），桌面仍 Mono。
- Tripo 迁 API v3（官方 migration-v2-to-v3 只读一次）：base https://openapi.tripo3d.com/v3；text_to_model→/generation/text-to-model，animate_rig→/animations/rig，animate_retarget→/animations/retarget，请求体去掉 type，original_model_task_id→input；轮询 GET /tasks/{id}。指南未提响应外层与 output 字段变化，沿用 code/data、output.pbr_model|model；首次真实请求若不符，只查一次文档改。
- Windows 试跑（Blender 4.5.9 便携 + 自带 Python 3.11）：tripo_gen --dry-run 退出 0；占位 glb（合并/拆分）CHECK-GLB PASS ×2；clean.py 两种输入 CLEAN PASS ×2。占位与输出在 D:\tmp，未入库。clean.py/check_glb.py 无需改动。
- 工具链（D:）：Blender 4.5.9 便携 D:\tools\blender-4.5.9；MinGit 2.56.0.2 D:\tools\mingit；Unity Hub D:\Unity\Hub（UnityHubSetup-x64.exe /S，无 UAC；旧地址 UnityHubSetup.exe 已 404）；Unity 6000.3.26f1 + WebGLSupport 于 D:\Unity\Editors\6000.3.26f1（windowsstandalonesupport 自带），已 `editors --add` 登记进 Hub。
- 坑：Hub 无头 install 把安装包下到 %APPDATA%\UnityHub\downloads（C:），4 GB+0.9 GB 撑爆 C: 致 INSTALL_ERROR。做法：安装包挪 D:\tools\dl、TEMP 指 D:\tmp\utemp，手动 `UnitySetup64-*.exe /S /D=<编辑器根>`、模块同 /D。以后加模块照此办理。
- 无头启动：退出码 198，`No valid Unity Editor license found`——待用户在 Windows 桌面登录 Hub 激活个人版许可证。
- win-7：第 5 节 Unity 侧代码已写入 unity/Assets（Editor 4 个、Scripts 10 个 + Game.asmdef、Water.shader、Tests.asmdef + 2 个测试，每个 C# ≤150 行）。**C# 仅过编译检查，未在 Unity 内运行**：用编辑器自带 Roslyn（NetCoreRuntime\dotnet + DotNetSdkRoslyn\csc.dll，netstandard 2.1 + Managed\UnityEngine\*.dll 模块，不引整包 UnityEditor.dll 否则类型重复），Game（Win/WebGL 两套宏）/Editor/Tests 三组 0 错误 0 警告。检查脚本放 D:\tools\jobs\csc_check.py（不入库）。Water.shader 未编译。
- ProjectSetup 用反射建 URP Asset（不引用 URP 程序集），序列化字段 m_MSAA/m_RenderScale/m_MainLightShadowsSupported 缺失即报错退出；Animator 控制器（AC_character）也在 ProjectSetup.MakeAnimator 里，BuildScene 调用；FBX 导入规则（WarmImport）同文件。
- 场景布局：沙滩台地顶面 = 码头顶面 = GroundY 1 m（角色不爬台阶）；瀑布做成 45° 斜崖（相机朝 +Z 俯视，竖直崖面看不见）；角色脚下影子圆片复用 Water.shader（波幅 0、黑色 35%）。色卡仍是占位，Phase 1 读参考图后只改 Config.cs。
- Phase 3 第 1 步改为 `-projectPath D:\warm\unity` 打开已有目录，不再 -createProject。

## Phase 3–5 Windows 首跑（2026-10-21，占位模型）
- **assets/ 是占位模型**（clean.py --placeholders 生成于 D:\tmp\warm_ph → clean.py 正式流程），Tripo 充值后跑 Phase 1–2 替换。
- manifest：加 URP 17.3.0（编辑器自带版本）+ com.unity.test-framework 1.6.0（新建工程的默认 manifest 不含它，测试缺 NUnit 编译失败）；删 inputsystem（本来就没有）/multiplayer.center/adaptiveperformance/androidjni/unityanalytics 与 video/vr/xr/cloth/vehicles/wind/umbra 模块。
- ProjectSetup 反射写 URP 字段 m_MSAA/m_RenderScale/m_MainLightShadowsSupported 在 URP 17.3 下一次通过；Water.shader 编译无错。
- FBX 根节点 90° 旋转修复：Blender 用 -Z/Y 轴导出时把 -90°X 写在根节点上，Unity bakeAxisConversion 管不到。clean.py 改为原生 axis_forward=Y/axis_up=Z 导出，由 Unity bakeAxisConversion 烘进顶点 → 根旋转 0（推翻上面 trial-3 那条「Unity 侧开 bake 即可」的推断）。
- Phase 3 EditMode 18/18（BuildSceneTests 6 + SystemsTests 12）。
- Phase 4：win-7 已含全部 5 个玩法系统，SystemsTests 12/12 首跑即绿，无代码改动（未做逐步追加，只验收）。
- Phase 5：BuildPlayer.Windows 一次成功（unity/Build/Desktop，93.6 MB，Mono），本机启动 30 s Player.log SCENE-READY ×1；BuildPlayer.Web 一次成功（Build/ 10.0 MB Brotli+fallback，index.html 两处注入在），web_check WEB PASS。
- web_check.py：Edge 把页面所有 console 级别都记成 INFO，无法按级别数 error → 改为进程内 http.server（标准库线程，不再单起 -m http.server），只在发给浏览器的 index.html 里加钩子把 console.error / 未捕获异常打 WEBCHECK-ERROR 标记计数；构建产物不改。已用含 console.error 的假页面验证会判 FAIL。
- 汇总默认值：Unity 6000.3.26f1 / URP 17.3.0 / test-framework 1.6.0；桌面 Mono、网页 WebGL2 IL2CPP 剥离 High；URP_High(MSAA4/阴影) 桌面、URP_Medium(0.8/无 MSAA/无阴影) 网页；旧 Input Manager；OnGUI；色卡仍是 Config.cs 占位色；模型仍是占位。
- 未验证（需人工）：Alt+Enter 全屏、走动/钓鱼手感、网页端实际画质档（web_check 只验到 SCENE-READY 与 0 报错）。

## v3.2 光影（2026-10-21，light）
- 用户拍板：不充 Tripo、不用参考图、建模之后做；优先光影与动作。rock/boat/cabin 仍是占位模型。
- 时段 TimeKey（Config.Times）：太阳仰角/方位 晨 14°/-70°、午 62°/-25°、昏 12°/70°、夜(月) 38°/30°；插值改 smoothstep（端点与中点值不变，旧测试照过）。环境光改 Trilight 三色。
- 坑：HDR + Bloom 不加色调映射，午时整片过曝 → Volume 加 Neutral Tonemapping，并压低午时太阳与环境光。午时仍偏亮（沙地接近米白），最终包再降一次。
- 阴影：URP_High m_SoftShadowsSupported/阴影距离 40/2 级联/2048；点光源 per-pixel，High 每物体 4、Medium 2；WebGL 运行时只留 2 盏点光源。
- 水面：近岸浅/远处深按世界 z（岸线 z=0，_ShoreFade 30 m）而不是深度图——网页端不用开 Depth Texture；影子圆片 _Lift/_Spec=0 退化成纯色。
- 浮漂/船改用 Floater.Wave（与 Water.shader 同公式，时间用 Time.timeSinceLevelLoad = shader 的 _Time.y）；船锚点下沉 0.2 m 当吃水。
- 小屋挪到 (-8,1,-3.5)、灯笼在码头左舷 z=2/10/18，让固定机位里能同时看到小屋窗和灯笼。
- 截图：`Warm.exe -shots <dir> -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`，ScreenCapture 在 Player 内截，不用 PowerShell 截屏。
- Game.asmdef 加引用 Unity.RenderPipelines.Core.Runtime / Universal.Runtime（网页端关 Bloom 要用 Volume 类型）。

## v3.2 动作（2026-10-21，anim）
- blender/rig.py：21 骨（规格骨架 + hat + rod_mount），272 面人偶，8 段动作；姿势角度按骨架空间轴给（rest 四元数共轭），与骨骼 roll 无关。RIG PASS / CHECK-GLB PASS / CLEAN PASS。assets_raw/character.glb 入库；rock/boat/cabin 仍用 D:\tmp\warm_ph 占位 glb（clean 源目录 D:\tmp\warm_src）。
- clean.py：ANIMS 扩到 8 段，只对 LOOPS（idle/walk/wait/reel）收尾帧并校验首尾一致。
- 坑：ModelImporter 默认 avatarSetup=NoAvatar → 模型上没有 Animator，BuildScene 静默走了「无骨骼退路」（占位期一直如此）。WarmImport 改 CreateFromThisModel。
- 走路防滑步：rig 走路 ±25° 三角波大腿摆，一循环 1.44 m / 20 帧 = 1.72 m/s（Config.WalkAnimSpeed）；PlayerSpeed 3.5→1.8；walk 状态 speedParameter=WalkScale=实际速度/1.72。
- 朝向：BuildCharacter 用 AN_wait 第 0 帧比较 rod_mount 与 hips，挂点在玩家 -Z 侧就把模型转 180°；测试 CharacterClipsAndFacing 断言。
- 影子圆片改成扁圆柱（原来是方 Quad），桌面运行时关掉（有实时软阴影），只给网页。
- 测试：EditMode 19/19；桌面包 SCENE-READY ×1（94 MB）；网页包 10.4 MB WEB PASS（a6，加 -frames 之前的代码）。
- 演示：-demo 22.3 s 钓上一条（DEMO-CAUGHT snapper）。录屏坑：gdigrab title= 拿到旧画面、desktop 被用户自己开的 Warm 窗口挡住 → 改游戏内 -frames 逐帧截图 + ffmpeg 合成；该版本（a7 作业）在额度收尾时仍在跑，结果未验证。
- PowerShell 变量不分大小写：作业脚本里 $P（工程参数）被 $p = Start-Process 覆盖，导致 Unity 卡在无工程状态 → 改名 $Proj/$g。
- 额度：周额度到 65% 收尾（起 51%）。


## 双人合作子阶段（2026-10-30，用户逐阶段下达）
- 运行方式：用户说的「Unity 官方 Claude Code 插件」本机没装（目录里有，但只有技能文档、不连编辑器）。沿用老路子：iMac 改代码 → Windows batchmode 搭场景/跑测试/打包 → 游戏内 `-shots` 或 PowerShell 发键截图（每次发键前校验前台窗口是游戏）。提交在 Windows 上做（新脚本的 .meta 和场景由 Unity 在那边生成），再用 bundle 拉回 iMac 推 GitHub。
- stage-0：`CabinPos + (1.2, 0, 0.8)` 落在占位小屋网格里（小屋约 x∈[-9.6,-6.4]、z∈[-4.9,-2.1]），按规格沿沙滩 +x 平移 1.6 m；测试断言床和两人都不在小屋包围盒里。床摆在两人身后（`BedFromSpawn`）。单人模式 PlayerManager 除了关 Player2 整棵，也关它挂在 World 下的 Bobber2/HookedFish2/Splash2。GameBootstrap 桌面端也关 ShadowDisc2。M_shadow/M_splash 改成已存在就加载复用。
- stage-1：P2 的 BaitSelector 挂在 Player2 身上（单人模式随它一起关）；P1 的仍在 Game 上。按键状态走 `InputManager2P.Held/Pressed`，测试传自己的函数。
- stage-2：`BedPos` 改为属性——它引用另一个 partial 文件里的 `CabinPos`，跨文件的静态字段初始化顺序不保证。双人相关测试挪到 CoopTests.cs（SystemsTests 超了 150 行），SystemsTests 的钓鱼用例一字未动。`CamFrameMargin = 0.75`。
- stage-3：PlayerPrefs 在 Windows 存于 `HKCU\Software\Warm\Warm`，值名 `pier.save.v1_h928868525`（djb2-xor 哈希）。坏档：解析失败或 version≠1 → 删键、打一行 `SAVE-DISCARDED`、照常开局。
