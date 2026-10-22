# CUT-32：可独立验证的执行设施

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-32)。

## 1. 认领

- Owner：`eng-cut06-goose`；认领提交 `4f809865e`；base 主仓 `4f809865e`。
- 写集合：仅本卡文档、README §9 一段、BOARD 行。没有改 `scripts/`、`requirements/verification-system/` 或任何生产代码。

## 2. 裁决：RETAINED（保留为主仓独立 CLI/脚本，不新增库）

CARDS.md 明示"设施可以保持独立 CLI/脚本，不为了'所有东西都是 F# 库'把成熟工具再实现一遍"。盘点结果：本卡要求的能力在主仓已有成熟 owner，且全部是 Node 进程/文件系统物理设施（进程组、`ps`、只读挂载、SDK 归档），既不是可复用的纯机制，也不适合 Fable 库（wanxiangli 的模块形态是 F# 纯工厂 + 对象表达式）。移入库只会产生第二份 supervisor，违背"重复的验收实现可删，证明义务不能删"。

| 职责 | 现有 owner | 证明（verification-system WHAT 条款 → 测试） |
|---|---|---|
| 测试运行与权威计数 | `requirements/verification-system/tests/run.mjs` + `tests/e2e/support/supervise-node-test.mjs` | [021] → `021.test.mjs`（汇总/干净退出不能掩盖文件未排空、冲突结果不能抹掉失败） |
| 因果静默看门狗 | 同上（verdict-silence 窗口） | [006] → `006.test.mjs` |
| 子进程清理 | supervisor 的进程组校验与回收 | [006] → `support/normal-exit-residual-tests.mjs` |
| 日志与 verdict 分离 | supervisor UTF-8 转发 stderr，stdout 专用协议 | [021] → `021.test.mjs` "nested supervision preserves child output…" |
| 固定输入/工具/产物 | `scripts/lib/verification-*.mjs`、`verify.mjs` 快照 | [016] → `016.test.mjs` 及 `support/*-tests.mjs` |
| 构建新鲜度 | `tests/support/build-freshness.mjs` | run.mjs 拒跑（见 §4） |
| 静态门禁可红 | `scripts/check.mjs` + `scripts/checks/*` | [004][009] → `004.test.mjs`、`009.test.mjs` |

本卡反例 → 现有防线：

- 诊断字节污染结果协议 → [021] nested supervision 用例。
- 未收完文件却报告全绿 → [021] "summary and clean exit cannot disguise missing file completion"、"aggregate drain cannot replace a planned file stream drain"。
- TODO/skip/取消当 pass → supervisor 对 cancelled/container 失败直接失败，skip/TODO 报 partial evidence；[021] "cancelled and TODO failures are not ordinary assertion verdicts"。
- 子进程残留被忽略 → 残留进程组被回收且"the run still fails"（[006] residual 用例）。
- 修改后恢复哈希绕过固定输入 → [016]（运行中改写即中断，严禁以前后哈希相同冒充）。
- 从旁边 checkout 借产物 → [016]/dotnet-sdk 外借 SDK 反例。

## 3. 为切刀提供的窄入口

不新写 runner：`TESTS_MJS_FILES=<本卡用例> node requirements/verification-system/tests/run.mjs` 已是"更窄但完整"的入口（只跑列出文件，但保留新鲜度门禁、计划文件排空、权威计数、进程组回收、cancel/skip/TODO 不计通过，并公告范围收窄）。README §9 已补这一段。实测见 §4。

## 4. 实测（本机，Linux，Node v20.19.2，低于 engines `>=22.13.0`）

1. CUT-30 用例经窄入口（build generation 9 新鲜）：`runner: 45 passed, 10 failed; 13/13 planned file(s) completed`，进程组 `accepted=true`，6 项 TODO 逐条列出，exit 1。与裸 `node --test` 的 61/45/10/6 一致，且多出排空与回收证明。
2. 新鲜度门禁：master 前进后未重建即运行，`runner: refusing to run. Generated inputs changed (digest mismatch …)`，exit 1，未产生任何通过数。
3. 设施自证（`021/006/005/009`，`--skip-staleness-check`，这些用例不读 dist；该旗标会在 stderr 公告结果不代表当前源码）：`108 passed, 7 failed; 4/4 planned file(s) completed`，exit 1。7 个失败全在 `006` 的 worker cost / 残留用例，原因是 `worker cost … "status":"missing","reason":"Pre-import observation not received"`：本机 Node 20 没有收到 pre-import 观测，这些用例期望 `complete`。这是环境低于 engines 造成的，本卡不改代码，也不把它记为通过。

## 5. 退出与已知不足

- 没有可删的重复实现：库侧没有第二份 runner；主仓 supervisor 是唯一 owner。日志不授予流程权威，这一点不变。
- 平台范围：Darwin 只读挂载 native 用例在 Linux 上是声明的 skip，不能算 Darwin 通过（见 `requirements/verification-system/tests/README.md`）。
- 本机 Node 低于 engines，`006` 有 7 个环境性失败，需要在 Node ≥22.13 上复跑才能确认设施全绿。全量 `verify.mjs` 与 integration 未跑。
- 附带发现（不在本卡写集合，未改）：在当前 master 上运行 `node scripts/derive-envelope.mjs`，会从语料（4,718,636 token，入库产物为 5,348,040）派生出与入库包络不同的 normal/min（155.68/82.86 vs 155.12/81.15）。按 degeneration-guard WHAT，包络只能手动更新，因此已经还原，没有提交。是否重派生由仓主决定。
