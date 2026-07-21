# 09 — 方法论笔记本工具

## 概述

对外名 `methodology_<id>`，数量与 `src/Kernel/Methodology/Catalog.fs` 条目同步。工具供 LLM 在复杂推理时写入结构化 note，与 `todowrite` 的 `select_methodology` 联动。

## 数据驱动 Schema

| 模块 | 职责 |
| :--- | :--- |
| `Methodology/Schema.fs` | `MethodologyEntry` 类型、`buildUnifiedNoteDescription` |
| `Methodology/Logic.fs` | 逻辑条目 |
| `Methodology/ProblemTransformation.fs` | 问题转换条目 |
| `Methodology/MathematicalReasoning.fs` | 数学推理条目 |
| `Methodology/Optimization.fs` | 优化条目 |
| `Methodology/SystemsEngineering.fs` | 系统工程条目 |
| `Methodology/CriticalInquiry.fs` | 批判探究条目 |
| `Methodology/Catalog.fs` | 聚合 6 个条目模块（`all: Lazy<MethodologyEntry list>`） |
| `Methodology/Registry.fs` | 从 `Catalog.all` 派生 `enumValues`、`enumValuesArray`、`tryFindEntry` |

**单一派生**：`select_methodology` 枚举从 `allSchemas` 派生，三处不复制（架构测试 `hookSchemaNoDuplicateMethodologySchema`）。

## 宿主注册

| 宿主 | Schema 工具 | 注册路径 |
| :--- | :--- | :--- |
| OpenCode / Mimocode | Zod | `src/Hosts/OpenCode/Tools.fs` |
| Mux | MuxJsonSchema | `src/Hosts/Mux/PluginCatalog.fs` |
| OMP | TypeBox | `src/Hosts/Omp/OmpToolSchema.fs` |

## meditator vs `methodology_*`

- `meditator`：选 `methodology` 枚举 + 填 `intent`/`background`/`note` → `meditator` 工具
- `methodology_*`：按该方法论 schema 填结构化 note 字段 → 独立 54 个笔记本工具

## 扩展新方法论的步骤

1. 在职责对应的 `Logic.fs`、`Optimization.fs` 等条目模块增加数据
2. 确认 `Catalog.all` 聚合包含
3. 重跑 schema 生成测试；枚举自动反映到 `todowrite`
4. **禁止**在宿主手写重复 enum 列表
