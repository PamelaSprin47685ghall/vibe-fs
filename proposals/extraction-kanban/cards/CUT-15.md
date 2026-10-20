# CUT-15：前缀世代与候选提交

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-15) / [证据](../evidence/CUT-15/README.md)。

## 1. 认领与交接

- Owner：`eng-cut15b-goose`（2026-10-20 接手 `eng-cut15-goose`，后者两次中断；其工作树 `wt/CUT-15-main` 的未提交改动与 `wt/CUT-15-wxl` 的库提交全部接收，未丢弃）。Reviewer：无独立人工 reviewer（仓主授权自治集成，见 PROTOCOL）。
- 库：`wanxiangli` 实现 `7aac7b8`，发布 `770eea6`（0.10.0，prefix）。已在 origin/main 上（后续 0.11–0.18 均包含它）。
- 主仓：gitlink 不变（沿用 master 当时的 `71958aa` / 0.18.0，已含 prefix）；`package-lock.json` 不变。

## 2. A. 范围重判

旧职责分散在主仓四处：`Context/Prefix/Epoch`（世代推进、两类证据去重、吸收策略）、`Context/Prefix/PhaseWindow`（view 检查点去重）、`Host/CompactionPolicy.nextReanchor`（选哪一次压缩）、`ProviderProjection.isAppendOnlyPrefix`（同代追加律）。CARDS.md 列出的 `ProbeSelection` 与"探针成功/压缩窗口"推进源已被现行需求废除（prefix-stability-003），主线上不存在，不复活。

## 3. B. 契约（wanxiangli/prefix）

`Prefix` 无状态 owner：`empty / normalize / isReanchored / propose / proposeCompaction / commit / appendCheckpoint / classify`。世代只由两类已提交证据（宿主压缩伪 run、Plan 任期 incumbencyId）推进且恰好加一；候选不改状态；`StaleEpoch` 与 `AlreadyReanchored` 被吸收，`NonSequentialEpoch` 报错；`classify` 比较 provider/model/variant/工具集/system/逐条消息规范字节并列出全部违例。消息字节由调用方渲染；不读宿主、不调 Trace、不自证缓存命中。详见库 `src/prefix/AGENTS.md`。

## 4. C. 反例（真实的红）

接手时核对 master 发现一个真实缺陷：仓主 cutover `6eef89d1e` 之后，持久折叠 `ContextReanchored` / `TenureReanchored` 只清检查点、退休辅助呈现，**从不推进 `PrefixEpoch`**（`ProjectionUpdate.tryUpdatePrefix` 存在但无人调用）。后果：

- 世代永远停在 0，已处理的压缩 run 不入账 → `HostCompactionGate` 每次核对都对同一次压缩再写一行 0→1；
- 每条重写行都把会话之后重建的 view 检查点清空、把 guidance 可见性再退休一次；
- Plan 任期同理：`PluginTransforms` 的 `IsFreshHandover` 读的 incumbency 集合永远为空。

`requirements/prefix-stability/tests/006.test.mjs` 新增 5 个 `CUT_15_*` 用例，在 base（master `6859577a7` 构建）上 **5/5 红**，在 candidate 上 5/5 绿（证据 §3）。

## 5. D–H 结果摘要

- D：库侧 19 项 prefix 测试绿（`node --test src/prefix/tests/*.test.mjs`，gitlink `71958aa`）。
- E：沿用 submodule + `file:wanxiangli`；本卡不改 gitlink 与 lockfile。
- F：折叠经 `PrefixEpochProjection.applyReanchor / applyTenureReanchor`（→ 库 `commit`）推进世代，只有被接受的推进才清检查点与退休可见性；`HostCompactionGate` 用库 `proposeCompaction` 的候选写行；`PhaseWindow.appendCheckpoint`、`isAppendOnlyPrefix` 改为库投影。
- G：旧 owner 退出：`Epoch.fs` 内手写的世代/证据判定、`PhaseWindow` 去重、`HostCompactionPolicy.nextReanchor`（连同 `.fsi`）、`isAppendOnlyPrefix` 的逐字段比较删除。`CompactionPolicySurface.nextReanchor` 仅作为既有测试面保留，内部改问库。
- H：见证据：相关 6 个需求域 base/candidate 逐用例一致（candidate 多出的 5 项为新反例，全部通过）；新旧双算 65000 次 0 差异；`scripts/check.mjs` 发现集合与 base 相同。

## 6. 已知不足

- prefix-stability-001 第一个用例（reused delegation 端到端）在本机 base 与 candidate 上同样以 "Promise resolution is still pending" 取消并连带取消同文件其余用例；跳过该用例后其余 7 项在两侧都通过。端到端委派路径因此未由本卡证明。
- host-boundary-023/026 两项在两侧同样失败（`--ignore-scripts` 安装下 opencode-ai postinstall 未运行），环境性，非本卡。
- 增量（focused）构建在本卡第二次构建时误把 `Context/Companion/Transform.fs` 纳入影响集而失败；`--clean` 全量构建通过。这是构建工具的 focused-impact 问题，未在本卡修复。
- 全量 `scripts/verify.mjs` 与集成 run.mjs 未跑（共享机资源）。
- 既有日志：故障期间写下的重复 0→1 行在重放时被当作过期行吸收，首个压缩行生效；若真有第二次压缩被错写成 0→1，下一次核对会以正确世代补写（自愈，不改写原件）。
