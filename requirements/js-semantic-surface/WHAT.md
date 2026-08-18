# WHAT — js-semantic-surface

本文件是 `js-semantic-surface` 包的**唯一 normative 合同**。WHY/HOW/PROOF 非 normative。

命题编号 `JS-SEMANTIC-SURFACE-NNN`；每条命题 = 当前世界必须同时成立的事实。证据指针 →
`HOW.md` 行号。引用别的包一律用包名，不复制其它包命题。

历史编号收编：`SURFACE-001..006`（provider-language / provider-projection / finality /
participant-horizon / verification-system 的交叉引用）在本包落地为正式条款
`JS-SEMANTIC-SURFACE-001..006`，引用语义不变。

---

## JS-SEMANTIC-SURFACE-001：所有 automated tests 使用 JavaScript

**规范陈述**：`requirements/**/tests/**/*.mjs` 是 automated semantic proof 的唯一载体；语义测试及其
support、fixtures、helpers、e2e、integration 依赖区不写 F#，不引入第二测试语言。生产代码是
`.fs`；测试世界是 `.mjs`。

**含义 / 动机**：语言边界物理性阻止测试触碰实现内部（与 `verification-system` 的契约面
语言边界同源）；Fable 约定是编译器产物不是领域概念。

**边界**：本命题管「测试语言」，不管「证明技术」（`verification-system`）与「谁拥有什么」
（`requirement-system`）。编译产物验证（quarantine）测试仍可能直接消费 `dist`，见
JS-SEMANTIC-SURFACE-002 边界。

**证据指针**：→ HOW.md L13。

## JS-SEMANTIC-SURFACE-002：JS semantic tests 只能调用正式 semantic surface

**规范陈述**：语义测试只能经正式、稳定、JS-native 的 semantic surface 进入系统；禁止
deep-import 内部 `dist` 模块、禁止 mangled-name 查找（`Object.keys(mod)` /
`startsWith('Foo__')` / `endsWith('_Bar')`）、禁止消费 `fable_modules`。surface 存在是因为
一个 semantic component 拥有 contract，**从不**因为测试需要访问。

**含义 / 动机**：测试经内部路径进入 = 测试在问「F# 是怎么实现的」。surface 跟随 semantic
owner 分布，不集中成 god facade；「测试需要，所以 export internal」永不成立。

**边界**：编译产物验证测试（`VERIFY_008_every_emitted_module_actually_loads` 一类）的
subject 就是编译产物，有资格知道 `dist`；它们归 compiler/build verification quarantine，
不算 semantic tests。**quarantine 只存在于 compiler/build verification**——产品包的
`tests/support`、`tests/fixtures`、`*-contract.mjs` 不是第二 quarantine；把 forbidden knowledge
从 test 文件搬进 support 不减少债务，只是给 white-box 加一层布。`SURFACE_MANIFEST` 的
module 只有在 owner/laws/source/Compile/emitted dist/representation/kind/active WHAT-authorized
contract-test 全部闭合时才能成为正式入口；登记、死 import、无关 WHAT law 都不构成 evidence。
`domain.mjs` 已删除（退场完成）；已有 package-local `*-contract.mjs` 的 baseline 当前为空，
任何新 adapter 都直接 RED。

**证据指针**：→ HOW.md L14。

## JS-SEMANTIC-SURFACE-003：值得独立测试的 law 必须有独立 semantic owner + JS surface

**规范陈述**：一个值得独立测试的 semantic law，必须有一个明确的 semantic owner，且该
owner 必须提供 JS-native surface 承载它。surface 不是简单 forwarding：它负责
JS representation → semantic input → owner → semantic output → JS representation 的翻译，
翻译发生在 owner boundary。

**含义 / 动机**：没有 surface 的 law 无法被 JS 测试证明；把 boundary 塞进中央
`TestApi` / `DomainFacade` 会制造假 coherent ownership。surface 跟着语义 owner 分布
（`Host/Quiescence/Surface.fs`、`Participant/Provider/Attempt/Surface.fs` 形态）。

**边界**：本命题管「law → owner → surface 的归属关系」；具体 surface 文件命名
（`Surface.fs` / `Api.fs` / `Contract.fs`）是 HOW。

**证据指针**：→ HOW.md L15。

## JS-SEMANTIC-SURFACE-004：不拥有独立 law 的 helper 不直接测试

**规范陈述**：没有独立 failure meaning 的 helper、fixture、内部函数不直接作为测试 subject；
它们的行为通过 owner 的公开行为证明。测试断言公开行为而非内部协作——调用次数、辅助
布局、私有结构是「今天怎么写的」证据，不是正确性定义。

**含义 / 动机**：直接测 helper = pin HOW。内部 rename / inline / 换数据结构不要求修改
JS tests（positive canary），破坏真实 promise 必须让 JS tests 失败（negative canary）。

**边界**：本命题管「测试 subject 的选择」；helper 的具体归属与证明义务由
`requirement-system` 的 assertion 级 owner 规则承接。

**证据指针**：→ HOW.md L16。

## JS-SEMANTIC-SURFACE-005：semantic data 跨边界必须是 JS-native representation

**规范陈述**：跨 semantic surface 的普通数据只允许 JS-native 形状：`string`、`number`、
`boolean`、`null` / `undefined`、`array`、`plain object`、`Promise`、`function/callback`；
必要时允许 `bigint` 与 opaque resource handle（仅 create → pass back → dispose）。
禁止作为 semantic data 暴露：FSharpList / FSharpMap / FSharpSet / FSharpOption /
FSharpResult / F# DU instance / F# record runtime class / `tag` / `fields` / `cases()` /
Fable DateTimeOffset 编码 / curried F# function / mangled instance method。

**含义 / 动机**：JSON-shaped 数据（`JSON.stringify(result)` 理论上应工作）让测试只面对
语义；Fable runtime value 无法意外穿过新 surface。时间在边界归一为 ISO-8601 string 或
epoch milliseconds，JS 构造不了 Fable DateTimeOffset。

**边界**：opaque handle 不是 semantic data，是 capability token；测试不能 inspect 其
fields/prototype。representation 校验器（`assertJsData` / `assertOpaque`）是 HOW/P5。

**证据指针**：→ HOW.md L17。

## JS-SEMANTIC-SURFACE-006：Fable runtime representation 不属于 semantic contract

**规范陈述**：Fable 输出形状（`Module_` 前缀、DU tag ordinal、FSharpMap runtime object、
Fable reflection metadata、mangled instance method 名）不是 semantic contract 的一部分；
内部 rename / inline / 换 collection / 重排纯计算不要求修改 JS tests，破坏真实语义 promise
必须让 JS tests 失败。semantic tests 中不存在 `.tag` / `.fields` / `.cases()` /
`FSharpList` / `fable_modules` knowledge。

**含义 / 动机**：mangled name 在测试世界里应成为不存在的概念；「内部实现细节」与
「发布给测试的 contract」之间必须有一条机器可查的线。这条线的 gate 载体
（`js-boundary-gate` ratchet + `js-contract.mjs` validator）随迁移收紧，最终成为
absolute prohibition。

**边界**：本命题管「Fable 形状不是 contract」；contract 的语义内容归各产品包 WHAT。
compiler/build verification quarantine 是唯一有资格知道 Fable 的测试类（见 002 边界）。

**证据指针**：→ HOW.md L18。
