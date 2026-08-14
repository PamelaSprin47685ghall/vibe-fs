# provider-projection

**一句话 WHY**：已决定可见的 typed semantic intent，必须经唯一、确定性投影变成 provider
representation；representation 绝不能反向创造 authority 或 state。

## 这个包保证什么

- 投影是**代数**：typed intent + 纯管线（Planner/Renderer），无 AST + Interpreter；功能
  模块不得直接改 `Message list`。
- **确定性**：同一 semantic intent 集无论装配顺序，产出同一投影世界或同一显式冲突；
  同输入同 bytes。
- **Semantic ≠ Wire**：不同型、禁隐式互转；canonical digest 只从 Semantic 投影算，
  禁止反解析 wire/TOML。
- **表示不反造权威**：synthetic role 不产生 HumanRoot/Opening/completion；SyntheticToml
  故意没有 parser，业务不得把渲染文本读回控制流。
- **instruction/data plane**：comment vs data 由投影 owner 对接收 agent 的消费语义决定，
  不由来源可信度或历史性决定。

## WHAT 概览（12 条命题）

`WHAT.md` 编号 `PROVIDER-PROJECTION-001..012`：投影是代数（001）、不可变快照（002）、
Semantic/Wire 分型（003）、三层结构（004）、只声明 intent（005）、canonical order +
显式冲突（006）、DSL 不负责生命周期（007）、SyntheticToml 唯一 owner + 无 parser（008）、
instruction/data plane 判据（009）、表示不反造权威（010）、semantic ≠ wire equality +
digest 从 Semantic 算（011）、确定性 renderer（012）。

## HOW 概览

类型 `Domain/{ProjectionIntent,ProjectionPlanner,ProjectionRenderer,ProviderProjection,
SyntheticToml}.fs`；Y 帧形状源 `Domain/CompanionProjectionBuilder.fs`；host wire 适配
`Infrastructure/OpenCode/Codec/*`、`Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.fs`；
值树结果面证据 `archive/changes/completed/js-tools-toml-result.md`；plane 判据
`archive/changes/completed/corrective.md` §1。

## Proof 概览

- MOVE：`tests/synthetic-toml.test.mjs`（ARCH-010 字符串规则/值树/布局/确定性 + NEW
  `ARCH_011_renderer_exposes_no_parser`）。
- REUSE：`requirements/provider-projection/tests/projection-algebra.test.mjs`（algebra oracle，`SPLIT@cutover`
  拆 feature 语义）、`requirements/context-compression/tests/companion-projection.test.mjs`
  （COMPANION_007 digest，cutover 归本包）、`requirements/provider-projection/tests/join-result-renderer-entry-comment.test.mjs`、
  `requirements/provider-projection/tests/blogger-toml.test.mjs`、`requirements/prefix-stability/tests/pair-thought-anchored.test.mjs`。

## 阅读顺序（零上下文读者）

1. `WHY.md` —— 为什么必须独立存在、RED 长什么样、历史病灶。
2. `WHAT.md` —— 12 条命题（唯一 normative）。
3. `HOW.md` —— 三层装配 / intent 排序冲突 / 两投影 / digest / SyntheticToml 怎么落地。
4. `PROOF.md` —— 每条命题的测试落点与怎么跑。

## 运行

```text
node --test requirements/provider-projection/tests/synthetic-toml.test.mjs
node --test requirements/provider-projection/tests/projection-algebra.test.mjs
```

## DEPENDS ON

- `participant-horizon`：投影输入是已获准进入 experience 的最小事实集。
- `provider-language`：投影产出已本地化 prose 的 representation（理由见 `WHY.md`）。
