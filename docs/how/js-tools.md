# JS Tools — 已裁决算法与控制流

## 主流程（唯一实现序）

```text
resolve Attempt
→ get immutable profile（AttemptExecutionProfile）
→ generate js-* surface（若 capability 非空）
→ assemble builtins + js-ROLE
→ BuiltinToolDescriptionHook 改写可见 builtin filesystem description
→ provider 看到工具
→ 模型优先调用 js-ROLE（builtins 仍可执行）
→ ToolRegistry 校验 invoked name 属于当前 Attempt 同一 surface（forged → fail closed）
→ 创建 sandbox，注入生成的 runtime bindings
→ 执行 class Js { async run() { ... } }
→ 收集 return + ReadSet + WriteSet
→ 解析 JSON 为值树并 JS-010 校验（失败则零提交）
→ snapshot/path validation（preflight）
→ 若 WriteSet 非空：transaction prepare → commit
→ 若 WriteSet 空（纯查询）：跳过事务
→ 渲染 Synthetic TOML（JS-016：`# ok`/`# failed` + `[data]`/`[fs]`）
```

## 生成规则（deterministic）

```text
profile.ToolCapabilitySet
→ 过滤出文件系统相关 capabilities（Read/Write/Edit/Glob/Grep）
→ Capability Fragment Registry 逐能力取 member fragment（含 Grep → grep()）
→ 拼接 runtime 代理基类（沙箱用；含 `_api`；file() 语义与公开算法等价）
→ 拼接公开 JsProgram（Read 时嵌入 §16 file() 全文；Glob/Grep/Edit/Write 为 Host stub；始终有 run()）
→ 拼接 description = header + 模型合同（Prefer js-ROLE / 并行 / 复杂 program）+ 公开基类 + 仅当前能力的规则 + Requires ⊆ capabilities 的 examples + footer
→ Grep 是 member（JS-020）；Read+Glob 而无 Grep 时仍可含 glob+file+RegExp composition example
→ 生成 runtime capability bindings（存在的方法 ↔ 实际执行器）
→ 生成 BuiltinToolDescriptionHook 文案（引用 js-ROLE 名称）
```

同一 profile → 同一字节输出（fast/deep 相同）。generated-name gate：ToolRegistry 只接受当前 Attempt surface 生成的名字。

## Anchor 匹配

- 声明顺序匹配：第 N 个声明匹配文本中第 N 次出现；duplicate textual occurrence 依序消歧。
- 精确字符串锚点：literal 查找。
- RegExp 锚点：按 RegExp 语义匹配；`^` / `$` = 文件绝对首/尾（非行锚）。
- 零宽 RegExp：位置有效，可表达插入点。
- 5 类拒绝（`ANCHOR_*`）：不唯一且未指定序 / 不匹配 / 正则非法 / 跨文件混用 / 空锚点。按序找不到：`ANCHOR_NOT_FOUND`，reason 含声明序号、path、cursor、pattern 预览。
- `text(from, to)` 位移名：`name+N` / `name-N`。全名已在 Map 则用声明；否则最后一个 `[+-]digits` 为 delta，基名递归解析。caret clip 到 `[0, file_len]`（含 EOF）。位移是临时 caret，不写入 Map。

## glob（JS-007）

```text
expand {a,b}
→ compile gitignore/wildmatch（相对 capability 根；无 `/` 则加 `**/` 前缀）
→ 载入根 `.gitignore` 与 `.git/info/exclude`
→ walk：跳过 `.git` 与 symlink；目录命中 ignore 则剪枝；进入子目录时叠加该目录 `.gitignore`
→ 只收集匹配的普通文件
→ 稳定排序
→ 截断到 maxEntries（绑定层 256）；truncated = 匹配或访问触顶
```

有界计数的是匹配文件，不是 DFS 前缀。`.git` 不得占用配额。

## grep（JS-020）

```text
JS-007 glob(pattern) 选文件（文件上限宽于返回给模型的 glob 上限）
→ 逐文件 strict UTF-8；失败则跳过
→ needle：字面量或 RegExp，收集全部出现
→ 每条命中 { path, line, column, text }（1-based）
→ 截断到 maxMatches（绑定层 500）；truncated 可见
```

## FileView

`file(path, matches = [])` 读取时快照不可变视图并按序解析 anchors。read 路径：strict UTF-8 校验 → 快照缓存 → FileView.text(from, to)。N 个锚点可只用于阅读：一次 `file()` 钉多节标题，再用 `text("h1end", "h2")` 或 `text("h1", "h1+200")` 取窗。同一 program 内 mutation 不改变先前取得的 FileView（快照隔离）。

## Transaction

```text
preflight（全部目标路径一次校验：存在性 / UTF-8 / 同路径单意图 / capability 边界）
→ durable prepare（EventStore facts + payloads；禁私有 store）
→ 执行 staged effects（ephemeral）
→ result validation
→ commit（workspace effect + EventStore Committed fact；CAS 失败 → rollback）
→ rollback（normal 失败：全部 staged 效果归零）
→ crash recovery（只从 EventStore facts/payloads 重建：未 commit 无效果；已 commit 重放一致）
```

ReadSet/WriteSet 由 sandbox 收集（typed execution 捕获，不从 transcript 文本推断）。冲突：快照后外部修改 → `FILE_CHANGED` fail closed，不隐式 retry。

## Sandbox

runner 只获得显式注入的数据（路径字符串、FileView 内容、glob 结果），不获得文件句柄 / 环境 / 网络 / 进程。`new Function` 仅作 invocation mechanism。deadline 超时 kill + reap；stdout/stderr 不是编辑结果（编辑结果只来自 staged effects）。



## Result 渲染（JS-016）

```text
sandbox JSON string
→ parse 值树
→ validate（数组 null / 异构对象数组 → INVALID_RETURN_VALUE，此时尚未 prepare/commit）
→ preflight / 事务
→ JsToolsResult：
     Failed → document(["failed"], code + reason)
     Succeeded → document(["ok"], encodeData(value) ++ optional [fs])
→ ARCH-012 bound（ToolHostCodec）
```

`[fs]` 仅含非空 `rewritten` / `created` 路径数组，且为最后一个 table block。对象字段 `null` 省略。根原始值用 `data = …`。
## Failure algebra

程序可预见失败 = 稳定失败码返回（proposal §77.1：`FILE_NOT_FOUND` / `FILE_ALREADY_EXISTS` / `FILE_CHANGED` / `INVALID_UTF8` / `ANCHOR_*` / `PERMISSION_DENIED` / `PATH_DENIED` / `PROGRAM_*` / `TRANSACTION_*` / `DUPLICATE_MUTATION_TARGET` / `RESULT_TOO_LARGE` / `INVALID_RETURN_VALUE`）。异常只留给程序无法继续的事故（进程崩溃、Host 故障）；不从 exception message 反推业务错误种类。

运行时 throw 若带 `__jsFailure = { code, reason }`，沙箱 sentinel `__jsHostFailed` 按 code 还原 JsFailure；普通 throw 走 `__jsProgramFailed` + message，渲染为 `PROGRAM_FAILED` 且 reason 含该 message。异步 deadline proxy 使用 `PROGRAM_TIMEOUT` sentinel，不靠嗅探 `'__PROGRAM_TIMEOUT__'` 字符串。
