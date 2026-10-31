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
- stage-4：游戏里还没有任何获得砖的途径，所以「先 Stock 里有 1 块砖再放」的做法是：**新开局（没有存档）库存给 `TestBricks = 1` 块**，B（P2 小键盘 2）放到面前那一格；存过档就以存档为准。格子＝`floor(x / CellSize)`，砖心在格子中心、离地 BrickSize/2；只认沙滩上、IsWalkable 为真、不在码头上的格子。暂停：`Pause.On` 为真时 PlayerController 不读移动、FishingStateMachine 整个 Update 跳过（不读输入也不走计时）、BaitSelector 不读键；鱼获、张力条、饵按钮、F3、屏外指示点都在暂停时不画；TimeOfDay.frozen 置真、恢复时还原原值。暖纸底是半透明的（α 0.55；0.72 时菜单后面的砖太淡），菜单后面的砖还看得见。中文按钮用 OnGUI 默认字体：桌面 Windows 会回退到系统字体能显示，**网页端没有系统字体回退，中文可能显示成方块**，未验证。

## 美术打磨 art-1~4（2026-10-30，用户整份下达）
- 流程：`blender/model.py` 程序化低模 → assets_raw/*.glb → `blender/clean.py` → assets/SM_*.fbx → unity/Assets/Art/。clean.py 只改两处：model.py 已按色卡起名的 `M_*` 材质保留原名（角色材质照旧编号成 M_character_N）；回读校验容忍几个 FBX 共用色卡材质名时 Blender 加的 `.001`。STATIC 清单加了 art-3 的 7 件。
- 坐标：Blender X/Y/Z = Unity X/Z/Y。依据是场景里 SK_character 实例被 BuildCharacter 转了 180°：rig.py 角色面朝 Blender -Y，到 Unity 是 -Z。
- 颜色：模型的材质名 = 场景里同名的 Config 色材质（M_wood、M_wood_dark、M_coral、M_cliff、M_leaf、M_trunk、M_bedding、M_glow），`BuildScene.Model` 按名字换成 Assets/Art/Materials/ 下的同名材质，颜色只认 Config，没加新色。`BuildNature.Mat` 统一光滑度 0.1（粗糙度 0.9）；模型自带材质（角色）由 `WarmImport.OnPostprocessMaterial` 同样设 0.1。**没做 512 手绘图**：木板缝靠木板侧面的深木色在板缝里露出来，帽子色圈用几何环带换材质。
- 小屋：山墙朝相机（-Z），两扇窗，窗玻璃用 M_glow，随时段一起亮；窗边有珊瑚色百叶，窗下有花箱。门在 +X 朝床，门洞往里退 0.1 m（不做室内），门口一级石阶。屋顶三排错台瓦（珊瑚色），加屋脊压条、石烟囱、石基。包围盒 x∈[-9.55,-6.45]、z∈[-4.67,-2.33]。BuildFx 原来贴上去的两块发光方块（CabinWindow）删掉，只留窗前那盏点光源。
- 船：7 个站位放样，空舱，船舷外翻（舷缘比舭部更靠外），一根横座和船头柱；外壳上沿和船尾板刷珊瑚色。吃水不变（BoatPos y=-0.2，底面中心是原点）。礁石：两团凸包拼一块，底面压平，朝下的面删掉。
- 渔人（rig.py 只换 build_body）：躯干改成前后压扁的棱台，加深色腰带、斜肩。袖子卷起露出小臂，手做成连指手套加拇指，裤脚翻边，靴头往前错出一截。大帽檐半径 0.44 不变，外圈加一道深色环；帽顶两道深色环；脸上两个深色眼点、一个小鼻子（眼点比第一版下移 1.6 cm，不然被帽檐挡住）。6 个材质名不变；帽带环几何不变（P2 在 Unity 里那圈绿色还盖得住）。
- 小件：树是三层尖冠，随机数抽取顺序不变，所以位置、大小和原来一致；朝向按位置算。另有木箱、木桶。灯笼灯身用 M_glow，配深色角柱和小顶，点光源放在灯身中间。床拆成 SM_bedframe + SM_bedding 两件，名字 BedFrame/Bedding 不变。木板的材质槽 0 是顶面 M_wood，侧面深木色。模型不带碰撞体：`BuildScene.Solid` 按网格包围盒给木板、箱、桶、床补 BoxCollider，树只补树干大小的盒子。测试加了一条：木板碰撞体顶面 = GroundY。删掉不再用的 BuildNature.Cone 和 Generated/cone.asset。
- 面数（三角形）：小屋 382、船 200、礁石 59、渔人 1108、树 33、木箱 150、木桶 134、灯笼 64、床架 90、床垫 36、木板 10。
- 网页中文：实测 Unity 网页端 OnGUI 中文完全不显示（按钮和提示框是空的，没有系统字体可以回退）。加了 `Assets/Fonts/WarmCJK-Regular.otf`：思源黑体 CN 子集，只含暂停菜单和底部提示用到的字，另含 stash 里鱼换砖两句的 7 个字，8.6 KB。只赋给 Pause 和 Bricks 的 GUIStyle。OFL 给 "Source" 保留了字体名，子集算改动版，所以内部名字改成 WarmCJK，原许可证放在 WarmCJK-OFL.txt。
- 工具坑：Windows 系统环境变量 OCIO 指向 Blender 5.2 的配置，Blender 4.5 读不了会报错，不影响导出，渲预览时去掉这个变量。网页端实测发键必须带真实扫描码（`MapVirtualKey`）：浏览器的 `KeyboardEvent.code` 由扫描码算出，keybd_event 扫描码填 0 时 Unity 网页端收不到键；桌面包读虚拟键码，所以以前没出问题。
- 鱼换砖（上一轮没提交的改动）开工前收进了 git stash（iMac 和 Windows 各一份），没进 art 提交，新包里也没有。

## 玩法 play-1~4：一天的循环（2026-10-30，用户整份下达）
- stash「鱼换砖」（iMac、Windows 各一份，内容相同）：只留下对得上的两点，按本轮规则重写：钓上就进该玩家的 Stock 生鱼；新开局不送砖（保留 `TestBricks` 常量，值改成 0，原 stash 是删掉这个常量）。对不上的都丢掉：随处可换、「换到一块砖 / 先钓一条鱼」、左上角改成库存数。两份 stash 都已 drop，没有第二套兑换。
- play-1：`FishingStateMachine.Credit` 在 CaughtCount 增加时把差额记进这名玩家的生鱼，FishingLogic 不碰 Stock。左上/右上原来的字不动，后面接「生鱼 n」，这几个字用 WarmCJK 单独画。WarmCJK 用第一次下载的思源黑体原文件重新切子集，没有再下载：现在只含暂停菜单、三句底部提示、「生鱼」和 0–9、空格，art-4 时为 stash 预留的字已去掉。
- play-2：小屋门口 `Config.DoorPos = CabinPos + (1.4, 0, 0)`，取在 +X 门外的石阶上（按 art-1 的小屋模型算：对中后墙在 +1.225，石阶外沿在 +1.575）。人离门口 ≤ InteractRange、手里有生鱼，按 E（P2 小键盘 1）才扣 1 条鱼、加 1 块砖，提示「换得一砖」；没鱼或不在门口时什么都不做，也不弹提示。注意 BedPos 离门口约 1.6 m，所以 P1 站在床边本身就算在门口。底部提示改成 `Bricks.Toast(文字)`，放砖、睡醒共用。
- play-3：同格往上垒。`Bricks.CanPlace(s, 格, 层)` 的条件是：这层空着、下面一层有砖（第 0 层除外）、层号 < MaxHeight、手里有砖，再加上原来的沙滩、可走、非码头。按 B 时放在 `Height(格)` 那一层，即从下往上连着垒的层数。存档格式不变，BrickRecord 本来就带 layer。两参数的 `TryPlace(s, 格)` 保留，作为放第一层的接口，子阶段 4 的老测试照旧通过。
- play-4：在床边（离床中心 = 床点 + BedFromSpawn ≤ InteractRange）按 R（P2 小键盘 4），`PierSave.Sleep` 把时间设到 `Morning`（Config.TimeNames 里 morning 那一档，t = 0），两人回床边（P2 偏开 0.7），立刻 `Save()`，底部提示「天亮了」，不退出游戏。自动存档：`Stock.dirty` 连续超过 AutosaveSec（30 秒）就自己存一次，失焦存档照旧。新开局走 `PierSave.NewGame()`，即 TestBricks = 0。熟鱼和布保持 0，H、Ctrl 这轮没做。为了不超 150 行，CoopTests 里原有几条测试收紧了排版，断言一条没改。

## 自迭代环（2026-10-31 起，用户授权：自己设计、自己选题、5 亿 token）
- 环的规则、选题表、账本、工作手册放在仓库外的 `scripts_local/loop/`（CLAUDE.md §1.8 不许仓库里加文档）。每圈一个提交 `loop-N <slug>`，提交前过一键闸（场景 → EditMode → 桌面包 → 网页包 + web_check）和实玩闸（真键 + 截图 + 读存档），再由独立审查看图看 diff。
- loop-0：WarmCJK 改成按代码自动切子集（`scripts_local/font/cjk.py`）：收集 Scripts/*.cs 字符串字面量里的全部非 ASCII 字，外加全部可打印 ASCII（提示里的按键字母在网页端也能显示），129 个字形、17.7 KB；一键闸先查缺字，缺字就不开工。另外钓上一条时不论是不是演示都打一行 `CAUGHT p<玩家> <鱼> t=…`（演示模式前面多个 `DEMO-`），实玩脚本靠它判断钓没钓上。
- loop-1 砍树得木：新文件 `Trees.cs`，由 BuildNature 挂在 Nature 上，子物体里名叫 Tree 的就是树，生成顺序 = 日志里的 tree=k（树的位置、模型、随机数顺序都没动）。人离树干 XZ ≤ `ChopRange`（1.6 m）按 E 砍一下：树绕树根（模型原点）左右摆 0.3 s、最大 5°、幅度衰减到 0 后回原旋转，打 `EVT chop p<i> tree=<k> hits=<h>`；第 `ChopHits`（3）下 Stock.wood +1、提示「得一木」；出过木的树再按提示「这棵明天再砍」，不加木、不晃。砍了几下记在静态 `Trees.Hits`，不进存档（开局清零），`PierSave.Sleep` 调 `Trees.NewDay()` 清零；PierSave 已 149 行，这句和 PlaceAtBed 写在同一行。自然过完一天（时间转一圈）不重置，只有睡醒重置。E 的读键从 `Bricks.Update` 挪到 `Trees.Update`，`Trees.Interact` 先试 `Bricks.Trade`（门口有生鱼先换砖），不成再找够得着的最近一棵；场景里门口离最近的树 6.7 m，门口 2 m 与砍树 1.6 m 两个范围碰不到一起，先后只在测试里假放一棵树验。顶部正中常驻「砖 n　木 n」画在 `Bricks.OnGUI`，与底部提示共用一个 WarmCJK 的 box 样式，暂停时都不画。新测试放 `PlayTests.cs`（5 条）。
- loop-2 锁屏实玩：新文件 `ScriptedPlay.cs`。`Warm.exe -script <plan.json>` 时由 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 建一个物体（不改 GameBootstrap），设 `runInBackground = true`，等 SCENE-READY 后按计划走。词同 play_input.ps1（wait/down/up/tap/until/shot），另加 save（`PierSave.Save()`）、quit；click 打 `SCRIPT-SKIP click` 跳过。键码是 Windows 虚拟键，文件里映射成 KeyCode。注入：把 `InputManager2P.Held/Pressed` 换成脚本自己的两个集合，真键盘不再进来；`DefaultExecutionOrder(-1000)` 让它每帧最先跑，帧首清空「本帧按下」再推进计划，所以 Pressed 只在按下那一帧为真；点一下按住 80 ms（同 play_input.ps1）。until 订阅 `Application.logMessageReceived`，超时打 `SCRIPT-ABORT until <正则>` 并 `Application.Quit(5)`。shot 用 `ScreenCapture.CaptureScreenshot(绝对路径)`（帧末截，带 OnGUI），截完等一帧。计时一律 `Time.unscaledTime`。Esc（Pause）、T（TimeOfDay）、F3 由各脚本直接读 `Input`，不走 InputManager2P，脚本模式发不了，这圈没改。`BuildPlayer.Windows` 读 `-buildOut <目录>`（默认仍是 Build/Desktop）：闸把测试包打到 Build/Loop，不再和用户开着的 Build/Desktop 抢文件。网页端不读命令行，什么都不做。PlayTests 加了键码 9 条、计划缺字段 1 条。
- loop-3 就近提示：新文件 `Hints.cs`，挂在 Game 上（BuildScene 赋 WarmCJK）。每帧对每名玩家调纯函数 `Hints.Text(库存, 玩家号, 位置, 朝向, Trees.Pos, Trees.Hits, 钓鱼空闲)`，只列「现在按了就会成功」的：门口有自己的生鱼 → 换砖；否则 `Trees.Nearest` 那一棵今天还没出木 → 砍树（E 实际只砍最近那棵，它出过木就不提示，哪怕旁边还有新树）；`PierSave.NearBed` → 睡觉；面前那一格 `Bricks.CanPlace(格, Height)` → 放砖（同 Bricks.Update）；`Config.InFishSpot` → 抛竿。按这个顺序取前 `HintMax`（2）个，全角空格隔开；钓鱼不空闲返回空串。判断全用现成方法，规则一条没改；`Trees` 的树位置从私有实例字段改成 `public static Vector3[] Pos`（同 Hits），`BaitSelector.KeyName` 改成 public 供提示沿用饵按钮的键名（P2 Num1/2/4/5），P1 的 Space 叫「空格」。画法：角色根 + `HintHeight`（2.2 m）投到屏幕，字的底边在这一点，`HintFont` 16 号，`GUI.skin.box` 半透明底（同底部提示），夹在屏幕内；两张字条叠在一起（开局两人在床边只隔 0.7 m，第一版实玩两条字压在一起）就 `Hints.Apart` 左右分开、各让一半；人在屏幕外（帽子高 1.8 m 的点不在视口里，同屏外指示点的判据）、暂停时不画。砖是两人共用的：手里有砖时站在沙滩上的人几乎总有「放砖」，所以开局 P2 头顶是「Num4 睡觉　Num2 放砖」，不是规格写的「Num4 睡觉」。PlayTests 加 7 条。
