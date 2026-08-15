# HOW：js-semantic-surface 的实现模型与约束

非 normative。本文件解释机制怎么工作、历史怎么来的、什么被弃权。

## 实现模型

无 runtime 源码（META 包正确形态）。宪法由四类机器面承载：

### 1. 本包 tests/（`surface-charter.test.mjs`）

六条宪法中可静态判定的机器面：

```text
001: 扫描 requirements/**/tests/**/*.test.mjs 全部是 .mjs（F# 测试文件不存在）
002: 契约面测试存在（guide-contract 机制）；FORBIDDEN pattern 扫描（.tag/.fields/.cases()
     /fable_modules/deep dist import）在语义测试中归零
003: 本包自身即示范：law → owner → surface 的归属文档化（PROOF 表）
004: positive/negative canary 概念由本包 PROOF 落点 + verification-system 承接
005/006: representation 规则由 P5 `js-contract.mjs` validator 承载（assertJsData /
     assertOpaque），本包 tests 只做规则存在性与禁止清单 pin
```

### 2. P2 ratchet gate（`scripts/checks/js-boundary-gate.mjs`）

只减不增的债务基线：

```text
deep dist import（语义测试 import '../../../dist/<internal>.js'）
mangled-name lookup（Object.keys / startsWith('Foo__') / endsWith('_Bar')）
Fable representation knowledge（.tag / .fields / .cases() / FSharpList / fable_modules）
legacy interop authority（member( / bind( / fableInstanceMethod( / prod( / toList( / caseOf( / payloadOf( / resultOf(）
```

现存违规进 `legacy-js-boundary-debt.json` baseline；`baseline 可以删，不可以加`。
每迁一个测试删一个 entry；P11 归零后删 baseline 与 gate 本身。

### 3. P5 representation validator（`tests/support/js-contract.mjs`）

```text
assertJsData(value)  递归拒绝 .cases() / FSharpList tail/head / FSharpMap runtime object /
                     Fable reflection metadata / Date 之外的一切非 JSON-shaped 值
assertOpaque(value)  只允许 create → pass back → dispose；拒绝读 fields/prototype
```

### 4. compiler/build verification quarantine（`requirements/verification-system/tests/`）

`VERIFY_008_every_emitted_module_actually_loads` 一类测试的 subject 是编译产物，有资格
知道 `dist` 与 Fable 形状。它们不是 semantic tests，不受 002/006 约束。

## domain.mjs 退场

当前 `requirements/verification-system/tests/support/domain.mjs` 是大量测试的
anti-corruption boundary，Fable mechanics 在 `domain/interop.mjs`。退场分四步：

```text
1. 冻结：no new imports from domain.mjs（P2 gate 的 baseline 只减不增）
2. 每迁一个 family（identity/journal/context/execution/orchestrator/...），减少其 exports
3. 普通 semantic tests 不再依赖 representation helpers 时，删除 bind/member/
   fableInstanceMethod/unionCase/prod
4. 最后删除普通测试可见的 caseOf/payloadOf/toList/listItems/mapEntries/resultOf/unwrapOption
```

删除 helpers 不因为它们写得不好——它们成功完成了迁移任务，以后普通测试已到不了危险区域。

## 历史与弃权

- 六条宪法来自 Operation Clean Slate（TASK.md P0），非本仓库原创。
- `SURFACE-001..006` 历史编号在本包收编（见 WHAT 头部）；引用它们的包无需改动。
- `JS-001..020`（repository-programming HOW 的 js-tools capability 编号）不归本包。
- 本包不拥有 `domain.mjs` 的实现细节，只拥有「测试到不了 Fable mechanics」这条边。
