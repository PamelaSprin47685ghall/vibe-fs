# advisor — README

本包规定「调用者按需请来的第二意见」这一席位：条款正文在 `WHAT.md`，用例覆盖与局限在 `tests/README.md`。这里补说机制与落点，说法与 `WHAT.md` 保持同一套，不新增规则。

## 席位与归属

同一个调用 session 的每次咨询都落到同一个顾问。`AdvisorSeat.consult` 首次调用为该 session 建立 advisor child——走 `ISessionHostPort.CreateChildSession`，因此物理父级是家族根而不是调用者本身——之后复用同一个 child，并在其自然停机后捕获其完整 post-scope 视图。进程内 seat 表按调用 session 记 child，重启即遗忘，下一次咨询重新建立：seat 不持有 durable link，也不写调用者的会话历史。子会话请顾问与顶级会话请顾问得到同样的顾问，不会产生 sub-subagent。

## 单一请求文档装配

顾问由 `Sessions.createChildSession` 建立，宿主内部以家族根为物理父级，与 Sphinx subagent 同级；顾问不再拥有子代理，不产生第二级会话拓扑。每次咨询由顾问独立的 system prompt 与一份单一的 user 请求文档组成：intent 散文骑乘 `# ...` 注释头，调用方完整的 post-scope 视图骑乘 TOML `[[context]]` 数据。

## 完整视图传递

系统废除已退役的 delta 游标与增量跟踪。调用方当前的完整 post-scope LLM 可见视图经最新 `resetScope` 整理后渲染为 TOML 作为背景直接提供给顾问专席；顾问自然停机后其完整 post-scope 视图同样以 TOML 格式返回调用方。

## 成员与信封

`this.advisor(intent)` 由 `JsCapability.Advisor` 投影：注册表提供成员名，本地化基类伪代码提供接口、说明与例子，运行时按同一能力集提供绑定，`ToolsBindings.makeAdvisorMember` 把宿主闭包（`JsDelegationHost.Advisor`）注入成员，两处 install 分别在 `ToolRegistry` 的生成面宿主与 `RuntimeSurface`。宿主接收单一请求文档后调用 `AdvisorSeat.consult`，顾问自然停机后交回其完整 post-scope 视图，返回 `{ ok: true, context }` 或 `{ ok: false, code, reason }`，其中 `context` 为顾问完整 post-scope TOML 视图；席位无法作答为 `advisor-unavailable`，宿主缺席为 `DELEGATION_UNAVAILABLE`。拒绝是数据，程序不需要为它写 catch；渲染只在工具结果边界发生。

## 无工具与无副作用

`OfficeCapability.permissions Role.Advisor = Set.empty`，`StaticTools` 不为 `ToolPermission.Advisor` 准入任何工具，agent 条目 hidden——顾问只能读被交给它的材料，其终态输出为其回答散文并随完整 post-scope 上下文交回。一次咨询不改变调用者的 capability、不创建 Interaction Authority Root、不写调用者的历史；空回答按失败返回，不伪造建议。

## 局部运行

`node --test requirements/advisor/tests/*.test.mjs`（构建须 `node scripts/build.mjs`；红绿由 DevOps 执行）。

