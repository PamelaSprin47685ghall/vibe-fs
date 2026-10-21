# CUT-28　Fission 多 lane 执行

返回 [BOARD](../BOARD.md) · [CARDS §CUT-28](../CARDS.md#cut-28)

## 1. 当前状态与前置核对

- **当前列状态**：`DONE`（eng-cut28d-goose 接手 eng-cut28c-goose ← eng-cut28b-goose ← eng-cut12d-goose，2026-10-21）。三刀都已落地，见 §2–§4；余项去向见 §6。
- **前置**：CUT-03、CUT-10、CUT-12、CUT-13 均 DONE。
- **对象**：一位 participant 的 lane 集合与整组准入/拒绝。组表、session/blob/run 值的相等、typed 事实构造与写入、lane 的创建/启动/中止都留在主仓；库只拿“有无已有值、是否同值、lane 是否越界、lane 身份是否吻合、进度（哪些 lane 交了工作、每个 Fission 前完成送到了哪些 lane）”，给出 set/keep/reject 与拓扑答案。
- **与 CUT-29 的边界**：replica/Strength 是不同参与者的并行（CUT-29 `warm-start`）；Fission 是同一参与者的 lane，库模块不共享概念、不互相 import。

## 2. 第一刀：组事实裁决与收束前提 → `wanxiangli/fission-group` 0.59.0

- 库：wanxiangli `d853e39` 新模块 `src/fission-group`（`FissionGroupLaw.judge` / `convergenceReady`，npm 入口 `wanxiangli/fission-group`），AGENTS.md 写 AXIOM 1–3 / LEMMA 1–3，`index.d.ts` 写 plain 契约；`7c0d5a9` 发布 **0.59.0**。
- 主仓：`Execution/Fission/Projection.fs` 的 `FissionProjection.fold` 九种事实（admitted、lane 物化、完成捕获/送达、外部亲和、接管 claim/start、收束、失败）只比较自己持有的值并按裁决写入；哪个事实 set、哪个是幂等重放、哪个被拒以及拒绝次序都归库。
- 差分 [fission-differential.mjs](../evidence/CUT-28/fission-differential.mjs)：base 与 candidate 两个真实 `FissionProjection.fold` 跑同一组 20,000 条种子随机事实序列（2 组 × 2 owner × lane 数 1..3，含重放、冲突值、错 lane/session、未知组、迟到终局），每步比较整个 Result：480,000 步 0 差异（admitted Ok 41,627、lane Ok 31,967、converged/started/claimed 的 ConvergenceIncomplete 等全部拒绝种类都覆盖到）。
- 变异 [mutants.py](../evidence/CUT-28/mutants.py)：对 candidate 实际加载的库产物做 12 个语义变异（单 lane 准入、owner 重复、lane 工作覆盖、sibling 替换、越界、缺工作收束、缺广播收束、接管覆盖、别的 lane 收束、第二终局、收束后失败、外来完成）：12/12 CAUGHT（差异 15–15,152 步不等，每个都是比较出的差异，不是候选抛异常）。

## 3. 第二刀：lane 请求、完成路由、环次序、Fission/Turn 结算 → 0.60.0

- 库：wanxiangli `2f16392` 给 `fission-group` 加 `parsePrompts` / `routeCompletion` / `mergeOrder` / `finalLane` / `successor` / `settle`（AXIOM 4、LEMMA 4–5），`0718345` 发布 **0.60.0**。
- 主仓：`Execution/Fission/Model.fs` 只留 typed 词汇与 delivery/bundle 容器；`FissionPrompt.parse`、`FissionCompletionRouting.targets`、`FissionRing.*`、`FissionSettlement.*`、`FissionConvergence.ready` 只做与库的翻译。
- 有意的行为修复：活体收束检查原先接受“为别的 lane 数建的 delivery”来见证本组广播；现在与持久投影读同一个 `convergenceReady`，并要求 delivery 的 lane 数等于组的 lane 数，越界 lane 不算送达。
- 差分 [topology-differential.mjs](../evidence/CUT-28/topology-differential.mjs)：小域穷举（≤4 个 prompt × 五种文本、lane 数 0..4 的路由、-1..6 的环次序/终局 lane、0..5 × 每个关闭子集的后继、两种结算 × 每种观察、0..4 lane × 每个 bundle 键集 × 每个 delivery 集）：8,829 项 0 差异；另把“delivery lane 数 ≠ 组 lane 数”的 6,636 个观察单列，44 个与 base 不同，全是 §3 的有意修复（base 接受了别的 lane 数的 delivery）。
- 变异 [mutants-topology.py](../evidence/CUT-28/mutants-topology.py)：11 个语义变异：11/11 CAUGHT。
- 首版 `FissionCompletionRouting.targets` 漏了 `laneCount: int` 注解，Fable 推成泛型、与 `.fsi` 不符，构建失败（eng-cut12d 的落地循环 `BUILD_FAIL`，未推送）；本卡补注解后随第三刀一起落地。

## 4. 第三刀：宿主不再自己推导收束前提

- `OpenCode/Host.fs` 的 `allLaneWorkPresent && allSharedCompletionsDelivered` 与 `OpenCode/Tool.fs` 的 `readyExceptForRetirement` 各自从组的 map 重新推了一遍收束前提（第三、第四份）。现在两处都问折叠后组的 `FissionProjection.convergenceReady`，即库的 `convergenceReady`；收束前提只剩一份。
- 等价依据：`LaneWork` 与 `LaneProviderRuns` 在同一个 `laneMaterialized` 写入里一起加（Projection.fs），`completionDelivered` 只接已捕获的完成（LEMMA 2），所以旧检查里额外的 provider run 计数与“已捕获”条件在折叠可达的组上恒真。
- 差分 [convergence-differential.mjs](../evidence/CUT-28/convergence-differential.mjs)：同一事实生成器跑 candidate fold，对每一步每个可达组比较新 `convergenceReady` 与旧 Host、旧 Tool 两个手写谓词：521,062 次比较（其中 17,564 次 ready）0 差异。

### 接手时发现并修掉的缺陷（未推送前）

- eng-cut28b 的落地循环第一次跑差分时，`fission-differential` 与 `convergence-differential` 在候选上直接抛 `wanxiangli/fission-group: malformed progress.workedLanes`，12 个变异也都只是“candidate threw”，即这些证据当时其实没有成立。
- 原因：`FissionProjection.progressOf` 写的是 `group.LaneWork |> Map.toArray |> Array.map fst`，Fable 把 `int[]` 编成 `Int32Array`，而库的契约（`index.d.ts` `readonly number[]`，实现用 `Array.isArray`）只收普通数组。也就是说，一旦某个组走到收束前提检查，生产 fold 就会抛异常。定向测试套件没有抓到它，是差分抓到的。
- 修复：改成 `Map.toList |> List.map fst |> List.toArray`（普通 JS 数组），以 fixup 并进第一刀，三刀都不含坏版本。`Model.fs` 的 `List.toArray`/`Set.toArray` 已核对编译产物，本来就是普通数组。修复后上面的差分与变异数字才是真的。

## 5. 验收（§9）

- candidate = master `c581ce038`（第一刀）、`5354e53e0`（第二刀）、`bf5a8ea91`（第三刀）。gitlink 指向 wanxiangli **0.61.0** `71f80c4`（master 落地前已由 CUT-23 移到 0.61.0，它包含 fission-group 0.60.0；eng-cut28c 的落地循环因三刀仍指 0.59.0/0.60.0 而以 GITLINK_OLDER 停下，本卡 rebase 后三刀都改指 `71f80c4`、lock 0.61.0，gitlink 没有倒退），`node scripts/build.mjs` exit 0，`scripts/checks/architecture.mjs` OK。
- 定向套件（与 CUT-12/13 同一份 126 个测试文件，含 intra-participant-parallelism 17 个）：585 个测试，425 pass；失败子测试名 86 个，全部在 base 失败名单（master `ac046ae88` 上同一份文件的 129 个）之内，新增失败 0；intra-participant-parallelism 17 个文件 0 失败。base 名单早于本次 rebase 所在 master，只用作“是否有新失败名”的对照，不作 pass 数对比。上面 §2–§4 的差分与变异数字是在 rebase 后的候选（构建产物链接 0.61.0）上重跑的结果，与 0.60.0 上的一致。
- 净改进：Fission 的组折叠律、拓扑与结算原先散在 `Projection.fs` 的手写 match、`Model.fs` 的手写函数与两个宿主文件的重复检查里；现在只有 `wanxiangli/fission-group` 一个 owner，带编号库测试（7/7）、差分与变异证据；持久事实 schema 与折叠结果不变，无迁移（§10）。回退：revert 主仓提交，gitlink 回到上一版。

## 6. 余项去向（不是遗漏）

1. `Execution/Fission/Admission.fs`：整组准入的效果编排（查物理父、拿 owner view、逐个建 lane、提交 `LanesCreated`、逐个启动、静默打断 owner，任一步失败就中止已建 lane 并释放）与进程内单飞集合。它是对宿主端口的物理效果排序，规则部分（少于 2 个 lane、空 lane、owner 已有活跃组）已由库判；RETAINED 在主仓，随 CUT-35 复核。
2. `Execution/Fission/Runtime.fs`：lane 绑定、静默打断、handle 亲和、送达/接管 latch 等进程内资源索引；按 crash-reconciliation 属可丢弃加速器，持久真相在 `FissionProjection`。RETAINED。
3. `Execution/Fission/Surface.fs`、`OpenCode/Host.fs`、`OpenCode/Tool.fs`：JS 边界编解码与 OpenCode 宿主适配（建子会话、送 prompt、汇总视图、写事实）。归 CUT-34 宿主适配层，不入库。
4. Trace 的 keyed 合流按 `FissionRing.mergeOrder` 读，已不按到达顺序拼；Trace 本身归 CUT-03。
