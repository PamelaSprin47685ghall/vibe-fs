# 09 — 方法论笔记本工具

## 概述

**54** 个结构化方法论工具，对外名 **`methodology_<id>`**（如 `methodology_first_principles`）。供 LLM 在复杂推理时写入「方法论笔记本」字段，与 `todowrite` 的 `select_methodology` 联动。

## 数据驱动 Schema

| 模块 | 职责 |
| :--- | :--- |
| `Methodology/SchemaCommon.fs` | 共享 schema 构建块 |
| `Methodology/Args.fs` | 参数 DU / 记录 |
| `Methodology/Catalog1.fs`–`Catalog4.fs` | 各方法论 `buildSchema` 数据 |
| `Methodology/Catalog.fs` | 聚合 |
| `Methodology/Registry.fs` | `allSchemas`、`enumValues` |

**单一派生**：`select_methodology` 枚举从 `allSchemas` 派生，避免 Catalog / Kernel / 宿主三处复制（架构测试 `hookSchemaNoDuplicateMethodologySchema`）。

## Kernel 元数据

`Kernel/Methodology.fs`、`MethodologyCatalog.fs`：

- 方法论 id 列表
- `todowrite` / `task` 必填字段说明（`background`、`intent`、`note`、`methodology` 等 meditator 工具字段）

## 宿主注册

| 宿主 | 模块 |
| :--- | :--- |
| OpenCode / Mimocode | `Methodology/OpencodeTools.fs` → `Opencode/Tools.fs` |
| Mux | `Methodology/MuxTools.fs` → `Mux/HostTools.fs` / `PluginCatalog` |
| OMP | `Methodology/OmpTools.fs` → `OmpToolSchema.methodologyParameters` |

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

1. 在 `CatalogN.fs` 增加 `buildSchema` 数据条目
2. 确认 `Registry.allSchemas` 聚合包含
3. 重跑 schema 生成测试；枚举自动反映到 `todowrite`
4. **禁止**在宿主 hand-write 重复 enum 列表

## 相关文档

- [07-work-backlog.md](./07-work-backlog.md)
- [08-tools-and-permissions.md](./08-tools-and-permissions.md)