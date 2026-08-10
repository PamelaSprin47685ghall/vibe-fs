# Journal — 边界

## 物理位置

Journal 位于 Git common directory 下私有 runtime 路径（见 `RuntimePath`），**不**在受测 workspace 创建 `node_modules` 或插件数据目录。

## PERSIST-006：文件权限

```text
Runtime directory：0700
Journal 文件：0600
```

## StudentQaStore — G3 已删除（retired）

`StudentQaStore` / 私有 `QA.md` filesystem backend **不存在于生产**（G3 clean-break）。无 dual-write、
无 legacy reader、无编译期 QA 权限面。后继统一 durable 事件底座（若有）归属后续 EventStore cutover；
正式层不得把尚未落地的存储 Proposal 当作现行规范。

## 写入口纪律

领域事实的 append 经 Journal 唯一路径；各领域外部效果的 Requested/Accepted 成对出现（PERSIST-009），不得旁路「先改内存再补盘」。

上下文恢复事实（PERSIST-010）的单一观察写入口是相应 reconcile 路径（例如 compaction → `ContextReanchored`），禁止多处随手写 fold 特例。
