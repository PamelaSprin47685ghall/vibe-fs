# HOW：js-semantic-surface 的实现模型与约束

非 normative。本文件解释机制怎么工作、历史怎么来的、什么被弃权。

## 实现模型

无 runtime 源码（META 包正确形态）。宪法由四类机器面承载：

### 1. 本包 tests/（`surface-charter.test.mjs`）

六条宪法中可静态判定的机器面：

```text
001: 扫描 requirements/**/tests/**/*.mjs 全部是 JS（F# 测试文件不存在）
002: 扫描完整 semantic-test dependency zone；禁止 deep dist / Fable 形状 / mangled
     export discovery，已存迁移债务只能由 baseline 单调减少，baseline 缺失仅在零债务时允许
003: SURFACE_MANIFEST 机械证明 law → owner → source → Compile Include → contract test
004: semanticImportEdges 扫描整个 corpus；有债务的 helper 不能成为新的直接测试 subject
005/006: representation 规则由 P5 `js-contract.mjs` validator 承载（assertJsData /
     assertOpaque）；compiler/build verification 才能拥有显式 quarantine
```

### 2. P2 ratchet gate（`scripts/checks/js-boundary-gate.mjs`）

只减不增的债务基线：

```text
deep dist import（语义测试 import '../../../dist/<internal>.js'）
mangled-name lookup（Object.keys / startsWith('Foo__') / endsWith('_Bar')）
Fable representation knowledge（.tag / .fields / .cases() / FSharpList / fable_modules）
legacy interop authority（member( / bind( / fableInstanceMethod( / prod( / toList( / caseOf( / payloadOf( / resultOf(）
```

现存违规进 `js-boundary-baseline.json`；baseline 可以删，不能新增或增大。`--generate` 先以
当前 baseline 比较，发现新文件或计数上升即拒绝写入；只有债务已经减少时才落盘。baseline
文件可在绝对零债务时删除，gate 会把「文件缺失但仍有债务」判红，把「文件缺失且零债务」判为
终态。

### 2a. SURFACE_MANIFEST 注册合同

`scripts/lib/test-surface-scan.mjs` 中的 `SURFACE_MANIFEST` 不是字符串免检名单。每项同时
声明 `module`、`owner`、`laws`、`source`、`representation`、`kind`；
`scripts/checks/js-surface-manifest.mjs` 逐项证明：owner 的 WHAT heading 当前存在、每条 law
有 owner PROOF 表行、source 文件存在且被 `Wanxiangshu.fsproj` 精确 Compile、至少一个
`.test.mjs` 真实 import 该 emitted surface。缺一项即无权绕过 deep-import 规则。

### 3. P5 representation validator（`tests/support/js-contract.mjs`）

```text
assertJsData(value)  递归拒绝 .cases() / FSharpList tail/head / FSharpMap runtime object /
                     Fable reflection metadata / Date 之外的一切非 JSON-shaped 值
assertOpaque(value)  只允许 create → pass back → dispose；拒绝读 fields/prototype
```

### 4. compiler/build verification quarantine（`requirements/verification-system/tests/`）

`VERIFY_008_every_emitted_module_actually_loads` 一类测试的 subject 是编译产物，有资格
知道 `dist` 与 Fable 形状。它们不是 semantic tests，不受 002/006 约束。

**quarantine 边界（TASK.md §PR 2）**：Fable quarantine 只存在于 compiler/build
verification。产品包的 `tests/support`、`tests/fixtures`、`*-contract.mjs` 不是第二
quarantine——scanner（PR 1）覆盖整个 semantic-test zone，`js-boundary-gate` baseline
对它们同样只减不增。

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

## package-local contract 冻结（TASK.md §PR 3）

以下模式进入 migration debt，现存只能减少，**禁止新增**：

```text
requirements/<package>/tests/**/*-contract.mjs
（deep-import dist / 使用 interop helpers / re-export Fable 形状）
```

`js-boundary-frozen-contracts.json` 是显式 creditor 清单；当前清单为空，因仓库没有仍受支持
的 package-local contract adapter。将来若真实迁移 creditor 必须命名并冻结路径；没有清单项的
新 `*-contract.mjs` 直接 RED。所有其它 support/fixture 文件仍由 whole-zone scanner 扫描，
不是 quarantine。

support 可以是纯 fixture（`userMessage`、`fakeClock` 之类），禁止的是「support 必须调用
production 时越过 registered surface 直连 internal dist」。

## 历史与弃权

- 六条宪法来自 Operation Clean Slate（TASK.md P0），非本仓库原创。
- `SURFACE-001..006` 历史编号在本包收编（见 WHAT 头部）；引用它们的包无需改动。
- `JS-001..020`（repository-programming HOW 的 js-tools capability 编号）不归本包。
- 本包不拥有 `domain.mjs` 的实现细节，只拥有「测试到不了 Fable mechanics」这条边。