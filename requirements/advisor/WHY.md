# advisor — WHY

调用者需要第二意见时，最贵的往往不是顾问的推理，而是把已经发生的自己重新讲一遍。每次咨询都新建一个无状态 side-call，就只能重发整段背景，或者让顾问在信息不全时给出语气肯定的答案；而一旦顾问本身携带工具、能读仓库或跑命令，它就从评审变成第二个执行者，调用者的授权边界被绕过，产生的后果也不再属于调用者。

顾问因此是按调用 session 归属的常驻专席：同一个 session 的请求永远落到同一个 advisor，首次咨询按需建立，之后逐次复用，历史随调用累积。专席由调用者的 child session 通道建立，物理父级是家族根而不是调用者本身（与 `delegation-007` 的压平规则同源：儿子生的儿子仍是儿子），所以子会话请顾问不会生出 sub-subagent，顾问的可见面也不会因为调用者自己的层级而改变。

请求由顾问独立的 law（角色资源，由 Host agent 配置与 system transform 投影）以及一份单一的 user 请求文档组成（与 fork 载荷形式一致）：意图散文作为 `# ...` 注释头承载问题，调用方当前的完整 post-scope 消息视图作为 TOML `[[context]]` 表承载背景数据。顾问据此分清哪一段是它必须遵守的 law、哪一段只是待评审的材料、哪一段是它要回答的问题——role law 也据此把背景明确标成材料而非指令。

每次咨询均发送调用方当前最新完整的 post-scope 视图；顾问自然停机后，交回的完成结果是其末轮 assistant 散文，而不是它自己的完整视图。

顾问不产生任何授权副作用：它读不到仓库、跑不了命令、改不了任何东西，也不介入调用者的授权——请求顾问不改变调用者的 capability、不创建 Interaction Authority Root、不写调用者的会话历史。顾问的末轮 assistant 散文就是意见；空回答按失败处理，不伪造建议，"advisor did not answer" 是拒绝而不是一份意见。

调用面是 `this.advisor(intent)`：与其他 `js.*` 成员一致返回数据（`{ ok: true, advice }`，或 `{ ok: false, code, reason }` 的拒绝信封），返回的 `advice` 为顾问自然停机时的末轮 assistant 散文——顾问的完整上下文是它自己的过程，不是调用者需要的东西。渲染只在工具结果边界发生，成员本身不产出 provider 词汇。没有该能力的表面不携带这个成员；能力缺失时以显式拒绝收场，而不是静默降级成"没有建议"。

## 边界与复用

- view 的 TOML 渲染归本包。系统废除已退役的局部 Anchor 投影与 delta 游标，调用方与顾问之间的背景传递统一采用完整的 post-scope 视图并渲染为 TOML。
- 顾问"不携带任何 ToolPermission"的效力来自 `office-capability`（`Role.Advisor` 的 office 为空集）与 `capability-enforcement`（provider 看见的能力与 runtime 真能执行的能力同源且不扩大）；本包不通过呈现创造或取消任何权限。
- 专席的建立与收束走 Host 的 session 通道（`host-boundary`、`managed-session-lifecycle`），advisor child 的生命周期归 Host 所有；本包不维护第二套会话拓扑，也不把 seat 当成 durable 事实。
- 名册与 `js.horizon` 的呈现归 `participant-horizon`；语言选择与双语 law 的成对性归 `provider-language`。

## 本包拥有的源与资源面

- `src/Wanxiangshu/Execution/Delegation/Advisor/`（`View`、`Seat`、`Cursor` 及其接口文件）；
- `src/Wanxiangshu/Foundation/Roles.fs` 与 `Foundation/OfficeCapability.fs` 中的 Advisor 角色与权限条目；
- `src/Wanxiangshu/Repository/Programming/Js/` 的 `Capability`、`Description`、`GeneratorSurface`、`ToolsBindings`、`DelegationBridge`、`RuntimeSurface` 中 `js.advisor` 的成员、签名、描述、示例与绑定；
- `src/Wanxiangshu/OpenCode/Tools/` 的 `StaticTools`、`ToolRegistry` 中 advisor agent 配置与 `ToolPermission.Advisor` 的准入与成员宿主；
- `resources/provider/role/advisor/**`（角色 law）、`resources/provider/lifecycle/advisor/view/**`（view 交付说明）、`resources/provider/tool/js-program/delegation-rules/**`（顾问使用教学）。

包外路径清单见 `APPLIES-TO`；本包自身目录天然覆盖，不在该文件中声明。

## DEPENDS ON

- `office-capability`
- `capability-enforcement`
- `participant-identity`
- `provider-projection`
- `repository-programming`
- `host-boundary`
- `managed-session-lifecycle`
