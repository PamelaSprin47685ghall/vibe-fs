# CUT-09-a　入口准入决策（ChatAdmission/Intent）的纯裁决

返回 [BOARD](../BOARD.md) · 母卡 [CUT-09 §6](CUT-09.md#6-收窄范围与移交done-的边界)

## 1. 当前状态与前置核对

- **当前列状态**：`DONE`（eng-cut09e-goose 接手 eng-cut09d-goose、eng-cut09c-goose，2026-10-21）。
- **前置**：CUT-09 已 DONE（`6774662a2`）。
- **边界**：participant 解析（`ParticipantIdentity.resolveAtRoot`）、身份种子、投影查找与宿主 `Decision`/`Rejection` 的类型化载荷留在主仓；库只拿 plain 事实，按固定次序给出裁决标签。D-01 不变：HumanRoot 只能在入口边界凭"根上解析成功且带角色的显式 participant、且没有活跃运行"授予。

## 2. 库侧

- wanxiangli `da86791`：`src/authority/AdmissionLaw.fs` 新增 `admit`，`Authority.fs` 增加 plain JS 边界（`admit(input)` 返回 `{kind, reason?}`），`index.d.ts` 与 `AGENTS.md` 同步；`tests/002.test.mjs` 用按 interaction-authority [005][007][009]、dispatch-protocol-006 独立写的 oracle 对穷举输入比对。`64ae12a` 发布 **0.36.0**（已在 origin/main）。
- 固定次序：畸形身份载体 → 已接受 HumanRoot 物理消息的重放（含参与者冲突）→ 会话 / durable 快照 / 物理 id 缺失 → 已接受 continuation 或已落地 → 精确 pending claim（会话、claim 自身来源可准入性、显式 participant 一致）→ Host 内部 → 未接受消息（PromptKey 未 claim、显式 participant 授根或 `UnknownOriginWhileActive`、省略 participant 时按活跃档案续接）。
- 库测试（eng-cut09c-goose 发布前，rebase 到 0.35.0 之上）：authority + exports/package/repository/contracts 18/18；build-001 authority 隔离构建通过；对库产物做 4 个语义变异，库测试全部报红。

## 3. 主仓接线

- `OpenCode/Host/ChatAdmission/Intent.fs`：新增私有模块 `AdmissionLaw`（Import/Emit 的类型化投影）、`observe`（一次性查出 active、accepted continuation、landing、pending claim、根上解析的显式 participant）、`admissionInput`（plain 事实）、`decide`（裁决标签 → 宿主 typed `Decision`，缺证据或未知标签直接 failwith，不猜）。`.fsi` 不变，调用者不变。
- 删除的手写裁决：`rejectMissingPhysical`、`claimOriginAdmissible`、`pendingPrompt`、`externalRoot`、`KnownEvidence`/`knownEvidence`、`resolveUnaccepted`、`resolveWithProjection`、`resolveValid`、`acceptedHumanRootReplay`，以及 `resolve` 里的载体/重放次序。
- gitlink `wanxiangli` 从 master 的 `d2ce694`（0.35.0）前移到 `64ae12a`（0.36.0）；lockfile 只改 wanxiangli 版本行。（落地时 master 已被 CUT-19-b 前移到 `3d6a83f`（0.38.0，包含 0.36.0），故本卡最终提交不再改 gitlink 与 lockfile；rebase 后在 0.38.0 上重建并复跑差分与套件，结果相同。）

| 旧实现 | 去向 |
|---|---|
| 上列 9 个私有函数与 `resolve` 的次序 | 删除，由库 `admit` 裁决 |
| `tryRootIdentity`、身份种子 → selected agent、`hostInternal` 证据、`describeRejection` | 保留在宿主（participant 解析与类型化载荷） |

## 4. 验收（§9）

证据见 [evidence/CUT-09-a](../evidence/CUT-09-a/README.md)。

- 构建：base（master `c109341d7`）与 candidate 都 `node scripts/build.mjs` exit 0（725 模块链接）。
- 差分（[differential.mjs](../evidence/CUT-09-a/differential.mjs)）：`ChatAdmissionIntent.resolve` 在 base 与 candidate 上对同一输入宇宙双算 **1,290,240 次，0 处不同**。覆盖 `Decision` 的全部 6 种可由 resolve 产生的分支，以及 `Rejection` 全部 11 种。
- 变异（[mutants.py](../evidence/CUT-09-a/mutants.py)）：对 candidate 使用的库产物做 6 个语义变异（有活跃运行时仍授 HumanRoot、claim 的 HumanRoot 来源可准入、重放不看 Host 内部、缺物理 id 只在带载体时拒绝、不查 claim 会话、省略 participant 视为非托管），差分分别报出 37,848 / 576 / 8,064 / 14,112 / 14,400 / 12,616 处不同，全部被抓住。
- 套件：interaction-authority、participant-identity、dispatch-protocol、managed-chat-execution、crash-reconciliation、host-boundary 在 base 与 candidate 上计数相同，失败/取消用例名集合逐一比较相同（dispatch-protocol 1 cancel、host-boundary 2 fail 为既有问题）。
- fantomas `--check` Intent.fs 通过。`scripts/check.mjs` 未跑（机器负载；CUT-09 记录其在 base 上已 exit 1）。
- 净改进：入口准入的次序规则原先只以宿主私有模式匹配存在，现在只有库一个 owner，并有独立 oracle 测试、差分与变异证据；行为不变，权限没有扩大。代价如实记录：Intent.fs 从 363 行变为 392 行（+29），因为 plain 事实编解码与"缺证据即 failwith"的守卫是边界成本。
- 回退：revert 本卡主仓提交即可；库 0.36.0 留在 main 不影响旧 master。

## 5. 余项

无。claim 账与投影重置随 CUT-11；018 关闭见证见 CUT-09-b。
