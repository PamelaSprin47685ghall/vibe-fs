# concern-routing — WHAT

跨参与者与用户交互走显式工具：委派用 `fork`，汇合用 `join` 工具，交付走 `deliver`，向用户提问走 `ask`。系统不设按关注点寻址的邮箱路由——没有订阅/发布工具，没有自动订阅，没有 Pair Hint 捎带投递，durable 事实也不记录邮箱状态。本包条款固定这条边界，防止第二套通信状态机回流。

## [001] 会话准入不建立邮箱订阅

会话物理准入只登记会话本身。`chat.message` 准入路径不创建、不订阅、不查询任何按关注点寻址的邮箱；生产树中不得出现 `ensureSelfMailbox`、`subscribeSelfMailbox` 或 `Interaction/Concern` 接线。

## [002] Pair Hint 不携带邮箱公告或消息片段

`PairProgrammingThoughtTransform` 只渲染结对编程指导与预算提示。它不准备、不渲染邮箱公告、订阅通知或投递片段，wire 记录不携带 `ConcernPlacement` 这类邮箱位置字段。

## [003] 跨参与者交互并入编程面，无广播工具

不存在 `publish(id, message)` 之类的广播工具：`StaticTools.knownToolNames`、`permissionFor`、`requestToolMap`、`ToolRegistry` 与 `resources/ablation/tool-map.json` 均不登记或放行它，`resources/provider/concern-routing/` 下没有资源文件。跨参与者交付走 `deliver`，向用户提问走 `ask`，委派与汇合走 `fork` / `join`。

## [004] HostFactFold 只折叠结对指导

`HostFactFold` 折叠 `PairProgrammingGuidelineAnchored` 时只更新 `Guidelines` 投影，不调用 `ConcernProjection.applyPlacement`，不改动任何邮箱投影状态。

## [005] 不存在 Interaction/Concern 运行时或 ConcernTools 模块

生产编译图不包含 `Interaction/Concern` 目录、`OpenCode/Tools/ConcernTools` 模块或 `Composition/Durable/AttentionConcernJournalAdapter`，也不保留 `Wanxiangshu.Owner.concern-routing.*` 编译分片。

## [006] 参与者终结不追加邮箱退休事实

参与者任期完成、放弃或替换时只结清其工作义务，不调用 `retireMailboxesOf`，不追加 `MailboxRetired` 事实。

## [007] durable fact 与 projection 不含 Concern

`AgentFact`、`AgentProjectionSet`、`ProjectionUpdate`、`Fold`、`AgentJournalPortAdapter` 与 `resources/ablation/fact-map.json` 均不包含 `Concern`、`MailboxSubscribed`、`MessagePublished`、`MailboxRetired` 或同义持久状态。Host 事实族（`HostFactCases.PairProgrammingGuidelineAnchored`）是持久 AgentFact，不携带 `ConcernPlacement` 字段。
