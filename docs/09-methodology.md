# 09 — 方法论笔记本工具

## 概述

结构化方法论工具对外名为 **`methodology_<id>`**（如 `methodology_first_principles`）。数量与 `src/Kernel/Methodology/Catalog.fs` 的条目同步，不在文档中硬编码数量；工具供 LLM 在复杂推理时写入「方法论笔记本」字段，与 `todowrite` 的 `select_methodology` 联动。

## 数据驱动 Schema

| 模块 | 职责 |
| :--- | :--- |
| `src/Kernel/Methodology/Schema.fs` | 方法论条目与 schema 类型 |
| `src/Kernel/Methodology/Logic.fs`、`ProblemTransformation.fs` | 逻辑与问题转换条目 |
| `src/Kernel/Methodology/MathematicalReasoning.fs`、`Optimization.fs` | 数学推理与优化条目 |
| `src/Kernel/Methodology/SystemsEngineering.fs`、`CriticalInquiry.fs` | 系统工程与批判探究条目 |
| `src/Kernel/Methodology/Catalog.fs` | 聚合六个条目模块 |
| `src/Kernel/Methodology/Registry.fs` | 对外 schema 与 enum 派生 |

**单一派生**：`select_methodology` 枚举从 `allSchemas` 派生，避免 Catalog / Kernel / 宿主三处复制（架构测试 `hookSchemaNoDuplicateMethodologySchema`）。

## Kernel 元数据

`src/Kernel/Methodology/` 与 `src/Kernel/CapsPrelude.fs`：

- 方法论 id 列表
- `todowrite` / `task` 必填字段说明（`background`、`intent`、`note`、`methodology` 等 meditator 工具字段）

## 宿主注册

| 宿主 | 模块 |
| :--- | :--- |
| OpenCode / Mimocode | `src/Hosts/OpenCode/Tools.fs`、`OpencodeTools.fs` 等注册路径 |
| Mux | `src/Hosts/Mux/PluginCatalog.fs`、工具注册模块 |
| OMP | `src/Hosts/Omp/OmpToolSchema.fs`、`Tools.fs` |

OMP 使用 **TypeBox**；OpenCode 系使用 **Zod**（经 Shell `JsonSchemaBuilders`）；Mux 使用 `MuxJsonSchema`。

## meditator 工具

`methodology` 工具（meditator）与 54 个笔记本工具区别：

- meditator：选择 `methodology` 枚举 + 填写 `intent` / `background` / `note`
- `methodology_*`：按该方法论 schema 填结构化 note 字段

## 执行路径

参数 `obj` → Shell 解码 → 通常**无持久化副作用**（返回结构化文本供对话使用）；不写入 NDJSON，除非用户随后 `todowrite` 引用 `select_methodology`。

## 测试

- 集成：`IntegrationMuxMethodologySpecs` 等
- 架构：duplicate schema guard、OMP 全注册

## 扩展新方法论的步骤

1. 在职责对应的 `Logic.fs`、`Optimization.fs` 等条目模块增加数据
2. 确认 `Registry.allSchemas` 聚合包含
3. 重跑 schema 生成测试；枚举自动反映到 `todowrite`
4. **禁止**在宿主 hand-write 重复 enum 列表

## 相关文档

- [07-work-backlog.md](./07-work-backlog.md)
- [08-tools-and-permissions.md](./08-tools-and-permissions.md)
