# 18 — 术语表与 SSOT 对照

## 术语

| 术语 | 含义 |
| :--- | :--- |
| Kernel | 纯 F# 领域层，`src/Kernel/` |
| Shell | IO 与 codec，`src/Shell/` |
| Host | `Opencode \| Mimocode \| Mux \| Omp` |
| SSOT | Single Source of Truth；此处多指 NDJSON 或指定模块 |
| Fold | 对事件列表的纯 reduce，得当前投影 |
| With-Review | `/loop` 驱动的审查循环 |
| Caps | 系统前导提示（宝典/铁律/工具说明） |
| WIP | `submit_review` 部分完成标记 |
| Iterator | fuzzy_continue / continue 的翻页或子会话句柄 |

## 真相源对照表

| Concern | SSOT 位置 | 非真相 |
| :--- | :--- | :--- |
| Review task / loop 活跃 | `.wanxiangshu.ndjson` + `foldReviewTask` | 仅读 `ReviewStore` 捷径、compaction 前消息 |
| Todo backlog 内容 | 最后 `work_backlog_committed` | 历史 todowrite tool 消息全文 fold |
| Nudge 去重 | `foldNudgeDedup` / `nudge_dispatched` | 内存去重表单独为准 |
| 工具 description | `Kernel.ToolCatalog` | 宿主手写重复文案 |
| 工具权限 | `ToolPermission` + `ToolCatalog.Classification` | 宿主 if/else |
| 方法论 enum | `Methodology.Registry` 派生 | 三处手写列表 |
| 宝典/铁律正文 | `Kernel.CapsPrelude`（组装经 Shell cache） | MessageTransform 内联长文 |
| 宿主工具名映射 | `HostTools` | 魔字符串散落 |
| OpenCode hook 字段 | 原地 mutate + codec | 替换 output 对象引用 |
| 万象阵 DAG | `.wanxiangzhen.ndjson` + `Kernel/Wanxiangzhen` fold | 万象术 `.wanxiangshu.ndjson` |

## 文件路径速查

| 路径 | 说明 |
| :--- | :--- |
| `[workspace]/.wanxiangshu.ndjson` | 万象术事件日志 |
| `[workspace]/.wanxiangshu.ndjson.lock` | 追加锁 |
| `build/src/Mux/Plugin.js` | 默认包入口 |
| `build/src/Omp/Plugin.js` | OMP 入口 |
| `tests/logs/*.verbose.log` | 测试详细日志 |

## 文档维护

- 规格只写 `docs/`；冲突以 `src/` 为准后改 docs
- 实施顺序（`AGENTS.md`）：文档 → 测试 → 代码
- 万象阵专题：[19-wanxiangzhen.md](./19-wanxiangzhen.md) → 全文 [wanxiangzhen/01-master-spec.md](./wanxiangzhen/01-master-spec.md)

## 跨仓库

| 仓库 | 关系 |
| :--- | :--- |
| `../opencode` | 只读参考，不改上游 |
| `../oh-my-pi` | 只读参考，不改上游 |
| `../mux` | binding 可改，核心少动 |

## 相关

- [00-index.md](./00-index.md)