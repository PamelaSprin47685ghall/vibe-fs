# concern-routing 测试范围

规则来自 [WHAT.md](../WHAT.md)。本包固定交互路由的现行边界：跨参与者与用户交互走编程面（`fork`、`join`、`deliver`、`ask`），不存在按关注点寻址的邮箱路由。

测试验证：

- `001.test.mjs`：`HostSignalBootstrap` 不含 `ensureSelfMailbox` / `subscribeSelfMailbox` 自动邮箱订阅接线。
- `002.test.mjs`：`PairProgrammingThoughtTransform` 不准备或注入 `prepareConcernFragments` / `ConcernPlacement`。
- `003.test.mjs`：`publish` 不在 `ToolRegistry` 与 `resources/provider/concern-routing` 中，通信由编程面 `ask` / `deliver` / `fork` / `join` 承载。
- `004.test.mjs`：`HostFactFold` 不折叠 `applyConcernPlacement`。
- `005.test.mjs`：`Interaction/Concern` 与 `ConcernTools` 模块及编译分片不存在。
- `006.test.mjs`：`AttentionConcernJournalAdapter` 与 `retireMailboxesOf` 不存在。
- `007.test.mjs`：`Fact`、`ProjectionUpdate`、`Fold`、`AgentJournalPortAdapter` 与 `fact-map.json` 不含 `Concern` 持久家族。
