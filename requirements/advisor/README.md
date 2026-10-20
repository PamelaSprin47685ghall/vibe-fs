# advisor — README

本包规定「调用者按需请来的第二意见」这一席位：条款正文在 `WHAT.md`，用例覆盖与局限在 `tests/README.md`。这里补说机制与落点，说法与 `WHAT.md` 保持同一套，不新增规则。

## 席位与归属

同一个调用 session 在同一进程内复用顾问。`AdvisorSeat.consult` 首次调用走 `ISessionHostPort.CreateChildSession`，物理父级是家族根。后续咨询订阅新的终态，只取该终态中的正式散文；旧终态、历史WorkRecord和快照不补值。进程内 seat 表重启即遗忘，不持有 durable link，也不写调用者的会话历史。子会话请顾问不会产生 sub-subagent。

## 三材料装配

`AdvisorView.plan(language, delivered, request)` 是纯函数。它先复用 AnchorView 生成完整 view：输入为调用者消息、已有区间替换历史和统一声明 `{ prefixUntil, thenMap }`；再按宿主已交付集合取增量。prefixUntil 只保护当轮修改，不是交付上界或下界；首次交付整个 view。TOML 保持 `# ` 注释表头、空行及每记录一张 `[[new_work_to_record]]` 表，正文放在 record 字段。view 与 intent 独立发送，不合并。

## 增量与游标

`Cursor` 按调用 session 记录已经成功交付的视图材料身份。身份包含代表锚点和精确呈现内容，不能仅记编号：隐含锚点 0 永久存在，其新摘要仍是新材料。同一份材料不会重复发送；同编号的内容变化会产生增量。只有咨询成功后调用 `record`；失败与未确认结果不推进。`clear` / `clearAll` 用于相应专席清理。无增量时仍可独立追加新 intent。

## 成员与信封

`this.advisor(intent, view)` 由 `JsCapability.Advisor` 投影：注册表提供成员名，本地化基类伪代码提供接口、说明与例子，运行时按同一能力集提供绑定，`ToolsBindings.makeAdvisorMember` 把宿主闭包（`JsDelegationHost.Advisor`）注入成员，两处 install 分别在 `ToolRegistry` 的生成面宿主与 `RuntimeSurface`。宿主闭包本身实现 view 声明解析（非法声明拒绝为 `VIEW_DECLARATION_INVALID`）、调用 `AdvisorView.plan` + `AdvisorSeat.consult`、成功后推进游标，返回值是数据信封：`{ ok: true, advice }`，或 `{ ok: false, code, reason }`——席位无法作答为 `advisor-unavailable`，宿主缺席为 `DELEGATION_UNAVAILABLE`。拒绝是数据而非抛出，程序不需要为它写 catch；渲染只在工具结果边界发生。

## 无工具与无副作用

`OfficeCapability.permissions Role.Advisor = Set.empty`，`StaticTools` 不为 `ToolPermission.Advisor` 准入任何工具，agent 条目 hidden——顾问只能读被交给它的材料，回答只能是散文。因此一次咨询不改变调用者的 capability、不创建 Interaction Authority Root、不写调用者的历史；空回答按失败返回，不伪造建议。

## 局部运行

`node --test requirements/advisor/tests/*.test.mjs`（构建须 `node scripts/build.mjs`；红绿由 DevOps 执行）。
