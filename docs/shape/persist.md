# Journal — 边界

## 物理位置

Journal 位于 Git common directory 下私有 runtime 路径（见 `RuntimePath`），**不**在受测 workspace 创建 `node_modules` 或插件数据目录。

## PERSIST-006：文件权限

```text
Runtime directory：0700
Journal 文件：0600
```

Student QA 位于当前项目 Git private directory 下的任务路径，天然不进入 index/worktree；目录 `0700`、
文件 `0600`。路径由 workspace Git directory + Student SessionId + LogicalRunId 纯派生并校验，禁止接受
模型提供的路径片段。无法证明 Git-private 路径时 Student bootstrap fail closed。

QA 写入口唯一是 `StudentQaStore`：同目录临时文件写入、file fsync、原子 rename、directory fsync。
编译阶段 Student 只有 read 权限访问 QA；write/edit 仅用于 `.agent/skills`，最终 `return` 只调用 store
删除，Teacher 与普通文件工具不能写 QA。

## 写入口纪律

领域事实的 append 经 Journal 唯一路径；各领域外部效果的 Requested/Accepted 成对出现（PERSIST-009），不得旁路「先改内存再补盘」。

上下文恢复事实（PERSIST-010）的单一观察写入口是相应 reconcile 路径（例如 compaction → `ContextReanchored`），禁止多处随手写 fold 特例。
