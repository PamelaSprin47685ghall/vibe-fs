# speculative-investigation — 可安全丢弃的调查投机

> 系统可以用更便宜的执行先猜 primary 接下来需要哪些只读调查；但 speculation 只有在未被
> primary 消费前对 authoritative world **零影响**时才安全。本包保证「投机可以便宜，但永远
> 不能污染真实历史、不能成为正确性前提」。

## 一句话 WHY

**可丢弃 speculation 只有在 authoritative world 零影响时才可换取调查成本下降。**

一个昂贵的主模型在局部窗口里反复做出机械只读调查（read/glob/grep）。用一个便宜的
Replica 先猜、先查，可以省成本；但代价是引入一次「干预」。干预未被主模型消费时，它
**不是历史**；被消费后，它**就是真实因果历史**，重启后不能消失。本包定义这条
Candidate → Promotion 边界，以及让投机永远不改变「没有 Strength 时世界会怎样」的全部约束。

## WHAT 概览（13 条命题，见 WHAT.md）

| # | 命题 | 一句话 |
|---|---|---|
| SPEC-INV-001 | 零影响基线 | disabled/K0 时普通 Work Session 与没有 Strength 时完全相同 |
| SPEC-INV-002 | Eligible opportunity | 只有一组窄条件同时成立才允许投机；任一未知 → K0 |
| SPEC-INV-003 | 预算单位 K | K ∈ {0,1,2}，单位是 provider request，K+1 物理停止 |
| SPEC-INV-004 | Replica authority | 同 role fast peer + 更窄工具集；不新增身份、不换人/世界语 |
| SPEC-INV-005 | Candidate frame | 只保留真实 Host 工具交换的 canonical 形态 |
| SPEC-INV-006 | Prepared ≠ 历史 | 未消费 Candidate 不进入任何 canonical/context 历史 |
| SPEC-INV-007 | Promotion 只由消费证据 | 只有该 run 的真实 provider output 才能晋升 |
| SPEC-INV-008 | Replay 与 XTrace closure | Promoted 历史可重放、最终进入语义 timeline |
| SPEC-INV-009 | Projection 与 no-reflection | 跨 Session 只比语义投影；Replica 不读自己旧产物 |
| SPEC-INV-010 | Predictor 与 control | 干预数据永不冒充「无干预时 primary 会怎样」的 label |
| SPEC-INV-011 | 失败、取消与熔断 | 普通失败 fail-open K0；durable 歧义 fail-closed |
| SPEC-INV-012 | 模型不可见、系统可审计 | 机制 provenance 不进模型字节，诊断事实保留 |
| SPEC-INV-013 | DryRun visible nonblocking shadow | 真 Replica 在 OpenCode 可见，但 owner 不等待且结果零 Promotion |

## HOW 概览（见 HOW.md）

实现落在 `src/Wanxiangshu/{Domain,Application,Session,Infrastructure}` 的 `Strength*` 模块。
两段式主 transform：`StrengthReplay`（重放已 Promoted 帧）→ `StrengthSpeculate`。Treatment 可为当前
TargetProviderRun 准备 Candidate；显式 DryRun 只启动真实、OpenCode 可见的 attached Replica 后立即让 owner
继续，不等待 terminal/deadline，也不映射结果。durable 事实只有四条事件
`StrengthCandidatePrepared / Promoted / FramesTraced / CandidateAbandoned`，大 material
只经 EventStore `payload_refs`。默认 **Shadow/K0**；K1/K2 treatment 永不因架构闭环而启用。

## Proof 概览（见 PROOF.md）

- 本包自有测试 `tests/`：4 个文件（`authority-policy`、`commit-promotion`、`host-policy`、
  `turn-evidence`），单跑命令 `node --test requirements/speculative-investigation/tests/<file>`。
- 12 个 strength 测试文件已迁入本包 `tests/`（Wave 2a：MOVE/SPLIT，fable 直连 import 全部改写为 support 等价调用）；`requirements/speculative-investigation/tests/integration/strength/lifecycle.test.mjs` 为集成面。
- 交叉边界：`unpromoted ≠ history` 由本包测试证明，同时是 `semantic-trace` 的 cross-boundary
  invariant（见 HANDOFF §18.6）；本包只交叉引用，不复制其命题。

## DEPENDS ON

`repository-investigation`、`participant-identity`、`participant-horizon`、
`provider-projection`、`semantic-trace`（依赖骨架唯一来源：`requirements/INDEX.md`）。
逐条理由见 HOW.md「依赖」。

## 阅读顺序

1. `WHY.md` —— 为什么只投机只读调查、为什么 Candidate 不能直接成为历史、被拒方向。
2. `WHAT.md` —— 唯一 normative 合同：13 条编号命题 + 每条边界。
3. `HOW.md` —— 实现模型：模块地图、决策管线、崩溃矩阵、历史与弃权。
4. `PROOF.md` —— 每条命题的测试落点表、anchor id、cutover 待办。

## RED 判定

世界 RED 当且仅当：**未被 primary 使用的 speculative intervention 能污染 authoritative
history/authority，或优化关闭后产品 correctness 改变。** 对应 WHAT 命题的失败模式见
WHY.md「失败模式」与 PROOF.md 各落点。
