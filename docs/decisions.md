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
