# obligation-ledger — README

本包规定待办义务的消极边界：宿主原生 `todowrite` 退役约束与「不维护第二份待办账本」。规则正文见 `WHAT.md`；本 README 记录各条款的实际证明边界，不新增规则。上下文重置唯一由 `resetScope` 管理。

## 001 / 002 — 原生 todowrite 退役与角色编程面待办

- `tests/001.test.mjs`：验证 `todowrite` 已从 `StaticTools.knownToolNames`、角色权限矩阵、`InvestigationEstimateContract.classifyTool`（返回 `Unreviewed`）和 `resources/ablation/tool-map.json` 中移除；验证原生 `todowrite` 不被放行。
- `tests/002.test.mjs`：验证 `todowrite-for-*` 角色编程面执行边界。

## 003 / 004 / 005 — 不维护第二份待办投影与无副作用边界

### 已证明

- **纯 fold 路径**（`tests/004.test.mjs` 第一个用例）：companion、opening、trace parts、terminal、blog、prefix epoch、enforcement 在非空前态下逐值保留；同 call 重放幂等；跨 session 隔离；退役 API 拒绝（007）。
- **真实链路径**（`tests/004.test.mjs` integration 用例）：前态全部经真实 owner 入口建立；退役的 checkpoint 事实不影响其他投影。
- `Persistence/Journal/Surface.fs` 的 snapshot 暴露 `sessions`/`viewCheckpoints`/`sessionProjections` 字段。
- `Context/Companion/FoldSurface.fs` 的 `sessionToJs` 不再硬编码 null，转发全部 9 个 slice。
- 转发粒度：PromptAuthority profile 转发 session/logicalRun/authorityRoot/authorityKind（不含 identitySeed 完整结构，其保留由 interaction-authority 域测试承载）；ProviderFailures、Guidelines、DelegatedToolEstimate、Handles 等转发计数与 key 判别字段。

### 仍待裁决（前态无共享 journal 公开入口）

以下 slice 的非空前态在共享 journal 上没有公开入口（各 owner surface 的 journal handle 类型不配 `runtime.journal`，或 fact 族无 JS 解码入口），按裁决边界 2 列出，由 Manager 决定是否为该 owner 增加入口：

- ProviderFailures（TemporalSurface 的 journal handle 类型私有，纯 fold 无 checkpoint 路径）
- Guidelines / SessionStartedAt / DelegatedToolEstimate

这些 slice 目前以空形状参与深比较（「checkpoint 后空 slice 仍空」已被断言），非空前态的变异检测待入口裁决后补入。

## 局部运行

`node --test requirements/obligation-ledger/tests/*.test.mjs`（构建须 `node scripts/build.mjs`；红绿由 DevOps 执行）。

