# Enforcer Catalog

Status: accepted
Date: 2026-08
Decision owner: Wanxiangshu

## Context

规则曾由 RFC/规范生成 F# 源码，导致：

- 增删规则必须改代码并重编译；
- 生成器、文档、fixture 多份平行清单易漂移；
- 规则正文与运行时数据无法在不发版代码的前提下对齐。

规则是数据，不是控制流。应与领域类型分离，作为打包资源加载。

## Decision

规则实例存放于 `resources/enforcer/catalog.json`。

职责划分：

- Domain：`EnforcerRule` 类型与 `EnforcerCatalog.validate`；不读文件。
- Infrastructure：从 package-relative 路径加载 JSON，解码后调用 Domain 校验。
- 启动时 fail fast：缺失、JSON 非法、校验失败均中止加载。
- 不提供编译内置副本，不在 `dist/` 复制第二份 catalog。

包面：`package.json` 的 `files` 必须包含 `resources/`；运行时只认仓库/包根下的 `resources/enforcer/catalog.json`。

## Schema

根对象：

| 键 | 类型 | 含义 |
|----|------|------|
| `schemaVersion` | int | 目录格式版本；当前仅支持 `1` |
| `rules` | array | 规则实例列表 |

每条规则：

| JSON 键 | Domain 字段 | 含义 |
|---------|-------------|------|
| `id` | `RuleId` | 稳定规则标识；发布后不得重命名 |
| `field` | `FieldName` | provider tool schema 字段名；唯一；发布后不得重命名 |
| `family` | `Family` | 规则族标签（分组，非控制流） |
| `scoreWhen` | `ScoreWhen` | 评分触发描述；进入 tool schema description |
| `nudge` | `Nudge` | 固定反馈文案；投影为 canonical nudge 文本 |
| `catalogOrdinal` | `CatalogOrdinal` | 目录顺序；用于排序与稳定枚举 |

评分语义共用 0–9，目录不重复定义分值刻度。

`id` / `field` 一旦发布即稳定；改文案会改变 provider-facing schema，视为发布变更。

## Validation

`EnforcerCatalog.validate schemaVersion rules` 在加载边界执行：

1. `schemaVersion` 必须为当前支持版本（现为 `1`）。
2. `rules` 经校验后非空语义由完整规则集保证；每条文本字段非空（trim 后 length > 0）：`id`、`field`、`family`、`scoreWhen`、`nudge`。
3. `id` 全局唯一；`field` 全局唯一。
4. `catalogOrdinal` 排序后必须为连续 `1..N`（`N = rules.Length`）；缺口、重复、越界均拒绝。
5. 解码失败或校验失败 → 加载抛错，插件不得带残缺目录启动。

Domain 可导出 `triples`：`(FieldName, RuleId, CatalogOrdinal)` 供 codec 使用，仍不读文件。

## Consequences

- 新增/修订规则只改 `resources/enforcer/catalog.json`（及对应测试），不再生成 F#。
- npm pack 必须带上 `resources/`；缺资源 = 安装后无法启动。
- 资源损坏或 schema 不匹配导致启动失败，无静默降级。
- 文档与 RFC 不再承载规则正文 SSOT；规则正文以 JSON 为准。
- 未来若废除硬编码条数上限，校验应以 `N = rules.Length` 与连续 ordinal 为准，而非写死常量。

## Rejected alternatives

1. **从 spec/RFC 生成 F#** — 规则变更绑定编译与 PR 面；多产物漂移。
2. **把 JSON 复制到 `dist/`** — 双副本；build  staging 与包根不一致。
3. **代码内 fallback catalog** — 掩盖打包错误；运行时可能用过期规则。
4. **模块 import 副作用加载且可跳过校验** — 错误发生点不可控；残缺目录可进入业务路径。
