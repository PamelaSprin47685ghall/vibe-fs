# advisor — WHAT

顾问（advisor）是调用者按需请来的第二意见：一个**同一 session 专用**的评审参与者，读调用者交出的 view 与 intent，只回散文，不持有任何工具。仿照 `@juicesharp/rpiv-advisor` 的 advisor 策略，但顾问不是一次性 side-call，而是**按调用 session 归属的常驻专席**。

## [001] 专席归属与「孙子变儿子」

同一个调用 session 在**同一进程内**的 advisor 请求复用同一个 advisor：该专席按调用 session 按需生成，历史随调用累积。当前席位没有 durable link，重启后会重新建立；下文施工分期中的耐久归属与重启收养尚未实现。顾问由 `Sessions.createChildSession` 建立，因此物理父级是**家族根**（delegation-007：儿子生的儿子仍是儿子）——subagent 请顾问不会产生 sub-subagent。

## [002] 请求由三样独立材料组成，不合并

一次调用交给顾问的完整请求是**若干条彼此独立的 prompt**，绝不拼成一段：

1. 顾问自己的 system prompt（`resources/provider/role/advisor/**`，由 Host agent 配置与 system transform 投影）；
2. `view`——调用者交出的上下文视图，与共享 `{ prefixUntil, thenMap }` 使用同一套区间选择和历史替换规则；prefixUntil 是替换保护边界，不截断发送材料。按仓库既有的 blogger input 格式渲染（`# ` 注释表头 + `[[new_work_to_record]]` 数据表，`SyntheticToml` 词汇）；
3. `intent`——本次要顾问裁决的问题，**自成一条 user prompt**，与 view 不合并。

## [003] follow-up 追加 view 增量 + 新 intent

同一进程内的后续调用追加 view 的增量及新的 intent。先生成完整视图，再按代表锚点与精确材料字节过滤已成功交付的内容；不得先删旧输入再计算区间。相同材料不重发，改变的摘要须交付一次。游标按调用 session 记录，随进程内专席一起失效；不宣称重启后能复用旧游标。

## [004] 无工具、散文返回

顾问 office 不携带任何 `ToolPermission`（`OfficeCapability.permissions Role.Advisor = Set.empty`）。它的回答就是本次新终态中的正式散文，不从历史WorkRecord或旧快照补值。失败、取消或空回答明确拒绝，不能重放上次建议。

## [005] 无授权副作用

顾问读不到仓库、跑不了命令、改不了任何东西，也不介入调用者的授权：请求顾问不改变调用者的 capability、不创建 Interaction Authority Root、不写调用者的会话历史。

## [006] `js.advisor` 是编程面成员

调用面是 `js.advisor(intent, view)`：与其他 `js.*` 成员一致，**返回数据而非渲染后的 prose**（`{ ok, advice }` 一类信封），渲染只在工具结果边界发生。`js.horizon` 同样以成员身份返回名册数据（含每个子智能体的四数校准），不再只靠宿主工具呈现。

## 施工分期（继任者按此续作）

1. **已完成**：`Role.Advisor` agent type——内部角色、无工具、persona Auditor、role law 双语资源、Host agent 条目（hidden）、system prompt 投影、交互修复分区排除。
2. **专席归属**：durable link 事实（仿 `CompanionFactCases.CompanionBloggerLinked`）、`SessionAssociationProjection` 投影、进程内 runtime cell（仿 `Context/Companion/Runtime.fs`）、单飞 ensure（仿 `CompanionHost.EnsureBloggerAsync`）、重启收养（journal-linked SessionId 证明归属，不靠 Host parentID）。
3. **请求装配与增量**：view 选择（复用 `Anchor/AnchorSurface` 与本轮 transform 的 view 应用）→ `BloggerToml.dataBlock`/`SyntheticToml.document` 渲染；intent 独立成条；已交付游标 durable 化；交付走与该专席的托管派发等价路径，读回其散文。
4. **`js.advisor` / `js.horizon` 成员**：`ToolPermission.Advisor`（六处机械面：`ToolRegistry.staticAdmissions`、`baseSpecs`、`OfficeCapability` permissions/label、`StaticTools` toolNames/allPermissions/knownToolNames/permissionFor、`resources/ablation/tool-map.json`、`Strength/InvestigationEstimateContract.classifyTool`）+ `JsCapability.Advisor`/`Horizon` 与 capability fragment（member/signature/description/example/binding key 四层一致）+ `ToolsBindings` 成员 + `JsDelegationHost` 闭包与两处 install（`ToolRegistry.fs` 的生成面宿主、`Repository/Programming/Js/RuntimeSurface.fs`）+ 基类 stub 与描述。`js.horizon` 复用既有 `ToolPermission.Horizon`，无需新权限。
5. **测试与教学**：本包测试按条款编号 `NNN.test.mjs` 锚定 `WHAT[advisor-NNN]`；`repository-programming` 的四层同构与 `participant-horizon` 的名册测试同步扩展。
