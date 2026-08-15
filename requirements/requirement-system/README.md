# requirement-system

> 当前接受的产品真理必须有**唯一 semantic owner**、显式 dependency 与唯一 proof
> ownership；否则 docs、gate、test、change 会形成互相覆盖的平行法域。

## 这是什么包

`requirement-system` 是**元合同包（META）**：它不拥有任何产品领域事实（prompt 怎么写、
journal 怎么存、review 怎么判都不归它），它拥有的是「这些事实如何被组织、被拥有、被验收」
的治理规则本身。

```text
README.md   ← 你在这里
WHY.md      不可替代的存在理由：为什么必须有一个包管「谁拥有什么」
WHAT.md     唯一 normative 合同：16+1 条编号命题（REQUIREMENT-SYSTEM-001..017）
HOW.md      实现模型：meta-verifier + spec gate + change-lifecycle；历史与弃权
PROOF.md    每条命题的测试落点
tests/      本包拥有的可执行 proof
```

## WHAT 概览（按命题组）

- **所有权元规则**（001–005）：唯一 semantic owner、包身份独立于物理布局、全部包同时为真、
  每个 executable proof 恰一个 owner、无裸规范权威。
- **树结构合同**（006–007、016）：48 个包 × 5 份文档、无 INDEX 外目录、WHAT 是唯一
  normative 合同、依赖声明 ⊆ INDEX 依赖骨架。
- **条款治理**（008–009）：Clause ID 唯一且稳定、条款按层归属（行为→what、所有权→shape、
  算法→how、证明→proof、理由→why）。
- **变更生命周期**（010–015）：机制已停用（2026-08-14 归档）；废止路径不引用、
  用户所有权与启动授权（`proposals/`）、单文件 Change（重启时恢复）、Active/Completed
  合同、矛盾 blocker 协议、直接闭环小变更。
- **机器 verifier**（017）：meta-verifier 扫描 requirements/ 全树，把上面的结构事实变成
  可红测试。

## HOW 概览

本包无 runtime 源码（META 包的正确形态）。机制由四部分组成：

1. `requirements/requirement-system/tests/meta-verifier.test.mjs`：扫描 requirements/ 全树，
   断言 5 文档齐备、WHAT→PROOF 交叉、落点文件存在、无 INDEX 外目录、DEPENDS ON ⊆ 骨架。
2. `scripts/checks/spec.mjs` + `scripts/checks/spec-rules.mjs`：requirements/ 树治理门
   （定义只在 WHAT.md、引用可解析、全仓零归档树引用、废止路径、链接完整性；
   spec-rules 的纯规则回归在 `tests/spec-rules.test.mjs`）。
3. `requirements/README.md`：48 包树入口导航（2026-08-14 cutover 后承担导航职责）。
4. `tests/change-lifecycle.test.mjs`：WHAT-013/014/015 机器面（Completed 不作当前依据 pin +
   blocker 四步 + AGENTS 小修复豁免；Active 原文冻结仍人工）。

## proof 概览

```text
node --test requirements/requirement-system/tests/meta-verifier.test.mjs
node --test requirements/requirement-system/tests/spec-rules.test.mjs
node --test requirements/requirement-system/tests/change-lifecycle.test.mjs
```

- meta-verifier 迁移中途红是预期（当前 48 包若有未落地包）；两个 META 包自身的结构检查必须绿。
- 每条 WHAT 命题的精确落点见 `PROOF.md`。

## 边界（DOES NOT OWN）

- 什么证据技术足够证明某个产品事实 → `verification-system`。
- 任一产品领域事实（prompt/journal/review/host/…）→ 各对应包。
- Git/PR 历史沉积、Proposal 生命周期本身；未来 requirements/ 树只表达当前接受真理。
- 当前 Clause ID 前缀表、旧五层 docs 文件层级 → 当前 HOW（历史载体，2026-08-14 归档）。

## DEPENDS ON

- 无（本包不消费任何其它包的 guarantee）。
