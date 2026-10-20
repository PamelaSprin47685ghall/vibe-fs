# CUT-16：压缩覆盖与 checkpoint

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-16) / [证据](../evidence/CUT-16/README.md)。

## 1. 认领

- Owner：`eng-cut06b-goose` 认领（`0c9ae2c3f`），库侧子范围①完成后中断；`eng-cut16c-goose` 接手同一工作树 `wt/CUT-06-main`（分支 `cut/CUT-16`）完成接线、复验与落地；`eng-cut16d-goose` 接手子范围②及以后。
- 写集合（子范围②）：`src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs`（view 检查点结算段）；wanxiangli `src/compression/`。
- 写集合（子范围①）：`src/Wanxiangshu/Repository/Programming/Js/Anchor/{AnchorModel.fs,AnchorModel.fsi,AnchorSurface.fs,AnchorView.fs}`；wanxiangli `src/compression/`（新模块）。与 CUT-15 的 `Context/Prefix`、`HostCompactionGate` 及 CUT-25 的 `Process/JsSandbox` 不相交。

## 2. 处置

| 职责 | 现状核对 | 去向 |
|---|---|---|
| ANCHOR view 声明形状（`{ prefixUntil, thenMap }`，首处违约原因） | `AnchorSurface.parseDeclaration` 内联手写校验 | **抽出** → `wanxiangli/compression.parseView`，原因文字逐字保持 |
| 存活锚点分区（0 恒存活，被替换的真实锚点不复活） | `AnchorViewDeclaration.live` | **抽出** → `compression.live` |
| 永久区间替换计划（边界在替换前拍下，`prefixUntil` 之前与已退出锚点静默忽略） | `AnchorViewDeclaration.plan` | **抽出** → `compression.plan` |
| 按 history 先后折叠（覆盖区间连同此前摘要、opening 行、kept/summarized/folded/cached 计数） | `AnchorView.apply` 内 `replace` 折叠 | **抽出** → `compression.project`（返回指回输入的 index，宿主据此取原消息） |
| 编号台账（`AnchorLedger`：会话内 messageId→锚点号、待用声明、history 存储） | 主仓 | 留主仓（宿主会话状态） |
| o200k token 计量、摘要渲染回宿主消息（`summarizedMessage`/`openingSummary`） | 主仓 | 留主仓（宿主线格式与分词器） |
| view 检查点追加准入（每个 completed 调用恰好一次、在途重复 join、error 封口、WriterUnavailable 释放、终局失败不重写） | `PluginHooks` 内 `settledViewCheckpoints` + 五个局部函数 | **抽出** → `compression.checkpoints()`（子范围②，见 §7） |
| 检查点真实 journal 写入、在途 Task 会合、`JournalAppendFailure` 结局名映射 | `PluginHooks` | 留主仓（宿主 I/O 与等待者） |
| todo hook / 真实压缩候选接受拒绝 / 退休重启恢复（CARDS 顺序 3–4） | todo hook、Host 压缩路径 | 见 §6 |

## 3. B. 契约（wanxiangli/compression 0.21.0）

`Compression()` 返回 `{ parseView, live, plan, project }`，每条消息无状态、输入先完整校验（拒绝 accessor 字段与稀疏数组），回答冻结。详见库 `src/compression/AGENTS.md`、`index.d.ts`，测试 001–003（库侧 16 pass / 0 fail，含 exports 一致性）。

## 4. C. 反例

- 形状：多余键、缺 `prefixUntil`/`thenMap`、负数/小数/字符串锚点、`"01"` 这类非规范键、空白摘要 → 库 001；主仓 repository-programming 054/056/059。
- 覆盖：被替换锚点复活、后来的锚点落入旧替换区间、`prefixUntil` 前的锚点被改写 → 库 002；主仓 context-compression 031/032、work 020/034。
- 输入事后被调用方改动 → 库 003。
- 抽取后行为漂移 → 通过公开 JS 面（applyView/captureView/anchors/parse/reportMaterial）新旧双算差分。

