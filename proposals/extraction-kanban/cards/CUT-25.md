# CUT-25 受限 JS 编程面与执行组合

## 接手核对（2026-10-21，eng-cut25b-goose）

- **Owner**：eng-cut25b-goose（board claim `eb172e465`）。
- **(a) 逃逸回归**：`evidence/CUT-25/verification-2026-10-21.md`。`006-cut25-escapes` 前 6 条在 `f37a7ba48` dist-base 全红、当前全绿；WeakMap 两条在 0.23.1 红、0.23.2 绿。
- **(b) await 后死循环**：已验证，~200ms 内 `PROGRAM_TIMEOUT`（子进程）。
- **本轮合入**：库 0.23.2（`7b06b93`）；主仓 `f6749e0a2`、`dc3096154`。
- **仍未 DONE**：能力面 / generator / Plan 接线、教程/描述/schema 同源依赖 CUT-08 收尾；不把本卡标 DONE。


## 状态（2026-10-21，eng-cut20b-goose）

认领了子范围①「沙箱真实加载与拒绝逃逸」（不依赖 CUT-08 的能力投影），先做了反例调查，没有动 `src/`，
没有完成抽取。认领已释放，下一位接手人直接从下面的发现开始。

## 发现：[006] 沙箱隔离在 master 上不成立

`requirements/repository-programming/WHAT.md` [006] 规定用户程序“不直接获得文件系统、网络、进程或环境变量”。
`Process/JsSandbox.fs` 把 node:vm context 当作权限边界，但 context 里放的是宿主 realm 的对象，所以边界可以被穿透。

复现：`evidence/CUT-25/probe-escapes.mjs`，对 master dist 运行
`node probe-escapes.mjs <repo>/dist`，结果在 `evidence/CUT-25/base-red-escapes.json`。
JsSandbox / ToolsBindings / RuntimeSurface 在 6859577a7 与本次提交的 base 之间没有改动，所以结果适用于当前 master。

模型程序通过以下途径都拿到了宿主 `process`（`pid` 为 number，`getBuiltinModule` 为 function）：

- 全局 `api`：`api.constructor.constructor('return process')()`
- 全局 `__deadlineRef`：同上
- `globalThis.constructor.constructor`：createContext 的被包装对象是宿主对象
- 合法 API 的返回值：`(await this.ls('.')).constructor.constructor('return process')()`

最后一项最要紧：不需要碰任何内部名字，只用公开的 `this.ls` 返回值就能逃逸。
探针用它在 workspace 之外写了一个文件，写入成功（`file written outside workspace: true`）。

没有逃逸的两项：JsProgram 基类自己 `new Error` 构造的失败对象，以及经 `__wrap` 代理返回的箭头函数，
二者都属于 vm realm。

## 修复方向（未实现，供接手人判断）

- 程序与 api 之间需要一层在 vm realm 内部构造的 membrane：宿主返回值（数组、对象、Promise、Error）
  逐个复制或包装成 vm realm 的值；宿主函数只经 vm realm 的包装调用；vm 函数交给宿主前同样包装，
  免得宿主回调时把宿主对象递进去。membrane 用到的内建方法要在程序运行前捕获，不能被程序改写。
- context 用 `vm.constants.DONT_CONTEXTIFY`（Node ≥ 22.8）或 null 原型对象创建，不再把 `api`、
  `__deadlineRef`、`__parkState` 作为宿主对象挂到全局。
- 期限检查改在宿主侧做；现在程序可以改写 vm 里的 `Date.now` 让 `__wrap` 的检查失效。
- 另有一个已知但本次未测的边界：`await` 之后的同步死循环不受 `runInContext` 的 timeout 约束，
  [006] 的“硬性超时”只覆盖首个同步段和 api 调用点。要真正硬性需要 worker/子进程隔离。
- Node 文档明确说 node:vm 不是安全机制。membrane 能堵住上面这些已知路径，但不能当作完整证明；
  抽成 `wanxiangli/sandbox` 时，库测试应当把上面每条探针做成在旧实现上会红的反例。

## 未做

能力面 / generator / file/run/Plan 接线（依赖 CUT-08 DONE）、教程/描述/schema 同源，均未开始。

## 修复进度（2026-10-21，eng-cut25-goose）

子范围①安全修复已合入主仓 `Process/JsSandbox.fs`（本卡后续提交）：

- 上下文改为 `vm.constants.DONT_CONTEXTIFY` + `microtaskMode: "afterEvaluate"`，不再把宿主 `api` / `__deadlineRef` / `__parkState` 挂到全局。
- 宿主值经 membrane 复制/包装成 sandbox-realm 值；Callable 实例经 bridge proxy 进入宿主回调时参数同样包装。
- 期限用宿主 `Date.now`（`Number(deadlineRef.current)`，避免 Fable int64/BigInt 混算）。
- 驱动循环在每次 drain 时施加 `runInContext` timeout，因此 `await` 之后的同步死循环也会 `PROGRAM_TIMEOUT`（证据：006-cut25-escapes 与 await-loop 探针）。
- 回归：`requirements/repository-programming/tests/006-cut25-escapes.test.mjs`；探针绿证 `evidence/CUT-25/fixed-green-escapes.json`。

未做：能力面 / generator / Plan 接线；抽 `wanxiangli/sandbox` 模块（membrane 仍在主仓字符串内，下一步抽取）。

## 库抽取（2026-10-21）

membrane 驱动已抽入 `wanxiangli/sandbox`（库提交 `50f5e4c`，发布 `0.23.1` / `d2a9f3b`）。主仓 `JsSandbox` 组合 `Sandbox().run`；ParkState / sentinel 解码 / 能力投影仍在主仓。库测试 `src/sandbox/tests` 含 naive base-red 与 membrane 绿证。


