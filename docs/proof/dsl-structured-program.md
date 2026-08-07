# DSL 结构化程序规则 — 证明

行为见 `what/dsl-structured-program.md`；边界见 `shape/dsl-structured-program.md`；
算法见 `how/dsl-structured-program.md`；相关未闭环工作见
[`changes/active/dsl-structured-program-gap.md`](../../changes/active/dsl-structured-program-gap.md)。

## 静态义务

| 门 | 必须判红的反例 |
|---|---|
| `scripts/checks/dsl-ownership.mjs --threshold=0` | 业务 Interpreter/Command-Reply 第二运行时、程序计数字段、未声明 mutable、跨文件同构 DU、未分类的大 DU、未登记 Infrastructure leak |
| `scripts/checks/architecture.mjs` | Domain 向上层依赖、源码根/fsproj 不一致、资源越界读取 |
| `scripts/checks/spec.mjs` | DSL Clause 重复、悬空或 Change 影子定义 |

每项新增静态规则必须有永久 fixture，并曾用故意反例证明仓库入口会失败。

## 动态义务

- 进程等待分别覆盖自然退出、deadline、kill acknowledgement 超时和等待中取消。
- Companion 恢复机会覆盖注册、单次消费、无机会 no-op 与重启不恢复 waiter。
- Blogger single-flight 覆盖 busy、parked、完成、取消与恢复，不从流程位置字段推断事实。
- Journal recovery 覆盖 evidence 不足时 fail closed，并证明重入公共 workflow。
- family fold 与迁移前 wire/Journal 兼容性按对应领域 proof 证明。

测试必须走公共契约面并断言可观察结果或端口调用；不得只断言内部 tag。

## 完成判据

1. Active Change 所列完成条件全部满足，并在同一文件追加 Final outcome 后移入 Completed。
2. 静态门禁无阈值上调或永久豁免逃逸。
3. 相关 unit、integration 与 canary 按 `proof/verify.md` 通过。
4. 删除旧状态后不存在双写、adapter facade 或仅为旧测试保留的旁路。
