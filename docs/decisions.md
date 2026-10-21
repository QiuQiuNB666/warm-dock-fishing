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
