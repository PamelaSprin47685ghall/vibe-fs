# CUT-12　会话关联与执行事实

返回 [BOARD](../BOARD.md) · [CARDS §CUT-12](../CARDS.md#cut-12)

## 1. 当前状态与前置核对

- **当前列状态**：`DONE`（eng-cut12c-goose 接手 eng-cut12b-goose、eng-cut12-goose，2026-10-21）。四个连续子闭环全部落地：①关联先于首 prompt（§6）②Accepted/ProviderStarted/Terminal 单赋值律（§2–4）③exact 租约复用与新子回收（§7）④重启恢复决议（§6）。范围收窄与余项见 §8。
- **前置**：CUT-04 DONE；CUT-11 DONE（eng-cut09f-goose，d57f886c5）。CUT-11 DONE 后在 master `b10da13c9` 上对真实 Host 链套件复核（§9）。
- **边界**：证据（session、run、root、身份种子、provider run、request kind）的值比较、证据合法性（CUT-07 身份语义）、精确键与证据的一致性、投影与日志写入都留在主仓；库只拿当前相位与两条相等事实（`sameAccepted`、`sameStarted`），给出裁决。不新开第二份账：投影 `ChatExecutionProjectionState` 仍是唯一事实账，库不持有状态。

## 2. 库侧（子闭环 2）

- wanxiangli `ce67a86`：新模块 `src/chat-execution`（`ChatExecutionLaw`，npm 入口 `wanxiangli/chat-execution`），AGENTS.md 写 AXIOM/LEMMA/SPEC，`index.d.ts` 写 plain 契约。`80b0eaf` 发布 **0.44.0**（已在 origin/main）。
- 契约：`schemaRejection(v)`（先于任何证据判断）；`fold(current, fact)` → `set`/`keep`/`reject(reason)`，拒绝原因与旧实现逐字相同；`decideStart`、`decideTerminal`（先核 provider run，再核 disposition）、`settlePreProvider`（先拒 Completed，再看状态）。
- 库测试：`tests/001.test.mjs` 7 条（含卡片反例：重复 ProviderStarted、旧终态污染、pre-start Completed、已启动仍做 pre-provider 结算、终态后再启动）+ exports 3 条，10/10；干净目录 `npm pack` 后从 tarball 导入通过。

## 3. 主仓接线

- `Execution/Session/ChatExecution/Projection.fs(i)`：新增 `ChatExecutionPhaseLaw`（Import/Emit 的类型化投影，未知裁决直接 invalidOp，不猜）、`ChatExecutionLawFold`、`ChatExecutionLawDecision`。
- `Fold.fs`：`applyAccepted/applyProviderStarted/applyTerminal/fold` 的相位判断交库；本地只做证据校验、键一致性、按裁决用自己携带的证据构造下一个 typed 状态。
- `Acceptance.fs`：`ManagedChatProviderLifecycle` 的 `decideStart/decideTerminal` 交库；把裁决映射回原有 typed 错误与见证。
- `Settlement.fs`：`PreProviderSettlement` 的结算判断交库。
- gitlink `wanxiangli` 从 master 的 `d0318dc`（0.42.0）前移到 `80b0eaf`（0.44.0）；lockfile 只改 wanxiangli 版本行。

| 旧实现 | 去向 |
|---|---|
| `Fold.fs` `admitAccepted`、`executionOrInvalid`、`projectTransition`、`advanceProviderStarted`、`settleTerminal`、`unsupportedSchema` | 删除，由库 `fold`/`schemaRejection` 裁决 |
| `Acceptance.fs` `exactStartedEvidence`、`existingStart`、`existingTerminal`、两处相位 match | 删除，由库 `decideStart`/`decideTerminal` 裁决 |
| `Settlement.fs` `validDisposition` 与 `currentDecision` 的相位 match | 删除，由库 `settlePreProvider` 裁决 |
| 证据校验、键一致性、typed 错误/见证构造、读后核对（`startedWitness`/`terminalWitness`/`witness`） | 保留在宿主 |

## 4. 验收（§9）

证据见 [evidence/CUT-12](../evidence/CUT-12/README.md)。

- 差分（[differential.mjs](../evidence/CUT-12/differential.mjs)）：base 手写实现与 candidate（库）对同一输入宇宙双算 **7,656 次，0 处不同**：`fold` 5,940 组（全部 13 种可达相位 × 每种事实 × 证据相同/不同/非法 × 键相同/不同 × schema 1/2），`startWith` 528、`terminalWith` 660、`settleWith` 528 组（带记录追加的内存持久化，比较返回值与追加的事实序列）。
- 变异（[mutants.py](../evidence/CUT-12/mutants.py)）：对 candidate 实际加载的库产物做 7 个语义变异，差分全部报红（3 / 12 / 48 / 36 / 132 / 96 / 12 处不同）。
- 套件（candidate）：managed-chat-execution 全部、crash-reconciliation 全部、delegation-028、host-boundary-026、structured-workflow-012 共 38 文件，177 pass / 1 fail / 15 skip / 20 todo。唯一失败 `host-boundary-026 tool registration compiles without signal routing…`（`opencode-tools-bookkeepertool must not acquire HostEventCodec.fs`）在未改动的 master 源码上同样失败，是既有问题。
- fantomas `--check` 改动文件通过；`node scripts/build.mjs` exit 0。
- 净改进：同一条相位律原先在 Fold、ProviderLifecycle、PreProviderSettlement 三处各写一遍（三份手写 match，任何一处漂移都会让在线决策与重放折叠不一致）；现在只有库一个 owner，有编号测试、差分与变异证据；行为不变，持久字节不变。代价：宿主多了约 140 行类型化边界（`ChatExecutionPhaseLaw`），三个调用文件变短。
- 持久数据（§10）：日志事实 schema 与字节不变，折叠结果不变；无迁移。回退：revert 本卡主仓提交即可，库 0.44.0 留在 main 不影响旧 master。

## 5. 余项（子闭环 2 落地时的记录，已由 §6–§8 结清）

1. 子闭环 1：关联先于首 prompt（`Execution/Session/Association*`、Attachment）。
2. 子闭环 3：exact 结算与复用（ReuseScope、Recovery 的结算判定）。
3. 子闭环 4：删除与重启的真实 Host 链（SessionRecoveryHost、ChatExecution/Recovery*）。
4. 整卡 DONE 需 CUT-11 DONE 后在真实 Host 链复核。

## 6. 子闭环 1 与 4：关联律、重启恢复决议（eng-cut12b-goose 实现，eng-cut12c-goose 复核落地）

- 库：wanxiangli `c705bd2` 新模块 `src/association`（`SessionAssociationLaw.link`，npm 入口 `wanxiangli/association`）；`714c0ae` 给 `wanxiangli/chat-execution` 加 `recover()`；`6483534` 发布 **0.46.0**。
- 主仓：`Execution/Session/Association.fs` 的 `SessionAssociationProjection.linkSatellite` 只做键查找（两侧条目、main 已有的同类卫星）与写入，判定次序（自链 → 递归 → 第二个卫星 → 被别的 Work 占用 → 种类冲突 → 接受、父取提议值否则沿用）交库；删 `satelliteKindConflict`、`linkedSatellite`、`linkValidated`。`ChatExecution/Recovery.fs` 的 `decide` 只算精确键与证据相等，相位 × provider 观察 × 资源观察 × 持久承诺 × 失败策略的下一步交库 `recover`；删 `stale`、`terminalDecision`、`acceptedDecision` 等手写 match。
- 差分：[association-differential.mjs](../evidence/CUT-12/association-differential.mjs) 4,800 次 0 处不同；[recovery-differential.mjs](../evidence/CUT-12/recovery-differential.mjs) 19,200 次 0 处不同。变异 [mutants-12b.py](../evidence/CUT-12/mutants-12b.py) 8/8 被抓住。

## 7. 子闭环 3：exact 租约复用与新子回收

- 库：wanxiangli `1ae069c` 新模块 `src/attachment-lease`（`AttachmentLeaseLaw.resolve` / `abortOnFailure`，npm 入口 `wanxiangli/attachment-lease`，AGENTS.md 写 AXIOM/LEMMA/SPEC），`0bf6295` 发布 **0.50.0**；库测试 5/5，build-001 独立编译 1/1。
- 主仓：`Execution/Session/Attachment/AttachmentLeaseCore.fs` 的 `resolve` 只列子会话、按裁决新建或复用并映射原错误文本；"无持久关联一律新建、不收养长得一样的子会话；hint 精确命中（agent 与 title 都相等）才复用；命中但不精确为 Conflicting；hint 消失为 Replacement 且先关旧关联；重复 id 为 Ambiguous"与"关联失败时只弃新建/替换的子会话"交库；删 `exact`、`resolveHint` 与 `abortFresh`/`closeReplaced` 里的手写 match。`ReuseScope` 仍是 SessionId 的恒等包装（G2），无规则可抽。
- 差分 [lease-differential.mjs](../evidence/CUT-12/lease-differential.mjs)：restore hint × ≤3 个子会话（id/agent/title 全组合）× 五个操作成败全组合 × 4 种 AttachmentKind，比较返回值与操作序列，**90,112 次 0 处不同**。变异 [mutants-12c.py](../evidence/CUT-12/mutants-12c.py) 6/6 被抓住。

## 8. 范围收窄与余项

- `SharedTerminalBus` 是跨插件实例的终态扇出物理资源，`SharedState.SessionParents/SessionDirectories` 是 HOST-012 的物理父/目录登记；逻辑关联与 owner 只由 `SessionAssociationProjection` 判（经库）。二者属宿主适配层，归 CUT-34/35，不是本卡的第二份关联真相。
- 删除顺序（先提交后释放）由 `SessionRecoveryHost` 与 CUT-14 的结算决议执行；本卡交出的是决议，物理执行与确认仍在宿主。
- 持久数据（§10）：日志事实 schema 与字节不变，无迁移。回退：逐个 revert 主仓提交即可，库版本留在 main 不影响旧 master。

## 9. 验收复核（eng-cut12c-goose）

- base = master `b10da13c9` 原样构建；candidate = base + 本卡四个子闭环 + CUT-13 子闭环 1，gitlink `ea20725`（0.52.0）；两次 `node scripts/build.mjs` 均 exit 0。上面四组差分与 14 个变异都在这一对上重跑。
- fantomas `--check` 改动文件通过；`node scripts/checks/architecture.mjs` OK。
- 套件见 [evidence/CUT-12](../evidence/CUT-12/README.md)。
