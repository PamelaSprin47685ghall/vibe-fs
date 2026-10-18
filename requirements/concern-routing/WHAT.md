# concern-routing — WHAT

本包现为退休边界（superseded by the programming surface `js.ask`, `js.deliver`, `js.fork`, and `js.join`）：concern-addressed 邮箱（`subscribe` 与 `publish` 工具、会话自动订阅、Pair Hint 邮箱注入与 `Concern` 持久投影）已整体退役。保留这些条款，是为了锚定替代路径并防止已退役的邮箱路由协议回流生产路径。

## [001] 会话建立不再自动订阅自身邮箱

`subscribe` 工具与会话物理准入时的自动邮箱订阅（`ensureSelfMailbox` / `subscribeSelfMailbox`）均已退役。`HostSignalBootstrap` 在 `chat.message` 准入时不再创建或查询任何 `Concern` 邮箱。

## [002] Pair Hint 不再注入邮箱公告或消息片段

`PairProgrammingThoughtTransform` 不再准备或渲染 `concern-routing/{pair-heading,subscription-announcement,message-delivery}` 片段，也不再在 `PairProgrammingGuidelineWire` 上携带 `ConcernPlacement`。

## [003] publish 退役，跨参与者与用户交互并入编程面

`publish(id, message)` 工具已彻底退役，不在 `StaticTools.knownToolNames`、`permissionFor`、`requestToolMap`、`ToolRegistry` 或 `resources/ablation/tool-map.json` 中登记或放行；`resources/provider/concern-routing/**` 资源文件全部删除。跨参与者交付与向人类用户提问改由编程面（`js.fork`、`js.join`、`js.deliver`、`js.ask`）与对应角色工具承担。

## [004] HostFactFold 不再折叠 ConcernPlacement

`HostFactFold` 在折叠 `PairProgrammingGuidelineAnchored` 时只更新 `Guidelines` 投影，不再调用 `ConcernProjection.applyPlacement` 或修改任何 `Concern` 投影状态。

## [005] 不存在 Interaction/Concern 运行时或 ConcernTools 模块

生产编译图不得包含 `Interaction/Concern`、`OpenCode/Tools/ConcernTools` 或 `Composition/Durable/AttentionConcernJournalAdapter`，也不得保留 `Wanxiangshu.Owner.concern-routing.*` 编译分片。

## [006] 参与者终结不再执行邮箱退休追加

参与者任期完成、放弃或替换不再调用 `retireMailboxesOf` 或追加 `MailboxRetired` 事实。

## [007] durable fact 与 projection 不含 Concern

`AgentFact`、`AgentProjectionSet`、`ProjectionUpdate`、`Fold`、`AgentJournalPortAdapter` 与 `resources/ablation/fact-map.json` 均不得包含 `Concern`、`MailboxSubscribed`、`MessagePublished`、`MailboxRetired` 或同义持久状态。
