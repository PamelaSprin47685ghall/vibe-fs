# HOW — repository-programming 的实现模型与约束

> 非 normative。描述当前实现如何满足 WHAT；实现可整体替换（`17-repository.md` INDEPENDENT CHANGE：换 embedded language/IR 而合同不变）。

## 模块地图（当前实现）

### Domain（纯决策；零 Host I/O）

| 文件 | 内容 |
|---|---|
| `src/Wanxiangshu/Domain/JsCapability.fs` | `JsCapability = Read | Write | Edit | Glob | Grep`；`JsCapabilityFragment`（member 名/description/example/runtime binding 单一事实源）；`JsFragmentRegistry` |
| `src/Wanxiangshu/Domain/JsSurface.fs` | `JsSurface` 类型；`JsToolGenerator`：`membersFor` / `toolNameFor`（`"js-" + roleName.ToLowerInvariant()`）/ `generate`（投影主函数）/ `isGeneratedToolName` / `memberBinding` |
| `src/Wanxiangshu/Domain/JsDescription.fs` | `JsCanonicalDescription`：description 组装规则 + 资源路径常量 |
| `src/Wanxiangshu/Domain/JsFailure.fs` | `JsFailure` 代数（23 个 case）+ 稳定码映射（`JsFailure.code`）；`AnchorFailure` |
| `src/Wanxiangshu/Domain/JsAnchor.fs` | `AnchorSpec = Exact | Regex`；`AnchorDeclaration`；`AnchorRules`（有序匹配/拒绝规则） |
| `src/Wanxiangshu/Domain/JsTransaction.fs` | `JsStagedMutation = Rewrite | Create`；`JsTransaction`（staging 纯逻辑）；`JsTransactionId`；`JsDurableMutation` / `JsTransactionPrepared` / `JsTransactionCommitted`；`JsTransactionFacts` |

### Infrastructure（Host 适配；唯一 I/O 点）

| 文件 | 内容 |
|---|---|
| `src/Wanxiangshu/Infrastructure/JsToolsBindings.fs` | `createApi(root, staging)`：注入 sandbox 的 `file`/`glob`/`grep`/`rewrite`/`write` 实现；`resolveInside` 做 path containment（escape → `PATH_DENIED`） |
| `src/Wanxiangshu/Infrastructure/JsUtf8Fs.fs` | strict UTF-8 解码（`INVALID_UTF8`） |
| `src/Wanxiangshu/Infrastructure/JsGlobFs.fs` | gitignore/wildmatch glob 实现（全量枚举、跳过 `.git`/symlink） |
| `src/Wanxiangshu/Infrastructure/JsMutationFs.fs` | 磁盘 mutation 原语（rewrite/create、compare-before-effect） |
| `src/Wanxiangshu/Infrastructure/JsAnchorFs.fs` | 锚点解析的 fs 侧实现 |
| `src/Wanxiangshu/Infrastructure/JsToolsTransactionStore.fs` | EventStore 适配：`TransactionStream = "js-tools/transactions"`；`PreparedEventType = "JsTransactionPrepared"` / `CommittedEventType = "JsTransactionCommitted"`；`appendPrepared` / `appendCommitted`；recovery 读取 |
| `src/Wanxiangshu/Infrastructure/OpenCode/Tools/JsToolWorkflow.fs` | `JsToolsData`（JSON 值树 parse + REPOSITORY-PROGRAMMING-011 校验，`validateArray`/`ofJsValue`）；`JsToolWorkflow.run`（sandbox → 收 ReadSet/WriteSet → preflight → prepare → commit）；`JsToolsResult.render`（REPOSITORY-PROGRAMMING-016 两份文档） |
| `src/Wanxiangshu/Infrastructure/OpenCode/Tools/JsToolHost.fs` | `BuiltinToolDescriptionHook`（`validateRecommendation` fail-closed、`annotate`）；`JsDescriptionAssets`（双语 prose 装载）；`JsToolSpec.create`（生成 spec） |
| `src/Wanxiangshu/Infrastructure/OpenCode/Tools/FileMutationTools.fs` | `mvSpec` / `rmSpec`（POSIX 语义 + 本地化 consequence） |
| `src/Wanxiangshu/Infrastructure/OpenCode/Tools/ToolRegistry.fs` | `rolePredicate`（spec 可见性）+ 执行 gate（invoked name 属于当前 surface） |

### Process

| 文件 | 内容 |
|---|---|
| `src/Wanxiangshu/Process/JsSandbox.fs` | `wrapProgram`（base class + model source → framed program）；`run` / `runSurface`；`classifySyncError`；sentinel 前缀 `__jsProgramFailed` / `__jsHostFailed` / `__jsInvalidReturn`；deadline 超时 kill；输出 bound |

### 静态门禁

`scripts/checks/js-surface-gate.mjs`：`FORBIDDEN_TOKENS`（js-student/js-teacher 等 G3 debt）+ `HANDWRITTEN_ROLE_TOOL_TOKENS`（js-coder 等字面量）→ 扫描 `src/Wanxiangshu/**`；唯一合法静态枚举 = `src/Wanxiangshu/Tools/StaticTools.fs`（权限矩阵 schema 层）。`requirements/repository-programming/tests/js-surface-gate.test.mjs` 原为门禁的单元 oracle，已随本包 MOVE 为 `js-surface-gate.test.mjs`。

