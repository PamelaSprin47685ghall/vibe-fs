# repository-programming — HOW

## 架构模型与执行流

`repository-programming` 实现了从静态权限到可编程动态沙箱与直接文件工具的完整投影链路：

```text
AttemptExecutionProfile.ToolCapabilitySet
  ↓
JsToolGenerator (生成 js-engineer / js-devops 工具定义、基类、描述与示例)
  ↓
ToolRegistry (验证被调用工具名属于当前生成的合法 surface)
  ↓
JsSandbox (启动隔离执行环境，注入只读与事务 Staging 原语)
  ↓
执行 JsProgram.run() → 收集返回值、ReadSnapshots 与 Staged WriteSet
  ↓
JSON 兼容性与合法性校验 (失败 → INVALID_RETURN_VALUE，零提交)
  ↓
事务预检 Preflight (路径合规、UTF-8、冲突检测、同路径单意图)
  ↓
WriteSet 非空: EventStore.appendPrepared → 顺序写入磁盘 → EventStore.appendCommitted
WriteSet 为空: 跳过提交
  ↓
收集实质访问 (Substantive Access)：显式 read 与成功 committed mutation
  ↓
Synthetic TOML 渲染器 (# ok / # failed + [data] / [fs])
```

## 核心机制

### 1. 投影与四层同构

- **代码生成**：根据 `ToolCapabilitySet`（Read, Write, Edit, Glob, Grep）按需拼接 `JsProgram` 基类方法声明、工具说明文本与 canonical examples。为 Engineer 与 DevOps 生成专属工具名（如 `js-engineer`、`js-devops`）。
- **直接文件工具**：对外提供 Read、Write、Edit、Glob、Grep、Move、Remove 工具面，与 JS 工具共享统一权限与底座。
- **运行时拦截**：沙箱内部通过绑定代理将 `file`, `glob`, `grep`, `rewrite`, `write` 路由至受控实现；`edit` 是生成 SDK 内的纯规划层，先经既有 `js.read` 取得不可变快照，完成定位与验证后再恰好调用一次既有 `js.edit` staging executor。

### 2. 事务快照与案例实质访问分离

- **ReadSnapshots**：包含程序显式调用 `file()` 以及 `grep()` 内部为了全文搜索而打开的所有文件快照，专供 Preflight CAS 指纹核对使用；
- **Substantive Access**：独立观察通道，仅记录用户显式 `file()`/`read` 以及在事务 Committed 后确认落盘的 `Write`/`Rewrite`/`Move`/`Remove` 目标路径；未提交或失败的事务仅保留在此之前发生的读取，不记录任何修改成功。

### 3. 事务生命周期与持久化

- **Staging**：`edit`、`rewrite` 与 `write` 最终都只在内存维护 `StagedMutation` 列表，不修改实际文件；其中一个 `edit` 调用至多形成一个 `Rewrite` intent。
- **Preflight**：提交/回滚计划先将每个逻辑路径解析一次为私有 typed mutation；预检、逐项重验、物理写入、失败分类与 CAS 回滚复用同一 resolved path。在落盘前核验目标文件指纹是否与初次读取一致；若外部发生变更，立即报告 `FILE_CHANGED` 并中止。
- **EventStore 闭环**：多文件提交前先持久化 `JsTransactionPrepared` 事件；落盘成功后追加 `JsTransactionCommitted`。进程若在两事件之间中断，未完成事务仅作审计记录，重启后不自动回滚或补齐。

### 4. 渐进式编辑代数与保守失败恢复

- **Easy path — `edit(path, changes)`**：普通 replace / insert / delete / all 只声明当前 `find` 与最终 `put`。
- **Hard path — `rewrite(path, newText)`**：结构重排、计算式输出、capture-dependent 变换与任意生成逻辑仍可直接提交完整目标文件。
- **失败代数**：`INVALID_EDIT`、`EDIT_NOT_FOUND`、`EDIT_AMBIGUOUS`、`EDIT_OVERLAP` 进入稳定失败代数；诊断预算独立于文件行长与 `put` 大小。
