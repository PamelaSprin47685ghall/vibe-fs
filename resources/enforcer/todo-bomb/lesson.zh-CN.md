# todo-bomb — Lesson

## 评价

### 定义

TODO 只有在“当前系统即使永远不实现它也仍然履行现有 contract”时，才是真正的 future work。若 reachable shipped path 的正确性依赖未来把 TODO 补上，它就是一颗 correctness bomb。

Comment 不会暂停 contract。一个 `TODO`、`FIXME`、dummy return、`NotImplemented`、panic branch 若可被有效输入触发，就是系统已经知道自己无法履约，却把失败日期推给未来。

### 支配原则

TODO 只有在当前系统没有它仍然完整时，才是被推迟的工作。当 correctness 依赖未来的任务，comment 就把已知 defect 转成未文档化的定时炸弹：执行可能到达实现已经知道无法兑现的状态。系统要么实现它的 contract，要么在 boundary 显式拒绝不支持的 case。

### 何时触发

- supported input 能进入 `TODO: implement` branch；
- dummy value 暂时代替 required computation；
- production path 有 `panic/not implemented` 但 boundary 仍宣称支持该 case；
- safety/recovery/validation 被 note 替代；
- placeholder 只有“之后会补”而没有 contract narrowing。

### 不应触发

- 可选未来 enhancement，不影响当前 promise；
- unsupported case 在 boundary 被 typed/explcitly 拒绝；
- docs/backlog TODO 不冒充 shipped behavior；
- isolated spike 不可进入 production。

### 与相邻规则区分

`half-finished-refactor` 留下未完成的迁移；`spike-not-cleaned` 把 prototype shortcuts shipping 出去。Tie-break：如果已知的必需 correctness 通过 placeholder 在 delivered code 内被推迟，用本规则；如果 prototype shortcut 未清理就 shipping，用 `spike-not-cleaned`。

### 判定程序

给 TODO 找一个当前合法输入。若它能走到 placeholder，问题已经不是 backlog，而是 current contract hole。

### 例子

- positive：reachable production branch 用 `// TODO: compute tax` 返回 dummy value。
- near-miss：backlog comment 关于产品尚未提供的可选 report，且没有 supported path 进入那段代码。
- counterexample：merged spike 仍在“暂时”使用 hardcoded table，那是 `spike-not-cleaned`。

## 建议

### 现在该做什么

对 reachable required behavior 二选一：完整实现，或在真正 boundary 明确把该 case 标成 unsupported 并返回 typed refusal。不要让 placeholder 藏在深层路径等生产流量发现。

### 为什么重要

TODO bomb 把 known defect 伪装成 scheduling decision。系统表面上说“支持”，内部却写着“以后再说”，于是 caller 没有机会提前理解限制。

### 修复策略

追查 placeholder 是否可从 supported inputs 到达。实现真实 behavior 并测试它，或如果产品有意不支持该 case，把 rejection 移到 public boundary，返回 typed unsupported outcome。

### 决策分支

- 若 supported input 能到达 placeholder，实现 behavior，或在 public boundary 拒绝该 case。
- 若 note 是不支持 path 上的可选未来工作，把它留作 backlog，并保持不可达。

### 常见假修复

- 把 TODO 改成 warning log 后继续返回 dummy success。
- catch `NotImplemented` 并当 generic failure。
- 在 README 写“此 case 暂不完整”，但 API 仍 accept。
- 给 placeholder 加默认值，让它“不崩”。

### 验证

枚举 supported input space；任何有效输入都不应进入 placeholder/panic/dummy branch。明确 unsupported 的输入必须在 owner boundary 稳定拒绝。

### 完成条件

当前 contract 不依赖未来工作才能为真；future TODO 只描述 enhancement，不掩盖已知的 shipped correctness hole。

### Nudge

TODO 能推迟工作，不能推迟事实。要么现在实现承诺，要么诚实缩小承诺。