## 主流程（唯一实现序）

```text
resolve Attempt → immutable profile（AttemptExecutionProfile.ToolCapabilitySet）
→ JsToolGenerator.generate → js-* surface（name/schema/description/base class/examples/bindings）
→ ToolRegistry gate（invoked name ∈ 当前 surface → 否则 fail closed）
→ JsSandbox：注入 runtime bindings（JsToolsBindings.createApi）+ wrapProgram
→ 执行 class Js { async run() { ... } }
→ 收集 return + ReadSet + WriteSet
→ parse JSON 值树 → JS-010 校验（非法 → INVALID_RETURN_VALUE，零提交）
→ preflight：路径/UTF-8/同路径单意图/capability 边界/快照验证（FILE_CHANGED → fail closed，不隐式 retry）
→ WriteSet 非空：JsToolsTransactionStore.appendPrepared（EventStore durable）→ apply mutations（canonical 排序）→ appendCommitted → 暴露成功结果
→ WriteSet 空（纯查询）：无 commit，暴露已校验 return
→ JsToolsResult.render：Synthetic TOML 两份文档（#ok/#failed + [data]/[fs]）
```

## 依赖（DEPENDS ON，逐条理由）

| 依赖 | 理由 |
|---|---|
| `office-capability` | `ToolCapabilitySet` 由 office consequence 建立（ARCH-017）；本包只消费该集合，不裁决权限（REPOSITORY-PROGRAMMING-001）。 |
| `capability-enforcement` | capability → schema → runtime gate 同构/同源律由它拥有；本包应用该律到编程面（REPOSITORY-PROGRAMMING-002/021）。 |
| `effect-accounting` | transaction 的效果分型（Prepared/Committed/Unknown）跨 prompt/repository 共用 law oracle；durable prepare 语义消费它。 |
| `durable-events` | 唯一 EventStore substrate：`JsTransactionPrepared`/`Committed` facts + owned payloads；本包不建 feature store（REPOSITORY-PROGRAMMING-012/015）。 |
| `participant-horizon` | provider 可见 surface 不泄漏 Host 内部（公开基类无 `_api`；sandbox 不暴露 host internals；错误不回显 sandbox 内部）——信息准入边界。 |

## 历史与弃权

### 被拒方案（详见历史 change（js-capability-projected-tools、js-tools-toml-result）、历史 why/js-tools 条款）

- 五套独立 js-* RPC；万能基类 + prose warning；手写 role→JS 矩阵；alias/clean-break 替换 builtin；模型 JS 拿 ambient OS authority；事务先写盘再执行；结果 commit 后才发现不可用；walk-then-filter glob；`**`→`.*`；grep 仅靠 `glob()+file()+RegExp`；JSON stringify 进 TOML 字符串；`status` discriminator；程序对象扁平到文档根 + 保留字；逗号拼接路径；失败带半截 `[data]`；统一 `kind`/`origin`/`ok` 信封；js-tools 私有 TOML 方言（第二套 `"""`、null 哨兵）；从结果 TOML 反向解析控制流。全部按「为什么被拒」记录在 `WHY.md` §历史拒绝方案与各 change 文件。

### 判定为 HOW（非 normative；不入 WHAT）

- builtin `read`/`edit`/`write`/`glob`/`grep`/`patch` 与 `js-*` 的**共存**是当前产品形态（JS-003/017），「builtin 是否长期 coexist」本包不拥有（`17-repository.md` DOES NOT OWN）。
- `js-*` 具体工具名（`js-coder` 等）、base class 的 JS 语法形态、`JsProgram` 类名 → 当前实现词汇。
- bound 常数：glob maxEntries、grep maxMatches、sandbox deadline/memory/output 数值、`new Function` 细节 → HOW。
- Synthetic TOML 的引号/换行/delimiter/裸字段排序/值树编码 → `provider-projection`（ARCH-010）。
- `MaxKeywords=8`/`TopKPerKeyword=4` 等 warm-start 常数 → `repository-investigation`（AGENT-032 HOW，HANDOFF §12）。
- 10s join 预算等其它常数 → 各自 owner 的 HOW。

### 判定为 GARBAGE（migration/clean-break 沉积，不进入永久 WHAT）

- `js-student`/`js-teacher`/`StudentLearnJs`/`StudentCompileJs`/`StudentTeacherJs`：G3 rebase debt，已删领域（`PROMPT-012` absence）。`FORBIDDEN_TOKENS` 是 absence ratchet；新世界基线稳定后可删（PROOF-MAP DELETE 清单）。
- 旧结果面 golden（`status = "ok"` / `result = "{...}"` / 逗号拼接 `written`）：`js-tools-toml-result.md` 已 clean-break，旧字符串结果不迁移。

### 不归本包（COVERAGE 交叉确认）

- capability 同构/同源律 → `capability-enforcement`（`capability-isomorphism-gate.mjs`、`agent-permission-gate`）。
- provider-projection 部分（`js-tools-toml-result.md` 的值树进 SyntheticToml）→ `provider-projection`。
- Git shared-ref integration（`PublishClaimed` 三分支 CAS）→ `change-integration`。
