# advisor — WHAT

顾问（advisor）是调用者按需请来的第二意见：一个**同一 session 专用**的评审参与者，读调用者交出的 view 与 intent，只回散文，不持有任何工具。仿照 `@juicesharp/rpiv-advisor` 的 advisor 策略，但顾问不是一次性 side-call，而是**按调用 session 归属的常驻专席**。

## [001] 专席归属与「孙子变儿子」

同一个调用 session 的 advisor 请求**永远路由到同一个 advisor**：该专席按调用 session 按需生成（首次调用时建立），此后逐次复用，历史随调用累积。顾问由 `Sessions.createChildSession` 建立，因此物理父级是**家族根**（delegation-007：儿子生的儿子仍是儿子）——subagent 请顾问不会产生 sub-subagent。

## [002] 请求由三样独立材料组成，不合并

一次调用交给顾问的完整请求是**若干条彼此独立的 prompt**，绝不拼成一段：

1. 顾问自己的 system prompt（`resources/provider/role/advisor/**`，由 Host agent 配置与 system transform 投影）；
2. `view`——调用方**当前区间**按 `reportMapping` 投影出的上下文视图，按仓库统一的 TOML 视图格式渲染（`# ` 注释表头 + `[[new_work_to_record]]` 数据表，`SyntheticToml` 词汇）；
3. `intent`——本次要顾问裁决的问题，**自成一条 user prompt**，与 view 不合并。

调用面是 `this.advisor(intent, reportMapping)`：第二参数是 **reportMapping**，形态为 `Record<string, null|string>`（键是非负整数锚点编号字符串，值只能是 `null` 或非空摘要字符串），**没有** `prefixUntil` 字段。它等价于统一声明 `{ prefixUntil: 0, thenMap: reportMapping }`（`AnchorSurface.parseReportMapping`）：因此没有受保护的前缀段，锚点 0 本身可被替换，映射里未出现的锚点按保留处理。声明与投影复用 `AnchorSurface.parseReportMapping` / `AnchorSurface.reportMaterial`（只读投影：不消费 pending view、不写永久压缩），语义依 context-compression [031]。

`reportMapping` **必填**：缺失（`undefined`/`null`）或形状非法（缺字段、非法编号、非对象映射、非法值或空白摘要）→ 整次调用以 `VIEW_DECLARATION_INVALID` 拒绝，交回数据信封 `{ ok: false, code: "VIEW_DECLARATION_INVALID", reason }`，不咨询顾问、不留下部分效果。**「缺省按空映射静默处理」的宽容路径已退役**：空对象 `{}` 是合法映射（等价于不做任何替换），而缺失的 `reportMapping` 是非法输入。

一次咨询的可见范围 = 调用方自己的当前区间，按 `reportMapping` 投影：被摘要的锚点以其最新完整区间（其代表的原文、无独立锚点的内容及此前并入的摘要）整体替换，`null` 与漏项保留原文，隐含锚点 0 永远指向内容最开头。共享已有压缩状态与存活锚点的最新区间。`reportMapping` 不是内容交付范围，也不是已交付位置：交付的是投影后的视图本身。

`intent` 与 `reportMapping` 是两个分离的提示输入，互不合并：intent 自成一条 user prompt，reportMapping 只决定投影范围，不并入 intent 文本，也不与 view 文本合成一条。

## [003] follow-up 追加 view 增量 + 新 intent

后续调用**追加**而不是重发：每次 follow-up 同样必须携带自己的 `reportMapping`（形态与必填性同 [002]，缺失或非法即 `VIEW_DECLARATION_INVALID` 拒绝）。只把按本轮 `reportMapping` 投影出的 view 的**增量**（相对上次已交付给该顾问的部分）渲染成新的 view prompt，其后追加新的 intent prompt。已交付游标按 (调用 session, 顾问 session) 记录；顾问看到的是它已经被交底的部分加上新增部分。

必须先生成完整 view（按本轮 `reportMapping` 投影的调用方当前区间），再按宿主记录的该专席已接收身份计算 delta；不能先去掉已交付锚点再生成投影，否则会错误保留其区间中已经被代表的旧内容。已交付身份必须区分同一锚点的不同呈现内容，特别是可反复替换的隐含锚点 0；不能仅凭编号压掉新摘要。仅成功交付后推进游标；LLM 不传入已交付集合。咨询不能修改调用者的 pending view 或永久压缩状态。

## [004] 无工具、散文返回

顾问 office 不携带任何 `ToolPermission`（`OfficeCapability.permissions Role.Advisor = Set.empty`）。它的回答就是散文：调用者拿到的是被建议的散文本身，而不是渲染后的工具回执；空回答按失败处理，不伪造建议。

## [005] 无授权副作用

顾问读不到仓库、跑不了命令、改不了任何东西，也不介入调用者的授权：请求顾问不改变调用者的 capability、不创建 Interaction Authority Root、不写调用者的会话历史。

## [006] `this.advisor` 是编程面成员

调用面是 `this.advisor(intent, reportMapping)`：与其他基类成员一致，**返回数据而非渲染后的 prose**（`{ ok, advice }` 一类信封；拒绝时是 `{ ok: false, code, reason }`，如 reportMapping 非法时的 `VIEW_DECLARATION_INVALID`），渲染只在工具结果边界发生。advisor 已是编程面成员，**不另设独立 Host 工具注册**：宿主侧只经成员闭包（`JsDelegationHost.Advisor`）承接调用，不为顾问单独注册工具。`this.horizon` 同样以成员身份返回名册数据（含每个子智能体的四数校准），不再只靠宿主工具呈现。

## 施工分期（继任者按此续作）

1. **已完成**：`Role.Advisor` agent type——内部角色、无工具、persona Auditor、role law 双语资源、Host agent 条目（hidden）、system prompt 投影、交互修复分区排除。
2. **专席归属**：进程内 seat 表按调用 session 归属 advisor child（由 `Sessions.createChildSession` 建立，物理父级为家族根），不写 durable link。
3. **请求装配与增量**：reportMapping 必填校验与投影（复用 `AnchorSurface.parseReportMapping` / `reportMaterial`，非法即 `VIEW_DECLARATION_INVALID` 拒绝）→ `SyntheticToml.document` 渲染；intent 独立成条；已交付游标按会话记录；交付走与该专席的托管派发等价路径，读回其散文。
4. **`this.advisor` / `this.horizon` 成员**：`ToolPermission.Advisor`（六处机械面：`ToolRegistry.staticAdmissions`、`baseSpecs`、`OfficeCapability` permissions/label、`StaticTools` toolNames/allPermissions/knownToolNames/permissionFor、`resources/ablation/tool-map.json`、`Strength/InvestigationEstimateContract.classifyTool`）+ `JsCapability.Advisor`/`Horizon` 与 能力到成员名的注册表、本地化基类伪代码与相同能力集的运行时投影+ `ToolsBindings` 成员 + `JsDelegationHost` 闭包与两处 install（`ToolRegistry.fs` 的生成面宿主、`Repository/Programming/Js/RuntimeSurface.fs`）+ 基类伪代码与描述。`this.horizon` 复用既有 `ToolPermission.Horizon`，无需新权限。
5. **测试与教学**：本包测试按条款编号 `NNN.test.mjs` 锚定 `WHAT[advisor-NNN]`；`repository-programming` 的四层同构与 `participant-horizon` 的名册测试同步扩展。
