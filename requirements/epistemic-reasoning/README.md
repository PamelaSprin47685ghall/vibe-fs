# epistemic-reasoning — 认识状态治理

> 重复生成文本不能自动增加知识。一个 reasoning system 必须显式区分 proposal 与 evidence、
> 来源依赖与不确定性，并让下一信息动作/停止由认识状态 controller 决定，而不是 transcript
> eloquence。本包保证「No Free Information」与 controller-owned closure。

## 一句话 WHY

**生成不增知识；proposal ≠ evidence。** 认识状态是当前问题的 sufficient state（不是
transcript/search tree）；谁有资格产生 Evidence、谁决定下一步与停止，由认识状态 controller
治理。当前实现名（Sphinx、A*/Bayes/MCTS、MCP handle）全部是 HOW，可整体替换而本包命题不变。

## WHAT 概览（12 条命题，见 WHAT.md）

| # | 命题 | 一句话 |
|---|---|---|
| EPI-001 | 认识状态是 sufficient state | transcript/问题树/搜索树不是状态本体 |
| EPI-002 | Kernel 拥有 continuation/closure/停止 | 生成模型不能自选下一步或自封 answered |
| EPI-003 | 权威状态显式拥有认识基底 | RootContract/Findings/Evidence/Hypotheses/Dependencies/Actions/Budget/PendingRequest |
| EPI-004 | Pending Request 契约 | observation 必须与 Kernel 当前请求同型；错型不前进 |
| EPI-005 | Proposal ≠ Evidence（No Free Information） | 生成/重述/递归不增加 evidence mass |
| EPI-006 | Evidence 带 source/dependency | 同源重复不伪装独立支持；同 semantic 独立来源并存 |
| EPI-007 | RootContract 保留分布 | 不 argmax、不 bind-once；控制更新不增 Evidence |
| EPI-008 | action value 相对根问题 | 信息增益 + gateway value + cost/risk；stop 同空间比较 |
| EPI-009 | 概率只接受合格数值证据 | 否则保持 qualitative uncertainty，不伪造 posterior |
| EPI-010 | 经典算法是可验证退化 | A*/Bayes/MCTS 是 solver embedding，不是 ontology |
| EPI-011 | 等价约简 dependency-aware | wire 无判重权；类内 Pareto，不删独立来源价值 |
| EPI-012 | closure 幂等且全局 | 重复纯计算不凭空制造 Evidence/独立依赖组 |

## HOW 概览（见 HOW.md）

实现落在 `src/Wanxiangshu/Sphinx/*.fs`（namespace `Wanxiangshu.Sphinx.*`），Fable 编译到
`dist/Sphinx/*.js`，生产 MCP 入口 `dist/Sphinx/McpServer.js`。主循环：
`start → YIELD SemanticAssessmentRequest → resume → PendingRequest 校验 → Closure.absorbAndClose
→ fixed point → Policy.decide → YIELD | ANSWER`。MCP SDK/zod 只停在 `McpServer.fs`；raw JS
shape 只停在 codec 边界；`Session.fs` 是唯一 handle 索引。

## Proof 概览（见 PROOF.md）

- 本包自有测试 `tests/`：8 个文件（`kernel`、`semantics`、`mcp-handle`、`bayes`、`search`、
  `mcts`、`represent`、`methodology`）+ helper `support.mjs`，单跑
  `node --test requirements/epistemic-reasoning/tests/<file>`。
- REUSE：`requirements/epistemic-reasoning/tests/sphinx-mcp-kernel.test.mjs`（Sphinx MCP 身份/注入/权限，SPLIT 家族，
  cutover 拆分）、`requirements/verification-system/tests/support/sphinx-mcp-fixture.js`。
- anchor id：`semantic-anchors.mjs` 的 `inquiry` 组 7 个 id 归本包（见 PROOF.md）。

## DEPENDS ON

`participant-horizon`（依赖骨架唯一来源：`requirements/INDEX.md`）。新世界事实通过
evidence-acquisition contracts 注入为 observation；具体是 repository、external 还是其它来源，
不构成 epistemic core 的 hard dependency（逐条理由见 HOW.md「依赖」）。

## 阅读顺序

1. `WHY.md` —— 为什么是认识状态、为什么 Proposal 与 Evidence 必须分槽、被拒方案。
2. `WHAT.md` —— 唯一 normative 合同：12 条编号命题 + 每条边界。
3. `HOW.md` —— 实现模型：模块地图、主循环、absorb 规则、历史与弃权。
4. `PROOF.md` —— 每条命题的测试落点表、anchor id、cutover 待办。

## RED 判定

世界 RED 当且仅当：**模型可以通过重复思考提高「证据」，把同源材料当独立 likelihood，
或绕过 controller 自己宣布认识闭包。** 对应 WHAT 命题的失败模式见 WHY.md「失败模式」。
