# CUT-25 受限 JS 编程面与执行组合

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

