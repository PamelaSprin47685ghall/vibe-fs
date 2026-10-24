# advisor — WHAT

顾问（advisor）是同一调用 session 专用、持续复用的第二意见参与者。其职责是根据**本次咨询意图和协商回复**给出建议，而不是阅读委托者整份对话或冒充做过工具检查。顾问只有 `negotiate`、`resetScope` 两个非授权性协议工具，不能读写文件、搜索、执行或修改环境。

## [001] 专席归属与「孙子变儿子」

同一个调用 session 的 advisor 请求永远路由到同一个专席；首次按需创建，此后复用会话历史。顾问经 `Sessions.createChildSession` 挂在家族根层，维持物理父级压平的既有约束。

## [002] 一份当前意图，不传历史上下文

`this.advisor(intent)` 保留唯一的 intent 参数。每次只把当前咨询意图渲染成一份 user 请求；不发送完整 post-scope 视图、TOML `[[context]]`、此前材料的过滤数组或第二个 user prompt。顾问只知道当前指令；缺少决定性事实时向调用者提问。

## [003] follow-up 与迟到协商

每次 follow-up 同样只发送最新 intent，复用既有顾问专席。首次 `negotiate(questions: string)` 是强制协议：工具立刻报告问题已接纳发送，委托者的回答可以隔几轮通过真实 user steer 到来。此前 `resetScope` 不激活；答复 steer 到达后尽快调用 `resetScope` 开始正式闭环。其后可再随时协商，但不阻碍正常 `resetScope`。

顾问和普通子智能体一样，在无未决工具调用的自然终态停机；宿主只交回该终态轮次的 assistant 散文，不返回顾问完整历史。咨询不改变调用者的 scope。

## [004] 无办公工具；仅两个协议工具

`OfficeCapability.permissions Role.Advisor = Set.empty` 保持不变：顾问没有任何办公 ToolPermission。唯一例外是两个不授权的协议工具 `negotiate` 与 `resetScope`，它们不能访问文件、命令、搜索、开发或变更工具。未答复首次协商之前的 `resetScope` 必须拒绝；收到真实 steer 以后可用。空建议或无实质内容的建议作为失败处理，不伪造完成。

## [005] 无授权副作用

顾问不读取仓库、不跑命令、不修改文件。协议协商必须在独立临时对话中向委托者询问，工具调用无法穿越执行前门禁；回答回到顾问的 user steer 不自动扩大顾问或调用者权能。不得用「工具已发送」回执冒充答复。

## [006] `this.advisor` 是编程面成员

调用面仍是 `this.advisor(intent)`，返回 `{ ok: true, advice: string }`，其中 `advice` 为顾问自然停机的末轮散文；拒绝时为 `{ ok: false, code: string, reason: string }`。渲染仅发生在调用返回边界，顾问没有额外 Host 注册的办公工具。其余 `this.horizon`、顾问席位与模型绑定规则保持不变。
