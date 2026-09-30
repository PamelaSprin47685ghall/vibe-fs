# resources/ 语义合并审查报告（2026-09-30）

审查范围：昨天（09-29，PR#46 `c07b5ca79`）并入 upstream 的 commission 4 文件，以及 09-30 上游继续落地的 assume/todowrite（`f1c956c73`）、pair guideline（`23b604c30`）、chronicle（`8f9485c12`、`1a45a31df`、`e22c40ba3`）等语义级改动。基准为当前 `upstream/master`（`5321fba04`）。

方法：逐文件比对合并前版本（`1a4b1e0b4^`、`f1c956c73^`、`1a45a31df^`）与合并后现状；交叉核对资源文件与代码引用 key、双语一致性、陈旧术语。

---

## 发现的问题

### P1 [高] pair-programming-guideline 第 4 条中英文标题语义不一致

**文件**：`resources/provider/host/pair-programming-guideline/{en.md, zh-CN.md}`

- 英文标题：`4. [Update the ledger]`
- 中文标题：`4. [进度更新]`

两份正文都明确否定“进度播报”（英文 *never an optional progress broadcast*；中文 *绝不是可有可无的进度播报*），核心概念是“账本（ledger）”。英文标题与正文一致，中文标题却退回到被否定的“进度”表述，自相矛盾。

**建议**：中文标题改为 `[更新账本]` 或 `[铁账本]`，与正文“要把 todowrite 工具当作记录工作事实的铁账本”一致。

### P2 [中] pair-programming-guideline 第 3 条中英语气不对称

**文件**：同上

- 英文 `[Do not skimp]` 正文点名旧习惯是 *a habit left over from the era of scarce context*，并给出明确替代：*read generously, even the whole picture*。
- 中文 `[不要吝啬]` 也点名“上下文匮乏时期留下的错误习惯”，但收尾只到“有助于你的工作”，没有英文里 *it helps your work* 之后那种行动指向的强调。

**判断**：语义实质相同，属可接受的自由翻译；但若追求严格对等，中文可补“不要拘泥于片段”。低优先级。

### P3 [低] `tool/sphinx` 资源目录无代码引用

**文件**：`resources/provider/tool/sphinx/**`

反向扫描：所有 `resources/provider/tool/*/` 中仅 `tool/sphinx` 未被 `src/Wanxiangshu/**/*.fs` 以 `"tool/<name>/..."` 形式引用。

**判断**：Sphinx v2 走 MCP host（`src/Wanxiangshu/Sphinx/V2/Hosts/Mcp/Server.fs`），其工具 schema 由代码内 `Contract` 定义，不经 OpenCode tool 资源体系加载。属架构预期，非缺陷；但若该目录是为旧入口遗留，应确认是否可删。需业务确认。

### P4 [低] enforcer 无关文件中出现“进度更新”自然表述

**文件**：`resources/enforcer/status-announcement-noise/main.zh-CN.md:30`

> 好进度更新不是频繁地证明工作存在……

**判断**：这是 enforcer 规则说明文，与 pair-guideline 第 4 条术语无关，语境成立。不构成问题，仅记录以免误报。

---

## 核对为“正确”的项（无问题）

| 项 | 结论 |
|---|---|
| commission 4 文件（PR#46） | 中英文本语义对齐；`Coordinator / Lead` → `lead` 术语替换完整；与代码无冲突 |
| assume（`f1c956c73`） | description 447→5 行属“restore”语义重写；`arg-assumption`、`committed` 双语对齐；与 `AssumeTool.fs` 的 `assumption` 单参数契约一致 |
| todowrite（`3cab3a168`） | provider 面字段已改 `obligations`，代码 `TodoWriteCompressionContract.fs` 明确 `providerListField = "obligations"`、`hostListField = "todos"` 双向映射；`arg-retain-checkpoints` 双语对齐 |
| chronicle（`8f9485c12`/`1a45a31df`） | 四个必填字段 `charge/occurrence/settlement/consequence` + 必填 `tip` 与 `ChronicleTool.fs:106,114,151-173` 契约吻合；description/missing-tip/nothing-to-remember/remembered/arg-tip 双语对齐；“按序连成一个自然段”的新语义在 en/zh、role/blogger、companion/normal、companion/squash 五处一致落地 |
| role/blogger、companion/normal、companion/squash | 与 chronicle description 同步加入“去掉字段名连成一段”的表述，双语对齐 |
| ablation/tool-map.json | 新增 `"todowrite": "obligation-ledger"`，与字段改名同步 |
| 资源 key ↔ 代码引用 | 全部 `"tool/<name>/<arg>"` 引用均有对应 en/zh-CN 文件，无缺失 |
| 陈旧术语 | `delegate_readonly_rounds`、`self_note`、`assume(update, todos)`、`Coordinator / Lead`、`SessionExecutionBinding` 在 resources/ 全库零命中 |

---

## 处置建议

- **P1 建议修复**（一处中文标题）。
- P2/P3/P4 无需代码改动；P3 待业务确认 `tool/sphinx` 目录去留。
- 本次审查未修改任何 resources/ 文件。
