# concern-routing 测试与证明范围

规则来自 WHAT；本页不增加规则。concern 邮箱（`subscribe`、`publish`、自动订阅与 Pair Hint 邮箱投递）已整体退役（superseded by `js.ask`, `js.deliver`, `js.fork`, and `js.join`）。

测试验证：
- `001.test.mjs`：`HostSignalBootstrap` 不再包含 `ensureSelfMailbox` / `subscribeSelfMailbox` 自动邮箱订阅接线。
- `002.test.mjs`：`PairProgrammingThoughtTransform` 不再准备或注入 `prepareConcernFragments` / `ConcernPlacement`。
- `003.test.mjs`：`publish` 从 `ToolRegistry` 与 `resources/provider/concern-routing` 中彻底移除，通信由编程面 `js.ask` / `js.deliver` / `js.fork` / `js.join` 承载。
- `004.test.mjs`：`HostFactFold` 不再折叠 `applyConcernPlacement`。
- `005.test.mjs`：`Interaction/Concern` 与 `ConcernTools` 模块及编译分片已删除。
- `006.test.mjs`：`AttentionConcernJournalAdapter` 与 `retireMailboxesOf` 已删除。
- `007.test.mjs`：`Fact`、`ProjectionUpdate`、`Fold`、`AgentJournalPortAdapter` 与 `fact-map.json` 不再包含 `Concern` 持久家族。
