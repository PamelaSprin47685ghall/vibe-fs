# advisor — WHAT

顾问（advisor）是调用者按需请来的第二意见：一个**同一 session 专用**的评审参与者，读调用者交出的 view 与 intent，只回散文，不持有任何工具。仿照 `@juicesharp/rpiv-advisor` 的 advisor 策略，但顾问不是一次性 side-call，而是**按调用 session 归属的常驻专席**。

## [001] 专席归属与「孙子变儿子」

同一个调用 session 的 advisor 请求**永远路由到同一个 advisor**：该专席按调用 session 按需生成（首次调用时建立），此后逐次复用，历史随调用累积。顾问由 `Sessions.createChildSession` 建立，因此物理父级是**家族根**（delegation-007：儿子生的儿子仍是儿子）——subagent 请顾问不会产生 sub-subagent。

## [002] 请求由三样独立材料组成，不合并

一次调用交给顾问的完整请求是**若干条彼此独立的 prompt**，绝不拼成一段：

1. 顾问自己的 system prompt（`resources/provider/role/advisor/**`，由 Host agent 配置与 system transform 投影）；
2. `view`——调用方用 `carry` 从可见历史中选择的背景材料，保持结构化条目直到 pretty print 为合法 TOML；
3. `intent`——本次要顾问裁决的问题，**自成一条 user prompt**，与 view 不合并。

调用面是 `this.advisor(intent, carry = [])`。第二参数按 context-compression [032] 的统一数组规则选择材料：整数引用可见历史锚点的完整区间，字符串表示字面文本，按数组顺序展开。没有前缀参数，也不附加调用方当前回合；问题在 `intent` 中表达一次。材料选择只读，不消费待应用的压缩声明，不改变调用方的可见历史。

省略 `carry` 等同于 `[]`。非法数组或无效锚点返回 `{ ok: false, code: "VIEW_DECLARATION_INVALID", reason }`，不咨询顾问、不留下部分效果。数组不会被当作对象映射解释，也不接受包装对象。

`intent` 与材料是分离的提示输入：intent 自成一条 user prompt，材料单独渲染，不合并成一段，也不把已渲染的 TOML 再嵌入字符串字段。

## [003] follow-up 追加 view 增量 + 新 intent

后续调用追加材料增量与新的 intent。每次先按本次 `carry` 展开完整材料，再用宿主记录的已交付身份计算增量；不能在选择区间之前删掉已交付锚点。省略数组仍是合法咨询，可以只追加新问题。

已交付游标按调用 session 与顾问 session 记录。身份包含精确内容，不能仅按锚点编号忽略变化后的材料。只有成功交付后才推进游标；LLM 不需要维护或传入已交付集合。咨询不能修改调用方的待应用声明或压缩状态。

## [004] 无工具、散文返回

顾问 office 不携带任何 `ToolPermission`（`OfficeCapability.permissions Role.Advisor = Set.empty`）。它的回答就是散文：调用者拿到的是被建议的散文本身，而不是渲染后的工具回执；空回答按失败处理，不伪造建议。

## [005] 无授权副作用

顾问读不到仓库、跑不了命令、改不了任何东西，也不介入调用者的授权：请求顾问不改变调用者的 capability、不创建 Interaction Authority Root、不写调用者的会话历史。

## [006] `this.advisor` 是编程面成员

调用面是 `this.advisor(intent, carry = [])`：与其他基类成员一致，返回 `{ ok, advice }` 数据信封；拒绝时是 `{ ok: false, code, reason }`，如非法数组的 `VIEW_DECLARATION_INVALID`。渲染只在工具结果边界发生。advisor 不另设独立 Host 工具注册，宿主经成员闭包 `JsDelegationHost.Advisor` 承接调用。`this.horizon` 同样以成员身份返回名册数据（含每个子智能体的四数校准）。

## 施工分期（继任者按此续作）

1. **已完成**：`Role.Advisor` agent type——内部角色、无工具、persona Auditor、role law 双语资源、Host agent 条目（hidden）、system prompt 投影、交互修复分区排除。
2. **专席归属**：进程内 seat 表按调用 session 归属 advisor child（由 `Sessions.createChildSession` 建立，物理父级为家族根），不写 durable link。
3. **请求装配与增量**：复用统一 `carry` 校验与材料选择，保持结构化数据直到 `SyntheticToml.document` 渲染；intent 独立成条；已交付游标按会话记录；交付走与该专席的托管派发等价路径，读回其散文。
4. **`this.advisor` / `this.horizon` 成员**：`ToolPermission.Advisor`（六处机械面：`ToolRegistry.staticAdmissions`、`baseSpecs`、`OfficeCapability` permissions/label、`StaticTools` toolNames/allPermissions/knownToolNames/permissionFor、`resources/ablation/tool-map.json`、`Strength/InvestigationEstimateContract.classifyTool`）+ `JsCapability.Advisor`/`Horizon` 与 能力到成员名的注册表、本地化基类伪代码与相同能力集的运行时投影+ `ToolsBindings` 成员 + `JsDelegationHost` 闭包与两处 install（`ToolRegistry.fs` 的生成面宿主、`Repository/Programming/Js/RuntimeSurface.fs`）+ 基类伪代码与描述。`this.horizon` 复用既有 `ToolPermission.Horizon`，无需新权限。
5. **测试与教学**：本包测试按条款编号 `NNN.test.mjs` 锚定 `WHAT[advisor-NNN]`；`repository-programming` 的四层同构与 `participant-horizon` 的名册测试同步扩展。