主线行为本来正确，无可伪造的红；以负控（变异体被差分抓到）+ 差分 + 逐用例对比作证。

## 5. D–H

- D：库 `a8292d9`（实现 + 测试）、`468991f`（release 0.21.0，已在 origin/main，以 0.20.0 `b6b05e2` 为祖先）。
- E：submodule + `file:wanxiangli`；接线时 gitlink `b6b05e2`（0.20.0）→ `468991f`（0.21.0）；落地前 rebase 时 master 已由 CUT-25 把 gitlink 推进到 `d2a9f3b`（0.23.1，以 `468991f` 为祖先）并同步 lock，故本提交不再改 gitlink 与 lock，只在 0.23.1 上重建复验。
- F：`AnchorCoverage` 持有一个 `Compression()` 实例，把 F# 记录映射成纯 JS 记录交给库并映射回；`AnchorViewDeclaration.live/plan`、`AnchorSurface.parseDeclaration`、`AnchorView.apply` 的签名与所有调用者（Advisor View、PairProgrammingThoughtTransform、ToolRegistry、DeliveredValues、Fission Tool）不变；产物 `dist/Repository/Programming/Js/Anchor/AnchorModel.js` 实际 `import { Compression } from "wanxiangli/compression"`。
- G：主仓删除手写形状校验（`isAnchor`/`isRecord`/`keys`/`pairOf`/fold）、`live`/`plan` 内联实现、`AnchorView` 的 `Row`/`replace`/`messageOf` 折叠与三处计数。接手时发现前任未编译过的两处 `obj`→数组类型约束错误（`unbox` 修正）。
- H：见证据：32 个相关测试文件 base/candidate 逐用例 verdict 完全相同；差分 3×1500 轮共 270000 次比较 0 分歧，变异体（替换边界 `>`→`>=+2`）在 300 轮内被抓到 457 次。

## 6. 余项（本卡仍为 DOING）

- 子范围②：已完成，见 §7。
- 子范围③：todo hook（todowrite 失败不落 checkpoint、不重写原生 todo 列表）与压缩候选接受/拒绝、退休/重启从原件恢复。
- 已知不足：本机 Node 22.21.1 下有 7 个文件中 13 个用例既有失败（base 同样失败，见证据），全量 verify 与集成 run.mjs 未跑。

## 7. 子范围②：view 检查点追加准入（eng-cut16d-goose）

- B 契约：`Compression().checkpoints()` → `{ observe(terminal), settle(call, outcome) }`，键为长度前缀的 `(session, callId)`；见库 `src/compression/AGENTS.md` LEMMA 5 / SPEC 4、`index.d.ts`。
- C 反例：重复终态、在途重复、error 后 completed、键别名、WriteUnknown/NoNewWriteReleaseFailed/FactRejected 重试、WriterUnavailable 不释放、accessor/非法状态 → 库 004；主仓 obligation-ledger 005（集成层）。行为本来正确，无可伪造的红；以三处库变异体被主仓 005 抓到作负控。
- D：库 `5f15de3`（实现 + 测试）、`a699b95`（release 0.25.1）。
- E：gitlink `40265dd` → `a699b95`，`package-lock.json` 中 `wanxiangli` 版本同步为 0.25.1。
- F：`PluginHooks` 持有一个 `checkpoints()` 台账；`observe` 回答 `append` 时宿主才写 journal 并登记在途 Task，`join` 取同一 Task，`skip` 直接完成；写入结局经 `settle` 决定是否摘除在途 Task。
- G：删除 `settledViewCheckpoints`、`removePendingViewCheckpoint`、`releaseUnattemptedViewCheckpoint`、`startTerminalViewCheckpoint`、`settleNewViewCheckpointTerminal`、`joinPending`。
- H：72 文件 450 用例 base/candidate 逐用例相同；集成层 obligation-ledger 001–005 + work 020/034 42 用例相同；变异体 M1–M3 均被抓到。见[证据](../evidence/CUT-16/README.md) §6–9。
