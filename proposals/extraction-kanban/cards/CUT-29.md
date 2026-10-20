# CUT-29：Strength / 投机调查去向 —— 裁决

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-29)。

- **裁决人**：eng-cut09g-goose，2026-10-21，按仓主“完全自主决定”的授权。
- **裁决**：逐项裁决，不一揽子造“智能调查引擎”。Strength/投机调查已由仓主删除（REMOVED，不记为抽取成功），本卡清掉死残留；Warm-start 纯规则抽取；Semble MCP 传输与只读调查契约留主仓（RETAINED）。

## 依据（亲眼核对，master d57f886c5）

| 部件 | 现状 | 真实消费者 | 裁决 |
|---|---|---|---|
| 估计、预算、Replica、Strength 委派链（`src/Wanxiangshu/Strength/*`、`PluginStrengthPorts`、`StrengthSurface`）与 `requirements/speculative-investigation` | 已由仓主 `6eef89d1e`（retire … Replica）整目录删除 | 无 | **REMOVED**（仓主已删，非本卡抽取）。死残留：`ToolRegistry.Path.DeniedStrength` 与 `resources/provider/tool/registry/denied-strength/*` 无任何读取者 → 本卡删除 |
| 只读调查契约（repository-investigation WHAT 001–005） | 角色权限 + 提示约束，无运行时算法 owner | 角色/工具注册 | **RETAINED**：是产品规则与权限表，不是可抽算法；继续由 WHAT 与现有测试守护 |
| Warm-start 纯规则（WHAT 007/009：关键词规范化、每词 top-k、稳定去重、总条数上限、整条删减到字节预算、省略计数；WHAT 008 直接消费者角色） | `Repository/Investigation/WarmStart/Prompt.fs` 手写 | `Fork/OpenCode/Tool.fs` 经 `RepositoryWarmStart.appendToBaseDocument` | **抽取**（子范围①）→ `wanxiangli/warm-start`；宿主只留 Semble 调用、LlmFacing 渲染与语言资源 |
| Semble MCP 客户端（`Semble/{Client,Mcp,Stdio,SearchCodec}`） | 进程/stdio 传输与外部工具输出解码 | Warm-start | **RETAINED**：宿主 I/O 与外部协议适配，fail-open 已有测试（002/009） |

- 未裁决消费者：CUT-13（委派）尚未 DONE；子范围①不改 `RepositoryWarmStart` 对 Fork 的公开签名，写集合与 CUT-13 不相交。整卡 DONE 待 CUT-13 核对 Fork 接线后确认。

## 子范围

1. **Warm-start 选择与预算律**（DOING）：`normalizeKeywords`、命中→提示条目的选择（每词 top-k、按关键词序 + 局部名次、`(path,start,end,content)` 稳定去重、总数 24 上限、省略计数）、整条删减到 64 KiB（宿主提供量度函数）抽入 `wanxiangli/warm-start`。反例：大小写不同的关键词被并掉；空白行触发搜索；超限时截断半条；重复命中挤掉别的关键词；省略计数漏算去重项。
2. **死残留**：删除 `DeniedStrength` 键与两份资源。

## 不做

- 不恢复任何 Strength/Replica 功能；不改 Semble 启动与环境变量；不改提示资源文本。
