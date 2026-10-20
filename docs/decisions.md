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
