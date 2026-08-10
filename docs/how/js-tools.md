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
→ result validation（commit 前）
→ snapshot/path validation（preflight）
→ 若 WriteSet 非空：transaction prepare → commit
→ 若 WriteSet 空（纯查询）：跳过事务，直接渲染
→ 渲染 result（Synthetic TOML）
```

## 生成规则（deterministic）

```text
profile.ToolCapabilitySet
→ 过滤出文件系统相关 capabilities（Read/Write/Edit/Glob/Grep）
→ Capability Fragment Registry 逐能力取 member fragment
→ 拼接 base class（class Js extends JsProgram { file/glob/... }）
→ 拼接 description（只列存在的方法）
→ 拼接 canonical examples（只含存在的方法）
→ 生成 runtime capability bindings（存在的方法 ↔ 实际执行器）
→ 生成 BuiltinToolDescriptionHook 文案（引用 js-ROLE 名称）
```

同一 profile → 同一字节输出（fast/deep 相同）。generated-name gate：ToolRegistry 只接受当前 Attempt surface 生成的名字。

## Anchor 匹配

- 声明顺序匹配：第 N 个声明匹配文本中第 N 次出现；duplicate textual occurrence 依序消歧。
- 精确字符串锚点：literal 查找。
- RegExp 锚点：按 RegExp 语义匹配；`^` / `$` = 文件绝对首/尾（非行锚）。
- 零宽 RegExp：位置有效，可表达插入点。
- 5 类拒绝（`ANCHOR_*`）：不唯一且未指定序 / 不匹配 / 正则非法 / 跨文件混用 / 空锚点。

## FileView

`file(path)` 读取时快照不可变视图。read 路径：strict UTF-8 校验 → 快照缓存 → 返回。同一 program 内 mutation 不改变先前取得的 FileView（快照隔离）。

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

## Failure algebra

程序可预见失败 = 稳定失败码返回（`FILE_MISSING` / `FILE_EXISTS` / `FILE_CHANGED` / `FILE_NOT_UTF8` / `ANCHOR_*` / `CAPABILITY_DENIED` / `SANDBOX_*`）。异常只留给程序无法继续的事故（进程崩溃、Host 故障）。
