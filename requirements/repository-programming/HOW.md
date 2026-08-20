# repository-programming — HOW

## 架构模型与执行流

`repository-programming` 实现了从静态权限到可编程动态沙箱的完整投影链路：

```text
AttemptExecutionProfile.ToolCapabilitySet
  ↓
JsToolGenerator (生成 js-<role> 工具定义、基类、描述与示例)
  ↓
ToolRegistry (验证被调用工具名属于当前生成的合法 surface)
  ↓
JsSandbox (启动隔离执行环境，注入只读与事务 Staging 原语)
  ↓
执行 JsProgram.run() → 收集返回值、ReadSet 与 Staged WriteSet
  ↓
JSON 兼容性与合法性校验 (失败 → INVALID_RETURN_VALUE，零提交)
  ↓
事务预检 Preflight (路径合规、UTF-8、冲突检测、同路径单意图)
  ↓
WriteSet 非空: EventStore.appendPrepared → 顺序写入磁盘 → EventStore.appendCommitted
WriteSet 为空: 跳过提交
  ↓
Synthetic TOML 渲染器 (# ok / # failed + [data] / [fs])
```

## 核心机制

### 1. 投影与四层同构

- **代码生成**：根据 `ToolCapabilitySet`（Read, Write, Edit, Glob, Grep）按需拼接 `JsProgram` 基类方法声明、工具说明文本与 canonical examples。
- **运行时拦截**：沙箱内部通过绑定代理将 `file`, `glob`, `grep`, `rewrite`, `write` 路由至受控实现。未被授予的方法在基类中完全不存在，若通过反射强行调用则由底层代理 fail closed。

### 2. 沙箱隔离与资源边界

- 用户代码通过隔离机制调用，禁止注入 `require`, `process`, `fs`, `fetch` 等具有 ambient OS authority 的对象。
- 每次调用配置硬性执行 deadline 与输出缓冲区上限；同步无限循环或异步超时均由宿主环境强制终止并回收。

### 3. 事务生命周期与持久化

- **Staging**：所有 `rewrite` 与 `write` 只在内存维护 `StagedMutation` 列表，不修改实际文件。
- **Preflight**：在落盘前核验目标文件指纹是否与初次读取一致；若外部发生变更，立即报告 `FILE_CHANGED` 并中止。
- **EventStore 闭环**：多文件提交前先持久化 `JsTransactionPrepared` 事件；落盘成功后追加 `JsTransactionCommitted`。进程若在两事件之间中断，未完成事务仅作审计记录，重启后不自动回滚或补齐。

### 4. 工具描述的行为引导

- 工具描述内嵌风险中断与失败反思规则，明确要求模型在定位代码时优先声明有序锚点（`file(matches)`），禁止将大范围字符串截取或正则替换作为默认重构手段。
- 引导模型在返回前对关键规模和不变量进行断言，保证异常情况下 staging 自动废弃，杜绝污染工作区。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| REPOSITORY-PROGRAMMING-001 | `requirements/repository-programming/tests/js-surface.test.mjs` |
| REPOSITORY-PROGRAMMING-002 | `requirements/repository-programming/tests/js-surface.test.mjs` |
| REPOSITORY-PROGRAMMING-003 | `requirements/repository-programming/tests/js-surface.test.mjs` |
| REPOSITORY-PROGRAMMING-004 | `requirements/repository-programming/tests/js-surface.test.mjs` |
| REPOSITORY-PROGRAMMING-005 | `requirements/repository-programming/tests/js-tool-host.test.mjs` |
| REPOSITORY-PROGRAMMING-006 | `requirements/repository-programming/tests/js-sandbox.test.mjs` |
| REPOSITORY-PROGRAMMING-007 | `requirements/repository-programming/tests/js-tools-fs.test.mjs` |
| REPOSITORY-PROGRAMMING-008 | `requirements/repository-programming/tests/js-tools-fs.test.mjs` |
| REPOSITORY-PROGRAMMING-009 | `requirements/repository-programming/tests/js-tools-fs.test.mjs` |
| REPOSITORY-PROGRAMMING-010 | `requirements/repository-programming/tests/js-transaction.test.mjs` |
| REPOSITORY-PROGRAMMING-011 | `requirements/repository-programming/tests/js-workflow.test.mjs` |
| REPOSITORY-PROGRAMMING-012 | `requirements/repository-programming/tests/js-tools-transaction-store.test.mjs` |
| REPOSITORY-PROGRAMMING-013 | `requirements/repository-programming/tests/js-tools-fs.test.mjs` |
| REPOSITORY-PROGRAMMING-014 | `requirements/repository-programming/tests/js-transaction.test.mjs` |
| REPOSITORY-PROGRAMMING-015 | `requirements/repository-programming/tests/js-tools-transaction-store.test.mjs` |
| REPOSITORY-PROGRAMMING-016 | `requirements/repository-programming/tests/js-workflow.test.mjs` |
| REPOSITORY-PROGRAMMING-017 | `requirements/repository-programming/tests/js-parallel-contract.test.mjs` |
| REPOSITORY-PROGRAMMING-018 | `requirements/repository-programming/tests/js-anchors.test.mjs` |
| REPOSITORY-PROGRAMMING-019 | `requirements/repository-programming/tests/js-workflow.test.mjs` |
| REPOSITORY-PROGRAMMING-020 | `requirements/repository-programming/tests/file-mutation-tools.test.mjs` |
| REPOSITORY-PROGRAMMING-021 | `requirements/repository-programming/tests/js-surface-gate.test.mjs` |
| REPOSITORY-PROGRAMMING-022 | `requirements/repository-programming/tests/js-surface.test.mjs` |
