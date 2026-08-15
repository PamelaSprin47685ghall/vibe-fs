# js-semantic-surface

> 语义测试的宪法：所有 automated tests 用 JavaScript；JS semantic tests 只能调用正式
> semantic surface；语义数据跨边界必须是 JS-native representation；Fable runtime
> representation 不属于 semantic contract。

## 这是什么包

`js-semantic-surface` 是**元合同包（META）**：它不拥有任何产品领域断言（session 何时
quiesce、join 如何排序——那些归各产品包），它拥有的是「**语义测试世界与 Fable 实现世界
之间的边界**」的规则：什么算正式 surface、什么数据形状可以穿过边界、什么权力测试永远
拿不到。

本包是 Operation Clean Slate（Refactor Closure）的宪法层（P0）。它把仓库既有实践
（`.mjs` 测试、`domain/` 反腐蚀边界、`guide-contract.test.mjs` 的 surface pin、architecture
gate 的 absence ratchet）升格为编号命题，使「测试只能问 semantic component 承诺什么」
成为可检查的机器事实，而不是约定俗成。

```text
README.md   ← 你在这里
WHY.md      不可替代的存在理由：为什么测试世界必须与 Fable 世界隔离
WHAT.md     唯一 normative 合同：JS-SEMANTIC-SURFACE-001..006
HOW.md      实现模型：surface 归属 / representation 翻译 / 迁移路径；历史与弃权
PROOF.md    每条命题的测试落点
tests/      本包拥有的可执行 proof
```

## WHAT 概览（按命题组）

- **测试语言与边界**（001–002）：所有 automated tests 是 JS；JS semantic tests 只经正式
  semantic surface 进入，永不经内部 dist 路径或 Fable mechanics。
- **law 与 owner**（003–004）：独立 law 必须由独立 semantic owner 持有 JS surface；
  无独立 law 的 helper 不直接测试。
- **representation**（005–006）：semantic data 跨边界必须 JS-native；Fable runtime
  representation（`tag`/`fields`/`cases()`/FSharpList/DU/DateTimeOffset 编码）不属于
  semantic contract，也不得作为测试输入。

## HOW 概览

无 runtime 源码（META 包正确形态）。机制由本包 PROOF 锚点与后续 gate
（`js-boundary-gate`、`js-contract.mjs` validator）承载：

1. `tests/surface-charter.test.mjs`：六条宪法中可静态判定的机器面（测试语言、正式 surface
   契约测试、representation 禁止清单）。
2. P2 `js-boundary-gate`（只减不增 ratchet）：deep dist import / mangled-name lookup /
   Fable representation knowledge 的债务基线，随迁移单调下降。
3. P5 `js-contract.mjs`（`assertJsData` / `assertOpaque`）：统一 JS-native validator，
   Fable runtime value 无法意外穿过新 surface。

## 边界（DOES NOT OWN）

- 「怎么证明、如何可红」→ `verification-system`（本包消费其 guarantee）。
- 「每 assertion 一个 owner」「WHAT 是唯一合同」→ `requirement-system`。
- 任一产品语义（session/join/finality 等）→ 各产品包；本包只规定测试如何到达它们。
- `domain.mjs` / `domain/interop.mjs` 的具体实现 → 当前 HOW（迁移载体），退场路径见本包 HOW。

## DEPENDS ON

- `requirement-system`（surface 必须跟着 semantic owner 分布；「测试需要，所以 export
  internal」永不成立的前提是 assertion 级 owner 规则）。
- `verification-system`（本包宪法是可红、fail-closed 的证明规则；proof ladder 与 wired
  gate 机制由 verification-system 治理）。
