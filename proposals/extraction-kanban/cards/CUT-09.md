# CUT-09　来源、逻辑根与关闭权威

返回 [BOARD](../BOARD.md) · [CARDS](../CARDS.md#cut-09)

## 1. 当前状态与前置核对

- **当前列状态**：`DONE`（eng-cut09c-goose 接手 eng-cut09b-goose、eng-cut08c-goose，2026-10-21），按 README §6 末段收窄范围关闭。完成的是子范围①：逻辑根注册、精确关闭（显式关闭、子工作关闭、持久重放关闭）与来源解析优先级，这三份裁决现在只有 `wanxiangli/authority` 一个 owner。未完成部分拆成 CUT-09-a、CUT-09-b（§6），不算在本卡完成范围内。
- **前置**：CUT-07 已 DONE。
- **边界**：投影记录、事实编解码、Ledger/Fold 的 durable 写入口留在主仓；库无状态、不读日志。

## 2. D-01 裁定：未知来源失败关闭

库里早期草稿写的是"缺来源标记时回退为 human、只记诊断"。这与现行 interaction-authority [005][007][009] 冲突：回退为 human，等于让一条未经证明的消息拿到根独占权。**裁定：保持现行行为，无法证明的来源一律 `UnknownOrigin`；纯计算从不推断新的 `HumanRoot`**，HumanRoot 只能由入口边界凭显式 participant 授予。理由写在 wanxiangli `src/authority/AGENTS.md` LEMMA 1、2。

## 3. 库侧

- wanxiangli `994e683`：新增 `src/authority`（`RunLaw.fs`、`Authority.fs`、`index.d.ts`、`tests/001.test.mjs`）。`28d3dcc`：JS 边界取字段改用有类型的访问器，隔离构建因此没有警告；authority 加入 build-001 的隔离构建列表。`351e3c5` 发布 **0.28.0**。
- `register(active, requested)`：`admit` / `replay`（精确同一根，或同会话、同根消息、同根种类、同身份种子的持久重放）/ `conflict`。
- `close(active, last, logicalRunId, rootId)`：`close` / `already-closed` / `mismatch` / `no-authority`；`closeChildWork` 另要求活跃根是非 Manager 的 AgentOwnerRoot，否则返回 `not-child-work`。
- `resolveOrigin`：已接受的 continuation → 已 claim 的 PromptKey 自身来源 → Host 内部压缩/合成 → 带 PromptKey 且活跃根为 AgentOwnerRoot → `UnknownOrigin`。
- 畸形输入直接抛错，不猜测。身份种子以宿主给出的规范键逐字比较，库不解析 participant。
- 库测试：authority 6/6；隔离构建 build-001 中 authority 通过且无警告。整套库测试（rebase 到 0.26.0 之后）668 项里 667 pass、1 fail，失败的是 `SPEC[build-001] sandbox …`：sandbox 隔离构建会输出 Fable 警告，这个问题在 origin/main 上同样存在，与本卡无关（CUT-25 的模块）。发布前，又在 0.27.0（CUT-17）之上 rebase 并做了针对性复验：authority、projection 加 exports/package/repository/contracts 共 23/23，两个模块的 build-001 都通过。
- 打包验收：`npm pack` 得到 0.28.0，tarball 内含 `dist/authority/Authority.js`；在干净目录安装后 `import('wanxiangli/authority')` 可用，见证据 §5。

## 4. 主仓接线

- `Interaction/Authority/Run.fs`：新增内部模块 `AuthorityLaw`，是库的类型化投影，只用 Import/Emit。身份种子以 `PromptIdentitySeed.toInput` 的规范 JSON 作为比较键。
  - `resolveAuthorityProfile`：删除 `isExactDurableRootReplay` 和手写匹配，改由库 `register` 裁决；库给出与投影不一致的结论时直接 failwith。
  - `closeAuthority`：删除手写的 `sameRun` 匹配链，由库 `close` 裁决；宿主只执行状态重置（`settleClosure`）。
  - `closeCompletedAgentOwnerChildWork`：删除角色/种类守卫，由库 `closeChildWork` 裁决。
  - `resolveKnownOrigin`：删除 `resolveUnacceptedOrigin` 优先级链，由库 `resolveOrigin` 裁决。
  - 新增 `replayLogicalRunClosed`（同时写入 `.fsi`）：持久重放 `AuthorityLogicalRunClosed` 时，裁决与 `closeAuthority` 读同一条库规则。
- `Interaction/Authority/Fold.fs`：`foldAuthorityLogicalRunClosed` 删掉自己的"活跃运行是否精确命名"匹配，改调 `replayLogicalRunClosed`。改动前，这里是 Ledger/Fold 侧对关闭的第二份独立裁决。
- gitlink `wanxiangli` 从 master 当前的 `ece7176`（0.26.0，CUT-27 已移入）移到 `351e3c5`（0.28.0），向前移动；0.27.0（projection）随之进入，主仓尚未消费它。lockfile 只改了版本行。（最初在 `13944a5db`/`a699b95` 上做，rebase 到 `5232b0eba` 后 lockfile 冲突取 0.28.0。）

### 4.1 旧符号去向

| 旧实现 | 去向 |
|---|---|
| `PromptAuthorityRun.isExactDurableRootReplay` 与 `resolveAuthorityProfile` 的匹配 | 删除，由库 `register` 裁决 |
| `closeAuthority` 内的 `sameRun` 与四分支匹配 | 删除，由库 `close` 裁决；宿主只保留状态重置 |
| `closeCompletedAgentOwnerChildWork` 的 AgentOwnerRoot/非 Manager 守卫 | 删除，由库 `closeChildWork` 裁决 |
| `resolveUnacceptedOrigin` 与 `resolveKnownOrigin` 的优先级链 | 删除，由库 `resolveOrigin` 裁决 |
| `Fold.foldAuthorityLogicalRunClosed` 的独立关闭匹配 | 删除，改走 `replayLogicalRunClosed`，与上面读同一条规则 |
| 保留在宿主 | 投影记录与重置、事实编解码、Ledger/Fold 写入口、`PromptOrigin` 等类型、`Ledger.closeCompletedHumanRootManager` 的触发条件（工作完成时关 HumanRoot Manager，属于业务消费者；关闭本身仍走库） |

调用者（Ledger、Fold、Dispatcher、DelegationProjectionBridge、RuntimeSurface）都经 `PromptAuthorityRun`，不再有第二份可写裁决，也没有同步器。

## 5. 验收（§9）

证据见 [evidence/CUT-09](../evidence/CUT-09/README.md)。

- rebase 复验（eng-cut09c-goose）：rebase 到 master `5232b0eba` 后重新 `npm ci --ignore-scripts` 与 `node scripts/build.mjs`（exit 0，725 模块链接），差分对同一 base 重跑仍是 24,684 次 0 处不同（`13944a5db`→`5232b0eba` 之间 Interaction/Authority、Identity、Persona/Identity 源码未变，base 产物仍有效）；10 个相关套件计数与失败/取消用例名集合和 base 完全一致。
- 差分：base 为 master `13944a5db`，candidate 为本卡接线加 gitlink `351e3c5`。双算 24,684 次，0 处不同，覆盖注册、三类关闭、来源解析以及 Fold 重放。base 上每个函数族的成功、冲突、各类错误分支都出现过。
- 变异：对 candidate 的库产物做 5 个语义变异（重放要求逻辑运行号相等、种子比较恒真、去掉 Host 压缩优先级、关闭不比根消息、already-closed 改成 no-authority），差分分别报出 160、2240、768、364、708 处不同，全部被抓住。
- 10 个相关套件 base 与 candidate 的计数相同，失败/取消用例名集合也相同：delegation 23 fail / 33 cancel、managed-session-lifecycle 1 fail / 69 cancel、dispatch-protocol 1 cancel、effect-accounting 5 cancel，都是 base 上已有的。
- `check.mjs`：base 与 candidate 都 exit 1（既有问题）。规范化后发现集合相同；候选中途出现过 1 条 `match-pyramid`，已改写消除后复核。shard-edge-audit 两边都是 251。
- fantomas `--check` 对本卡 3 个文件通过。
- 净改进：根注册与关闭原先在 `Run.fs` 和 `Fold.fs` 各有一份裁决，现在只剩库一份，并有独立测试与差分/变异证据；行为不变，权限没有扩大。代价：宿主删掉约 70 行判定逻辑，但新增约 130 行类型化投影（plain data 编解码与一致性守卫），主仓 diff 净增 147 行；这是把规则移到可独立验证的库所付出的边界成本，如实记录。
- 回退：revert 本卡主仓提交即可。gitlink 回退到 `ece7176` 时，库 0.28.0 仍可留在库 main，不影响旧 master。

## 6. 收窄范围与移交（DONE 的边界）

| 余项 | 为什么不在本卡 | 去向 |
|---|---|---|
| `OpenCode/Host/ChatAdmission/Intent.fs` 的入口准入决策（显式 participant 授予 HumanRoot、`UnknownOriginWhileActive`、claim 来源可准入性、缺物理 id 的拒绝） | 它是 D-01 指定的入口边界，状态机和宿主 Decision/Rejection 类型深度耦合；纯裁决可以抽，但需要单独的契约和差分 | **CUT-09-a**（2026-10-21 DONE，见 [CUT-09-a](CUT-09-a.md)） |
| interaction-authority-018 的五类 ExpectedClosureKind 与 source witness 精确匹配 | 宿主目前只有 retirement witness 字符串，缺少可迁移的事实；要先在宿主补齐事实再迁入库 | **CUT-09-b**（新登记，前置 CUT-11 对 claim 账的处置） |
| 投影重置（清 run-scoped claims、continuation 映射、序列号、提问） | 与 claim 账耦合 | 随 CUT-11 |
| 崩溃恢复（`ChildWorkRecovery`、`LoadRecoverySurface`） | 只读投影，不做裁决 | 保留 |
