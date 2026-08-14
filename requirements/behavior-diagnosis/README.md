# behavior-diagnosis

## 一句话 WHY

工程行为问题不能因为某个词或一次失败就自动成立；diagnosis 必须有明确 trigger、
negative evidence 与 distinction，且每一次 diagnosis 是一次有独立身份的语义事件
（occurrence），不因历史压缩或重复告知而新生。

## WHAT 概览（唯一 normative 合同见 `WHAT.md`）

- **检测边界**：规则实例唯一真相是 `resources/enforcer/<TipName>/` 目录（目录名 =
  TipName = provider enum = durable RuleId），无 `catalog.json` 第二真相，装载
  fail-fast、无代码内 fallback。
- **tip 身份与枚举**：合法 tip 只能是目录 TipName 枚举的精确命中；未知/缺失/拼写
  近似一律失败，不 fuzzy、不默认、不复活 score path。
- **Cycle 归并**：一次 provider run 的多个 `chronicle` 调用按 PartOrdinal 确定性
  归并成单 cycle；canonical tip = PartOrdinal 最早；身份不足（重复 ToolCallId /
  空 messageId）fail closed；大小/数量越界 fail closed。
- **原子 occurrence**：`BlogObservationCommitted` 一个事实同时推进 frame +
  coverage + 单一 TipRuleId；不存在独立 `EnforcementCycleCommitted`。
- **Observation 配对**：tip 与 frame 是同一个不可拆观察的两半，前向 zip 成
  ObservationUnit；禁止 tips∥frames 两路平行流。
- **历史压缩不造事件**：squash 只是历史表示变换（K→1、tip co-truncate），不创造
  新 TipOccurrence、不触发新交付。

**不归本包**：诊断如何/何时展示给 Main、feedback dedupe/coverage、chronicle 工具
名与权限、tip 目录的物理格式、score vector/ordinal（全部见 `WHAT.md` 各命题边界
与 `16-feedback.md` DOES NOT OWN）。

## HOW 概览（实现模型见 `HOW.md`）

| 层 | 位置 | 职责 |
|---|---|---|
| Domain 校验 | `src/Wanxiangshu/Domain/{EnforcerCatalog,EnforcerCodec,EnforcerCycle,RulebookObservation}.fs` | 规则校验、codec、归并、配对（纯函数） |
| 资源装载 | `src/Wanxiangshu/Infrastructure/Resources/EnforcerCatalogResource.fs` | 扫描目录、fail-fast、合成 Blogger system |
| Cycle 提交 | `src/Wanxiangshu/Session/{EnforcerHost,EnforcerCycleCommit,EnforcerFrameRecovery,EnforcerContinuation,EnforcerCycleDecode}.fs` | 身份/边界校验、原子提交、恢复 |
| 投影 | `src/Wanxiangshu/Journal/{EnforcementProjection,ObservationProjection}.fs` | RecentTips 有界 8、Observation 配对视图 |

## Proof 概览（落点表见 `PROOF.md`）

- 本包自有测试：`requirements/behavior-diagnosis/tests/`（10 文件，全部 `node --test`
  单跑绿）。
- 交叉证明（REUSE）：`tests/unit/enforcer/{tip-v2-contract,enforcer-cycle-protocol,
  blogger-convergence-gaps,paired-history-eval}.test.mjs` 中本包断言锚点；
  `requirements/behavior-diagnosis/tests/integration/resources/enforcer-rulebook.test.mjs`（打包资源）。
- 本包无 semantic-anchors.mjs anchor id（该 catalog 只有 ROLE/TOOL/OFFICE 三组，
  与本包无关）。

## 阅读顺序（保姆级导航）

1. `WHY.md` —— 为什么必须有一个包保证「诊断不靠模糊词成立」；历史上 RED 是什么样。
2. `WHAT.md` —— 当前世界必须同时成立的编号命题（唯一 normative 合同）。
3. `HOW.md` —— 每个命题在 `src/` 的精确实现模型 + 失败模式 + 历史与弃权。
4. `PROOF.md` —— 每条命题 → 具体测试文件 + 断言锚点 + 运行命令；红了说明什么。
5. `tests/` —— 可执行 proof 本体。
