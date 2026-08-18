# HOW：js-semantic-surface 的实现模型与约束

非 normative。本文件解释机制怎么工作、历史怎么来的、什么被弃权。

## 实现模型

无 runtime 源码（META 包正确形态）。宪法由四类机器面承载：

### 1. 本包 tests/（`surface-charter.test.mjs`）

六条宪法中可静态判定的机器面：

```text
001: 扫描 requirements/**/tests/**/*.mjs 全部是 JS（F# 测试文件不存在）
002: 扫描完整 semantic-test dependency zone（.mjs/.js，包括 support、fixture、helper、e2e、integration）；禁止 deep dist / Fable 形状 / mangled
     export discovery，已存迁移债务只能由 baseline 单调减少，baseline 缺失仅在零债务时允许
003: SURFACE_MANIFEST 机械证明 law → owner → source → Compile Include → emitted dist module → active executable contract test；该测试必须以 WHAT law 直接授权 surface，登记或死 import 不构成证据
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
终态。零债务下 `--generate` 会删除 stale/empty ledger；正常运行只接受已删除的 ledger，或
仅含 `BUILD_VERIFICATION_FILES` 明确 quarantine 的 ledger。当前仓库为零债务、ledger absent。

### 2a. SURFACE_MANIFEST 注册合同

`scripts/lib/test-surface-scan.mjs` 中的 `SURFACE_MANIFEST` 不是字符串免检名单。每项同时
声明 `module`、`owner`、`laws`、`source`、`representation`、`kind`；
`scripts/checks/js-surface-manifest.mjs` 逐项证明：owner 的 WHAT heading 当前存在、每条 law
有 owner PROOF 表行、source 文件存在且被 `Wanxiangshu.fsproj` 精确 Compile、对应 `dist/`
产物存在、至少一个 `.test.mjs` 真正使用该 emitted surface，且该测试的 WHAT law 直接包含在
manifest 授权 law 中。缺一项即无权绕过 deep-import 规则；登记、死 import、无关 law 均不是证据。

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

`requirements/verification-system/tests/support/domain.mjs` 曾是大量测试的
anti-corruption boundary，Fable mechanics 在 `domain/interop.mjs`。退场已完成：文件已删除，
semantic tests 现在直接消费 registered owner surface，不再经中央 facade。退场分四步
（历史记录）：

```text
1. 冻结：no new imports from domain.mjs（P2 gate 的 baseline 只减不增）
2. 每迁一个 family（identity/journal/context/execution/orchestrator/...），减少其 exports
3. 普通 semantic tests 不再依赖 representation helpers 时，删除 bind/member/
   fableInstanceMethod/unionCase/prod
4. 最后删除普通测试可见的 caseOf/payloadOf/toList/listItems/mapEntries/resultOf/unwrapOption
```

删除 helpers 不因为它们写得不好——它们成功完成了迁移任务，普通测试已到不了危险区域。

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
- 本包不拥有已删除的 `domain.mjs` 的历史实现细节，只拥有「测试到不了 Fable mechanics」这条边。

## DEPENDS ON

- `requirement-system`（surface 必须跟着 semantic owner 分布；「测试需要，所以 export
  internal」永不成立的前提是 assertion 级 owner 规则）。
- `verification-system`（本包宪法是可红、fail-closed 的证明规则；proof ladder 与 wired
  gate 机制由 verification-system 治理）。

## 验证与测试落点

落点类型：`NEW`（本包 tests/）/ `GATE`（静态门禁，`node scripts/check.mjs` 集成执行）/
`PENDING`（由后续 P 阶段 gate 落地，见 HOW）。运行命令均为仓库根目录相对。每条 WHAT 命题恰一行。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| JS-SEMANTIC-SURFACE-001 | `requirements/js-semantic-surface/tests/surface-charter.test.mjs`（test: WHAT[JS-SEMANTIC-SURFACE-001] JS_SURFACE_001_all_semantic_tests_are_mjs） | NEW | node --test requirements/js-semantic-surface/tests/surface-charter.test.mjs |
| JS-SEMANTIC-SURFACE-002 | `surface-charter.test.mjs`（tests: WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002_forbidden_patterns_absent_from_semantic_tests、JS_SURFACE_002c_whole_semantic_test_zone_is_scanned、JS_SURFACE_002d_zero-debt_generate_removes_empty_ledger、JS_SURFACE_002e_build-verification_ledger_exemption_survives_zero-debt_cleanup、JS_SURFACE_002b_registered_surfaces_exist_in_the_production_source_tree）；`scripts/checks/js-boundary-gate.mjs`（whole-zone `.mjs`/`.js` ratchet, no silent regeneration, zero-debt ledger terminal） | NEW + GATE | node --test ... / node scripts/check.mjs |
| JS-SEMANTIC-SURFACE-003 | `surface-charter.test.mjs`（tests: WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003_law_owner_surface_registry、JS_SURFACE_003_every_registered_surface_has_a_contract_test）；`scripts/checks/js-surface-manifest.mjs`（owner WHAT/PROOF/source/Compile/emitted dist/active WHAT-authorized import evidence） | NEW + GATE | node --test ... / node scripts/check.mjs |
| JS-SEMANTIC-SURFACE-004 | `surface-charter.test.mjs`（test: WHAT[JS-SEMANTIC-SURFACE-004] JS_SURFACE_004_helper_not_directly_tested）；`semanticImportEdges` scans every package test dependency, including support/fixtures/e2e/integration | NEW + GATE | node --test requirements/js-semantic-surface/tests/surface-charter.test.mjs |
| JS-SEMANTIC-SURFACE-005 | P5 `requirements/verification-system/tests/support/js-contract.mjs`（`assertJsData` / `assertOpaque` validator）+ `surface-charter.test.mjs`（test: WHAT[JS-SEMANTIC-SURFACE-005] JS_SURFACE_005_js_native_representation_rules） | NEW | node --test requirements/js-semantic-surface/tests/surface-charter.test.mjs |
| JS-SEMANTIC-SURFACE-006 | `surface-charter.test.mjs`（test: WHAT[JS-SEMANTIC-SURFACE-006] JS_SURFACE_006_fable_representation_not_contract）；P2 gate scans Fable tokens and permits an absent baseline only at zero; absent `domain.meta` is a terminal cleanup, not a required file | NEW + GATE | node --test ... / node scripts/check.mjs |

### 语义 anchor

无 anchor id（META 包）；机器事实由 surface-charter + js-boundary-gate + js-contract 承担。

### 人工评审承接表

- 新增 semantic surface 但无 contract test pin 名字 → JS-SEMANTIC-SURFACE-003
- 「测试需要」成为 export internal 的理由 → JS-SEMANTIC-SURFACE-002
- surface 翻译在 owner boundary 之外（中央 god facade） → JS-SEMANTIC-SURFACE-003
- Fable 升级破坏 semantic tests（quarantine 外） → JS-SEMANTIC-SURFACE-006
