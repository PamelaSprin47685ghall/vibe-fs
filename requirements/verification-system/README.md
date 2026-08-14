# verification-system

> requirement acceptance 必须由**分层、可失败、可重放的证据体系**定义，而不是测试类型或
> 人工印象。「绿」必须是可检查、可红、可重放的事实，不是一个测试命令碰巧通过。

## 这是什么包

`verification-system` 是**元合同包（META）**：它不拥有任何产品领域断言（prompt 不得泄漏
SessionId、review 何时可消费——那些归各产品包），它拥有的是「**什么证明有资格支持
Satisfied(P)**」的规则：证据怎么分层、怎么晋级、怎么保证可红、怎么保证不靠墙钟运气。

```text
README.md   ← 你在这里
WHY.md      不可替代的存在理由：为什么「绿」本身需要被治理
WHAT.md     唯一 normative 合同：12 条编号命题（VERIFICATION-SYSTEM-001..012）
HOW.md      实现模型：proof ladder / gate 机制 / 运行器；历史与弃权
PROOF.md    每条命题的测试落点
tests/      本包拥有的可执行 proof
```

## WHAT 概览（按命题组）

- **证据分层**（001–003）：五层金字塔（Static → Pure → Temporal → Adapter → Long Stroke →
  Release）、恰一个 Long Stroke、晋级阶梯禁止跨级。
- **证据资格**（004–005）：verifier 必须可红；门禁必须 fail-closed。
- **因果与时间纪律**（006–007）：静默时长判挂死、语义事件投喂 watchdog、虚拟时间、
  wall-clock 不作语义判据。
- **边界与门禁纪律**（008–009、011–012）：契约面语言边界、静态门禁命中真实路径、
  覆盖率分母完整、行数不是门禁。
- **验收判据**（010）：ratchet 只降不升，断言强度不缩水。

## HOW 概览

无 runtime 源码（META 包正确形态）。机制：

1. `tests/proof-ladder.test.mjs`：pin `package.json` 的 `format-build-test` 层序与
   `scripts/check.mjs` 的 wired gate 清单 + fail-closed 传播（Oracle 3）。
2. `tests/e2e-watchdog-feed.test.mjs`：VERIFY-004 因果 watchdog feed 门禁回归。
3. `tests/kolmogorov-size-advisory.test.mjs`：行数非门禁（advisory 不阻断）。
4. 各运行器机制（`tests/unit/run.mjs`、`tests/e2e/support/*`、`scripts/check.mjs`）由 lead
   集成时执行；本包 PROOF.md 按 REUSE 精确锚点登记。

## proof 概览

```text
node --test requirements/verification-system/tests/proof-ladder.test.mjs
node --test requirements/verification-system/tests/e2e-watchdog-feed.test.mjs
node --test requirements/verification-system/tests/kolmogorov-size-advisory.test.mjs
```

- proof-ladder 现在必须绿；它 pin 的层序/清单一旦漂移立即红。
- 每条 WHAT 命题的精确落点见 `PROOF.md`。

## 边界（DOES NOT OWN）

- 「artifact 必须含 resources」等 distribution 产品事实 → `distribution`。
- 「prompt 不得泄漏 SessionId」等具体产品事实 → 各对应包；verification 只规定如何证明。
- 当前 `tests/unit|integration|e2e` 顶级目录分类 → 当前 HOW（迁移载体）。
- 当前 One Long Stroke 的 OpenCode 具体脚本名 → 当前 HOW。
- 「谁拥有什么」→ `requirement-system`（本包消费它的 guarantee）。

## DEPENDS ON

- `requirement-system`（证据资格建立在「每 assertion 一个 owner」「WHAT 是唯一合同」之上）。