## 固有方法篡改逃逸（2026-10-21，eng-cut25b-goose）

0.23.1 membrane 与模型程序同 realm，`wrap`/`unwrap` 调用 `WeakMap.prototype.has`/`.set` 时把宿主对象当参数传进去；模型改写这两个方法后能观察到宿主值，再经 `constructor.constructor` 拿到 `process`。证据：`evidence/CUT-25/membrane-0.23.1-red-intrinsics.json`（红）、`membrane-0.23.2-green-intrinsics.json`（绿）。

修复在库 `bd67c08` / 发布 `0.23.2`（`7b06b93`）：安装时捕获 WeakMap/Reflect 方法，经捕获的 `Reflect.apply` 调用；参数拷贝不用 `Array.prototype.slice`/`map`。库测试 `SPEC[sandbox-003]`；主仓回归 `006-cut25-escapes.test.mjs` 两条 WeakMap 用例。

另：`await` 后同步死循环由 afterEvaluate 驱动排水在 `runInContext` timeout 下终止，证据仍是 006 末条与库 `SPEC[sandbox-002]`（子进程）。

未做：能力面 / generator / Plan 接线（依赖 CUT-08 DONE）、教程/描述/schema 同源。

## 旧 wrapProgram 退出（2026-10-21，eng-cut25b-goose）

主仓 `JsSandbox.wrapProgram`（仍拼装 `__wrap`/`api`/`__deadlineRef` 的旧字符串，并在 `.fsi` 公开）已无调用方；包装体由 `wanxiangli/sandbox` 拥有。删除 `.fs`/`.fsi` 定义，避免再被误用。`006`/`006-cut25-escapes` 回归仍全绿。

## 编程面准入统一（2026-10-21，eng-cut25b-goose）

CUT-08 移交的 `StaticTools.hasFsCapability` / `jsPermission` 与 `JsToolSpec.admissionFor` 两份手写权限列表已删除。
schema、执行准入、生成器都读 `JsCapability.admitsProgrammingSurface`（= `ofToolCapabilities` 非空）。

反例（旧列表会分叉）：仅 Edit → 旧 hasFs 放行、生成器拒绝；仅 Exec → 旧 hasFs 拒绝、旧 admission 放行、生成器放行。
证据：`evidence/CUT-25/differential-admission.mjs` → `admission-unified.json`（11 角色 compared / differing 0）。
capability-enforcement 002/006 与 006-cut25-escapes 全绿。

## 编程面投影交库（2026-10-21，eng-cut25c/f-goose → eng-cut25g-goose 合入）

Office 权限 → JS 能力投影（JsPlan 展开、Orchestrator Fork→Commission）、`admitsSurface`、`toolNameFor`、
成员目录（dag 需 Join、Manager 的 `reviewOnly.*`）与 `projectRole` 只剩 `wanxiangli/programming` 一个 owner；
主仓只留 label ↔ `JsCapability` DU 表一份（RuntimeSurface 改读它）。证据 `differential-surface.mjs`：
11 角色 × 2 语言整面比较 22/0 不同；`admission-unified.json` 重跑字节相同。

## 基类、运行时 api 与成员目录同源（2026-10-21，eng-cut25g-goose）

`Description.fs` 的公开基类、Manager `ReviewOnly` 类、运行时基类与 `Callable`，以及 `ToolsBindings.createApi`
的运行时 api 安装，原来各自手写"哪个能力投影哪个成员"（共 33 处 `has`/`Set.contains` 门禁，`dag = Fork && Join`
写了两遍）。现在一律问 `present member`，答案来自 `JsCapability.memberNamesFor` = 库 `membersFor`，
与 schema 成员列表同一份目录。

- `differential-surface-directory.json`：基线 15f3c721e dist 对候选，22 个面（描述、基类、示例、成员、绑定）0 不同。
- `differential-runtime-api.json`：11 个角色的运行时 api 成员 0 不同；每个角色的 api 都等于库目录去掉基类语法糖 `dag`。
- 定向测试（repository-programming 全部、capability-enforcement 全部、work/024、advisor/006）：共 496 个，472 个通过，8 个 TODO，12 个跳过。
  失败的是 048 的 3 条，master 上原本就失败；另有 043 #6，它按源码文本钉住旧的 `Set.contains JsCapability.Remove`
  门禁，本刀改成钉住目录门禁，同一 dist 上 8/8 通过。

## 关卡（DONE）

- **退出条件**："同一能力不再有两套 generator/runtime gate"——满足。schema 成员列表、公开基类、运行时基类、
  运行时 api 安装、执行准入（`admitsProgrammingSurface`）与生成器准入，都读 `wanxiangli/programming` 同一份投影和目录。
  "公开 Surface 坚持普通 JS 数据"——`GeneratorSurface`/`WorkflowSurface` 只出普通对象，库面也只收发字符串数组和冻结对象。
- **沙箱**：membrane 驱动在 `wanxiangli/sandbox` 0.23.2，已知逃逸路径（宿主 realm 对象、公开 API 返回值、
  WeakMap/Reflect 固有方法篡改）都有先红后绿的回归测试，`await` 之后的死循环也会 `PROGRAM_TIMEOUT`。
- **诚实残余**：node:vm 不是 OS 级隔离，Node 文档明确说它不是安全机制。membrane 只堵住了上面列出的已知路径，
  不能当作完整证明；要真正隔离需要 worker/子进程加 OS 沙箱，这超出本卡"不降低原沙箱保证"的范围，
  留作产品决定。契约/规则的散文段和 ultra 示例仍按能力（不是按成员）选文本，因为那是规则文案，
  不是成员门禁。
