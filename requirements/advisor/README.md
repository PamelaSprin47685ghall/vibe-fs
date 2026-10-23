# advisor — README

本包规定「调用者按需请来的第二意见」这一席位：条款正文在 `WHAT.md`，用例覆盖与局限在 `tests/README.md`。这里补说机制与落点，说法与 `WHAT.md` 保持同一套，不新增规则。

## 席位与归属

同一个调用 session 的每次咨询都落到同一个顾问。`AdvisorSeat.consult` 首次调用为该 session 建立 advisor child——走 `ISessionHostPort.CreateChildSession`，因此物理父级是家族根而不是调用者本身——之后复用同一个 child，并订阅它的终态读回散文。进程内 seat 表按调用 session 记 child，重启即遗忘，下一次咨询重新建立：seat 不持有 durable link，也不写调用者的会话历史。子会话请顾问与顶级会话请顾问得到同样的顾问，不会产生 sub-subagent。

## 三材料装配

`AdvisorView.plan(language, delivered, request)` 是纯函数。它先按 `carry` 数组从调用方可见历史中选择完整材料，再按宿主已交付集合取增量；没有前缀参数，不附加调用方当前回合。首次交付全部选定材料。结构化条目在最终边界 pretty print 为 TOML，正文换行真实呈现；材料与 intent 独立发送，不合并。

## 增量与游标

`Cursor` 按调用 session 记录已经成功交付的材料身份。身份包含精确呈现内容，不能仅记编号；同编号的内容变化会产生增量。只有咨询成功后调用 `record`；失败与未确认结果不推进。`clear` / `clearAll` 用于相应专席清理。无增量时仍可独立追加新 intent。

材料身份、增量过滤、账本（按调用 session 隔离、单调、只由成功的咨询推进）和散文读回（最后一条非空白 assistant 文本）的规则由 `wanxiangli/advisor` 承担（CUT-39）。主仓的 `Cursor`、`AdvisorView.plan` 和 `AdvisorSeat` 只持有进程内实例、生成 view 并渲染 TOML，不再各写一份。

## 成员与信封

`this.advisor(intent, carry)` 由 `JsCapability.Advisor` 投影：注册表提供成员名，本地化基类伪代码提供接口、说明与例子，运行时按同一能力集提供绑定，`ToolsBindings.makeAdvisorMember` 把宿主闭包（`JsDelegationHost.Advisor`）注入成员，两处 install 分别在 `ToolRegistry` 的生成面宿主与 `RuntimeSurface`。宿主校验数组后调用 `AdvisorView.plan` 与 `AdvisorSeat.consult`，成功后推进游标，返回 `{ ok: true, advice }` 或 `{ ok: false, code, reason }`；非法数组为 `VIEW_DECLARATION_INVALID`，席位无法作答为 `advisor-unavailable`，宿主缺席为 `DELEGATION_UNAVAILABLE`。拒绝是数据，程序不需要为它写 catch；渲染只在工具结果边界发生。

## 无工具与无副作用

`OfficeCapability.permissions Role.Advisor = Set.empty`，`StaticTools` 不为 `ToolPermission.Advisor` 准入任何工具，agent 条目 hidden——顾问只能读被交给它的材料，回答只能是散文。因此一次咨询不改变调用者的 capability、不创建 Interaction Authority Root、不写调用者的历史；空回答按失败返回，不伪造建议。

## 局部运行

`node --test requirements/advisor/tests/*.test.mjs`（构建须 `node scripts/build.mjs`；红绿由 DevOps 执行）。

