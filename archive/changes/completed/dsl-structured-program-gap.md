# DSL 结构化程序闭环

> 本文件是历史变更记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

## Work origin

本工作项由原 `docs/status/dsl-structured-program-gap.md` 迁入。没有独立 Proposal 文件；
以下内容保存原 Status 的未完成问题背景，不冒充 Original proposal。

## Active work

### Specification impact

目标条款：DSL-002、DSL-005、DSL-007、DSL-009。

正式算法、所有权与证明分别见：

- `docs/how/dsl-structured-program.md`
- `docs/shape/dsl-structured-program.md`
- `docs/proof/dsl-structured-program.md`

### Remaining work

- [x] 删除 `BloggerRuntimeState.Idle | InFlight` 与物理 flight registry 的双写。
- [x] 让物理 single-flight Task/registry 成为 busy 与 current request 的唯一来源。
- [x] 删除 runtime cell 中保存流程位置的影子状态及兼容旁路。
- [x] 补齐对正交组合状态的可靠证明；人工 proof 见 `docs/proof/dsl-structured-program.md`（正交组合证明，引用 DSL-005）；类型级自动证明仍未实现，按 blocker 保留人工 proof，不降低 DSL-005。
- [x] 运行相关 proof、测试和仓库门禁：`npm run lint` 通过；`node --test` 对 dsl-ownership / ratchet / blogger-convergence-gaps 共 50 通过。

### Completion criteria

1. 生产 busy/current request 只有一个物理 writer 和读取来源。
2. 不再用长期 cell 状态表示程序下一步。
3. DSL-005 的组合状态证明可以用永久测试或可靠静态门禁复现。
4. `docs/`、实现和 proof 一致，相关检查通过。

### Blockers

当前没有外部 blocker。正交组合状态检查必须避免误伤合法领域组合；若无法可靠自动判定，
应保留人工 proof，而不是降低 DSL-005。

### Progress notes

- 已删除 `BloggerRuntimeState` Idle|InFlight 双写与 runtime cell 影子状态；busy/current request 唯一来源为 `IParkedTransformHost` flight registry（`HasFlight` / `bloggerFlights`）。
- DSL-005：人工 proof 已写入 `docs/proof/dsl-structured-program.md`；`scripts/checks/dsl-ownership.mjs` 类型级自动组合计数仍为 NOT IMPLEMENTED，按 blocker 保留人工 proof，不降低条款。
- 验证已完成：`npm run lint` exit 0；`node --test` 对 dsl-ownership / ratchet / blogger-convergence-gaps 共 50 通过。

## Final outcome

**Outcome**：已闭环。在批准范围内（不扩功能）完成 DSL 结构化程序收敛：删除双写与影子状态、确立物理 flight registry 为唯一 busy 来源，并以人工 proof 满足 DSL-005 组合证明；类型级自动组合计数按 blocker 保留为后续增强，不降低 DSL-005。

**Final specification**：正式条款仍在 `docs/what/dsl-structured-program.md`、`docs/shape/dsl-structured-program.md`、`docs/how/dsl-structured-program.md`、`docs/proof/dsl-structured-program.md`；DSL-005 人工证明在 `docs/proof/dsl-structured-program.md` 的「正交组合证明（引用 DSL-005，人工）」一节。本文件是历史变更记录，不是当前产品规范。

**Implementation result**：删除 `BloggerRuntimeState`/`BloggerRuntimeCell` 双写与影子状态；物理 flight registry（`IParkedTransformHost.HasFlight` / `bloggerFlights`）成为 busy 与 current request 的唯一来源。DSL-005 组合以槽位分解 + 人工枚举证明；`scripts/checks/dsl-ownership.mjs` 类型级自动组合计数仍 NOT IMPLEMENTED，按 blocker 保留人工 proof。

**Verification**：`npm run lint` exit 0（含 spec-check、dsl-ownership、ratchet 等门禁）；`node --test` 对 dsl-ownership / ratchet / blogger-convergence-gaps 共 50 通过，其中 `tests/unit/enforcer/blogger-convergence-gaps.test.mjs` 的 C0 断言确认 `HasFlight` 为唯一 busy 且无 shadow state API。

**References**：`docs/{what,shape,how,proof}/dsl-structured-program.md`；`scripts/checks/dsl-ownership.mjs`、`scripts/checks/dsl-ownership-ratchet.mjs`；`tests/unit/verify/dsl-ownership.test.mjs`、`tests/unit/verify/dsl-ownership-ratchet.test.mjs`、`tests/unit/enforcer/blogger-convergence-gaps.test.mjs`；实现移除见 commit `08e8a609`。
