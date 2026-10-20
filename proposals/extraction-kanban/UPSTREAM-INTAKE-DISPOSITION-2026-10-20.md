# 上游增量语义处置表（2026-10-20）

施工状态及证据见 [计划](UPSTREAM-INTAKE-2026-10-20.md)。本表是待落实的处置，不是已实现证明；实际产品合同由对应 WHAT 拥有。

| ID | 差异与来源 | 本批处置 | 包 | 验收及剩余边界 |
|---|---|---|---|---|
| I01 | 0932e0c08 的 candidateDigest | 接收完整输入摘要，成功/失败结果均带候选身份；保持变化拒绝 | M1 | verification-system-016；摘要不是全部不可变性或发行成功证明 |
| I02 | 1d9c60d47/7b29295b5 的动态 ToolResultBound | 接收 Host 配置，预算调用方共用实际上限，最终编码仍受限 | M2 | host-boundary-015、process-execution-015；不以采集容量代呈现预算 |
| I03 | 09493c4e9 的 call/Callable | 公开入口、错误码、实现命名、示例一次迁移；保留本地执行内核 | M3 | 旧活动运行先按原协议收束；历史代码文本不改写、不自动重播 |
| I04 | 584c30c61 的 sanity 数组协议 | Host 实际调用 next/watch；调用纳入现有受限 VM、绝对期限与取消 | M3 | repository-programming-050/051/052/058；真实实例重新构造，失败零启动；不宣称通用恶意 JS 隔离 |
| I05 | 6eef89d1e 的异步 rg | 常用 JSON 内容及路径列表接入已有 owner/进程监管，不单建 spawn 管理器 | M4 | 当前调用剩余期限、预取消零启动、实际关闭/排空、真实 rg 正反例 |
| I06 | rg 参数透传与结果混型 | 明确支持选项及 arity；根内路径；禁继承配置/外部执行/越界；非路径模式明确拒绝 | M4 | content/messages 与 paths 分开，NUL 路径；count/vimgrep/type-list 等延后 |
| I07 | 0932e0c08 的 Wave 2 测试 | 接有效新增断言，复用本地更强物理证明；测试标题与实际范围一致 | M5 | 同进程两对象不证明 OS 重启；投影 abandon 不证明持久化或资源排空；旧 GAP 保留 |
| I08 | 多提交的 Blogger/Chronicle/Y/WorkRecord/Replica/grounding 删除 | 延后；不整体 cherry-pick 混合提交，不复制大规模删除或格式变动 | 全部 | 现存职责与测试保留，不清空 .Herdchestra |
| I09 | 库 b2c3ccb/07200be/a8b89a4 删除公开入口及身份解析 | 库固定 05f6b5a、16 入口；历史身份与只读墓碑继续读取 | M5/M6 | Attachment、冻结 cut、原始 U/Trace 与存量日志不变；无旧程序重放 |
| I10 | deliver(value, view?)、view 去重及退役接线 | 本批不接；保留本地显式终值及当前 Advisor 寿命合同 | 全部 | 不把 rg/call 接收扩大为第二条迁移主线 |

## 已知问题记录

| ID | 问题 | 影响与处置 | 状态 |
|---|---|---|---|
| E01 | 上游 crash-reconciliation-001 以两个 gate 代称真实进程重启；durable-events-021 以 dispose/create 代称跨进程 | 可保留局部断言，不能替换本地真实冷恢复证据或关闭 GAP-149 | 留档，M5 按实际证明范围接收 |
| E02 | 上游 managed-session-lifecycle-023 的 handle abandon 不证明耐久退役或资源排空 | 保留身份/不复活局部证明，旧物理验收继续存在 | 留档，父 GAP 不变 |
| E03 | 完整 release 的既有 5000ms verdict-silence 失败及旧 TODO | 前批不通过；本批最终如实复核，不放宽门禁 | 既有边界 |
| E04 | Node26系统二进制被工具夹具单独复制后找不到动态libnode，完整016套件27失败 | 日志保留；已有官方完整Node22另验，安装器实现不扩修 | 环境边界，非摘要接口通过证明 |

## 实际落点

I01的完整摘要与输入变化拒绝已做定向2/0。I02已进入生产配置与实际结果预算，最小结果在Host容纳不了时启动前拒绝；gen24正式25/0、排空回收通过。上述只证明各自限定范围，M5真实Host及M6统一候选尚待。
