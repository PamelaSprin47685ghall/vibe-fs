# DSL 结构化程序闭环

> 本文件是变更工作记录，不是当前产品规范。
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

- [ ] 删除 `BloggerRuntimeState.Idle | InFlight` 与物理 flight registry 的双写。
- [ ] 让物理 single-flight Task/registry 成为 busy 与 current request 的唯一来源。
- [ ] 删除 runtime cell 中保存流程位置的影子状态及兼容旁路。
- [ ] 补齐对正交组合状态的可靠证明；不得以高误报正则代替类型级证据。
- [ ] 运行相关 proof、测试和仓库门禁。

### Completion criteria

1. 生产 busy/current request 只有一个物理 writer 和读取来源。
2. 不再用长期 cell 状态表示程序下一步。
3. DSL-005 的组合状态证明可以用永久测试或可靠静态门禁复现。
4. `docs/`、实现和 proof 一致，相关检查通过。

### Blockers

当前没有外部 blocker。正交组合状态检查必须避免误伤合法领域组合；若无法可靠自动判定，
应保留人工 proof，而不是降低 DSL-005。
