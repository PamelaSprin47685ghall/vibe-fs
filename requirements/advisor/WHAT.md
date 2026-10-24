# advisor — WHAT

顾问（advisor）是调用者按需请来的第二意见：一个**同一 session 专用**的评审参与者，读调用者交出的 view 与 intent，不持有任何工具。仿照 `@juicesharp/rpiv-advisor` 的 advisor 策略，但顾问不是一次性 side-call，而是**按调用 session 归属的常驻专席**。

## [001] 专席归属与「孙子变儿子」

同一个调用 session 的 advisor 请求**永远路由到同一个 advisor**：该专席按调用 session 按需生成（首次调用时建立），此后逐次复用，历史随调用累积。顾问由 `Sessions.createChildSession` 建立，因此物理父级是**家族根**（delegation-007：儿子生的儿子仍是儿子）——subagent 请顾问不会产生 sub-subagent。

## [002] 顾问请求由单一请求文档承载

一次调用交给顾问的请求由顾问的独立 system prompt 与一份单一的 user 请求文档组成（与 fork 载荷形式完全一致）：

1. 顾问自己的 system prompt（`resources/provider/role/advisor/**`，由 Host agent 配置与 system transform 投影）；
2. 单一请求文档：咨询意图散文作为 `# ...` 注释头（prose 属于指令），调用方当前的完整 post-scope LLM 可见消息视图（经最新 `resetScope` 清理后的全部保留消息）作为只读背景材料渲染为 TOML 数据平面（如 `[[context]]` 表）。不得拆成两条独立的 user prompt。

调用面是 `this.advisor(intent)`，单一参数。调用方不得传入第二参数。调用方的完整 post-scope 视图自动作为背景提供给顾问，不进行局部历史筛选，也不隐式继承未重置的旧过程。意图与材料是分离的提示输入：intent 自成一条 user prompt，材料单独渲染为 TOML 数据平面，不合并成一段，也不把已渲染的 TOML 再嵌入字符串字段。

## [003] follow-up 追加新请求文档，自然停机返回末轮散文

后续调用追加包含新 intent 与调用方当前最新完整 post-scope 视图的单一请求文档。每次调用均发送调用方最新的完整 post-scope 视图。LLM 不需要维护或传入已交付集合。

顾问会话与普通子智能体一致，在无未决工具调用的干净终态处自然停机；宿主在其自然停机后，把顾问该终态轮次的 assistant 散文返回给调用方，绝不捕获或渲染顾问会话的完整 post-scope 视图。咨询不改变调用方的 scope 状态。

## [004] 无工具、末轮散文返回

顾问 office 不携带任何 `ToolPermission`（`OfficeCapability.permissions Role.Advisor = Set.empty`）。顾问的回答就是其终态 assistant 散文；空回答或无实质内容的回答按失败处理，不伪造建议。调用者拿到的就是这份散文本身，不是顾问的完整上下文视图，亦非机器工具回执。

## [005] 无授权副作用

顾问读不到仓库、跑不了命令、改不了任何东西，也不介入调用者的授权：请求顾问不改变调用者的 capability、不创建 Interaction Authority Root、不写调用者的会话历史。

## [006] `this.advisor` 是编程面成员

调用面是 `this.advisor(intent)`：与其他基类成员一致，返回数据信封 `{ ok: true, advice: string }`，其中 `advice` 是顾问自然停机时的末轮 assistant 散文；拒绝时返回 `{ ok: false, code: string, reason: string }`（如席位无法作答 `advisor-unavailable`、宿主缺席 `DELEGATION_UNAVAILABLE` 等）。渲染只在工具结果边界发生。advisor 不另设独立 Host 工具注册，宿主经成员闭包 `JsDelegationHost.Advisor` 承接调用。`this.horizon` 同样以成员身份返回名册数据（含每个子智能体的四数校准）。

