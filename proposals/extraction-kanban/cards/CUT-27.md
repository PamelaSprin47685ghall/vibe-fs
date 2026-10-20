# CUT-27：Casebook / 知识复用去向 —— 裁决

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-27)。

- **裁决人**：eng-cut19-goose，2026-10-20，按仓主“完全自主决定”的授权。
- **裁决**：**保留产品功能，分步抽取**。不删除（knowledge-reuse WHAT 001–016 仍生效，`requirements/knowledge-reuse/tests` 有 16 个编号测试），也不整体 RETAINED。

## 依据（亲眼核对，master 17ed19c67 之后）

- 主仓 `src/Wanxiangshu/Repository/Knowledge/Casebook/` 共 24 个 `.fs`、4028 行；真实消费者：`OpenCode/Tools/FetchTool.fs`（`CasebookIndex.resolve`）、Bookkeeper 刷新链（`Bookkeeper.fs` → `CasebookIndex.refresh`）、Capture/Settlement 写入 EventStore。
- 持久权威在统一 EventStore（WHAT 007），多 writer 合并与保留已在 CUT-19 子范围①②交给 `wanxiangli/convergence`/`retention`；Casebook 不得另立存储。
- 草稿“不再建”与仍生效的 WHAT 冲突（D-05），按 D-05 默认保留并维护；没有删除授权的证据，故不选 REMOVED。

## 抽取子范围（按依赖从内到外，每步可独立验收）

1. **Index 纯投影**（READY）：`CasebookIndex` 中的 `compactTitle`、`shelfmarkFor`（FNV-1a 判别后缀）、从 Case 列表到 `{shelfmark, question}` 的 LRU 截取与 epoch 推进判定，抽为纯函数；进程级 `frozen/dirty` 单例与 EventStore 读取留在主仓组合者。反例：同问不同会话 shelfmark 撞车；Case 顺序变化但公开索引不变时 epoch 不应推进；内部 sessionId 泄漏到公开条目。
2. **Fetch 差异决策**：基线 B 与目标 T 的 diff 分类（无差异 / 有差异需 CaseRefresh / 读取故障不等于 Missing，WHAT 005/016）。
3. **Capture 实质访问判定**（WHAT 003）：工具结果 → relatedPaths 的纯分类。
4. Bookkeeper（LLM 参与者）、Settlement/fatal（WHAT 013）、Host 工具接线留在主仓，最后随 CUT-35 组装评估。

## 不做

- 不改 Casebook 事件词汇与 NDJSON 字节；不迁移已有 Case 数据。
- 不在本卡删除任何 Case 原件或缓存文件。

## 当前状态

裁决完成，转 `READY`（无 owner）。子范围①可直接认领；其“切换前置”中的 CUT-19 只影响存储，不阻塞纯投影。
