# CUT-06：WorkRecord 取材与视图 → 交付视图格

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-06) / [证据](../evidence/CUT-06/README.md)。

## 1. 认领

- Owner：`eng-cut06-goose`；Reviewer：无独立人工 reviewer（仓主授权自治集成，见 PROTOCOL）；认领提交 `ef0476046`。
- Base：主仓 `993e93683`，wanxiangli `a8b89a4`；工作树 `wt/CUT-06-main`、`wt/CUT-06-wxl`，分支 `cut/CUT-06`。
- 写集合：`src/Wanxiangshu/Repository/Programming/Js/DeliveredValues.fs`（`.fsi` 不变）、`wanxiangli` gitlink、`package-lock.json` 中 wanxiangli 版本行；wanxiangli `src/delivery/`（新）、`src/workrecord/`（删除草稿）、`package.json`、`scripts/tests/build.test.mjs`、三份 `AGENTS.md` 文字。

## 2. A. 范围重判（看板与主线不一致）

建板时（2026-10-15）的 CUT-06 对象是三段式 WorkRecord（Opening / 已沉淀 / Recent work）。master `6eef89d1e`（2026-10-20，仓主提交 "retire Blogger, Chronicle, Y-axis, WorkRecord and Replica"）已整体删除该包与全部投影：`src/Wanxiangshu/Mission/WorkRecord/` 与 `requirements/work-record/` 不存在，`participant-horizon` WHAT 明文"不存在三段式 `WorkRecord`"，子工作终值改为 `js.deliver(view)` 交付的 TOML 视图记录。

因此本卡不复活三段式记录，分两部分交账：

1. **旧职责（三段式 WorkRecord 物化）**：REMOVED，由仓主在 `6eef89d1e` 批准并执行。核对：`git grep -i workrecord -- src` 为空；resources 中的 "work record" 仅是提示词散文对交付视图的称呼，属内容 owner，不是代码 owner。库侧 `src/workrecord/AGENTS.md` 草稿（三段式承诺）与现行产品冲突，已删除。
2. **现行继任职责（本轮抽取）**：每个会话最近一次交付/暂存视图、产生交付的调用围栏（ProviderRun, ToolCallId）、已交还材料游标。原 owner 是主仓 `JsDeliveredValues` 内两张模块级 `Dictionary`。

当前调用者（均经 `JsDeliveredValues` 公开签名，未改）：`Js/OpenCode/ToolWorkflow`、`Js/RuntimeSurface`、`Delegation/Fork/Host/RunLifecycle`、`Delegation/Fork/OpenCode/ToolSurface`、`Delegation/SyncDelegate/Runtime`、`Delegation/Advisor/Seat`、`Fission/OpenCode/{Host,Tool}`、`Session/OpenCode/HorizonTool`、`OpenCode/Tools/ToolRegistry`。

## 3. B. 契约（wanxiangli/delivery 0.9.0）

`Delivery()` 返回独立实例，消息：`stage / deliver / take / peek / peekCached / fence / returned / claim / handOff / clear / clearReturned / clearAll`。详见库 `src/delivery/AGENTS.md` 与 `index.d.ts`。

- 交付格（带围栏）与暂存格分层；暂存不覆盖交付；只有交付格回答 take/peek/fence。
- `claim(session, {mode})`：`peek` 不取走不推进；`cache` 只取走暂存格；`run` 只取走同一 providerRun 的交付格；不符合返回 null 且格不变。返回冻结的一次性凭据 `{ view }`，状态留在签发实例（WeakMap），不可伪造、不可重放、不可改向。
- `handOff(ticket, candidates, assistant)`：过滤已交还 key，按原定义（会话 + UTF-16 长度 + 前 24 码元）附带一次助手散文（仅格有视图时），取走模式推进游标。
- 选材（AnchorLedger/AnchorView）和 TOML 渲染（LlmFacing）留在主仓组合者：它们属于别的 owner，不注入库。
- 不持久化；重启丢失与原实现一致，恢复读持久交付见证。

## 4. C. 反例（先找能推翻边界的失败）

- 旧 run 的终态取走新 run 的交付 → `delivery-002`。
- 呈现（peek）被当成交还、推进游标 → `delivery-003`。
- 暂存视图降级/覆盖交付证据 → `delivery-001`。
- 同一材料或同一散文重复交还；变化材料被吞 → `delivery-003`。
- 调用方借 accessor 字段在库内重入、或伪造/重放/改向凭据 → `delivery-004`。
- 抽取后行为漂移 → 新旧双算差分（纯决策，见证据 §3）。

主线上这些行为本来就是对的（无可伪造的红），因此以正控 + 差分作证，不伪造红。

## 5. D–H 结果摘要

- D：库 `1206e57`（实现与 19 项测试）、`2564387`（release 0.9.0）。隔离构建 `SPEC[build-001] delivery` 通过。
- E：仓规已改为 submodule + `file:wanxiangli`，禁止跟踪 `.tgz`（主仓 AGENTS.md）；故不进 vendor，改记录 gitlink 与临时 `npm pack` 的干净安装验收（Node 与 Bun 导入、tsc strict 类型消费）。
- F：`JsDeliveredValues` 改为持有一个 `wanxiangli/delivery` 实例，`.fsi` 与全部调用者不变；产物 `dist/Repository/Programming/Js/DeliveredValues.js` 实际 `import { Delivery } from "wanxiangli/delivery"`。
- G：旧 owner 退出：两张模块级 `Dictionary`（delivered / returned）、`markReturned`、`withAssistant`、`proseKey`、围栏比较逻辑从主仓删除；主仓只剩选材与渲染。三段式 WorkRecord 已于 `6eef89d1e` 退出。
- H：见证据。相关回归在 base 与 candidate 上逐用例裁决一致（含 master 既有失败）；check 门禁 155 → 154（去掉本文件一处既有 match-pyramid），无新增问题。

## 6. 已知不足

- 相关回归 101 项中 9 fail / 5 cancelled 在 base 上同样出现（master 正在进行的交付语义 WIP `c4991268c`），本卡无法用这些用例证明其覆盖的路径；差分双算弥补的是 `JsDeliveredValues` 自身的决策等价。
- 本机 Node 为 v20.19.2，低于主仓 engines `>=22.13.0`；全量 `scripts/verify.mjs` 与集成 run.mjs 未跑（共享机资源与既有失败）。
- 进程内全局单例仍是"每进程一个实例"，与原实现相同；未改成按 runtime 注入，属 CUT-35 组装范围。
