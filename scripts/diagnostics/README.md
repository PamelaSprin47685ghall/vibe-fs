# 诊断工具套件 (Diagnostics Suite)

本目录提供面向万象术内核与 OpenCode 运行时的自动化诊断工具，用于在系统异常或崩溃时精准固化事故现场。

## 1. 事故现场采集脚本 (`collect-incident.mjs`)

### 运行方式
```bash
# 自动探测异常标签采集
npm run diagnostics:collect

# 指定自定义事故标签采集
npm run diagnostics:collect -- "crash-on-admission"
```

### 输出规范
采集结果自动落盘至当前工程 `.diagnostics/` 目录（非 Git 仓库环境则存至 `~/.local/state/wanxiang/diagnostics/`）：
```text
.diagnostics/incident-YYYYMMDD-HHmmss-[Tag]/
├── summary.md            # 事故全景诊断报告（核心指标、案情卡片与日志切片）
├── metadata.json         # 结构化事故元数据（RunID、Session、Commit、OS环境）
├── run-scoped.log        # 异常运行周期的完整运行时日志（排除历史噪点）
├── events-tail.json      # 崩溃前万象术内核最后 10 条状态机跳变事实
├── latest-events.ndjson  # 崩溃现场的完整事件账本原始快照
├── wanxiangshu.mjs       # 当时生效的模型调度策略快照及语法校验结果
├── opencode-config.json  # 脱敏后的 Provider 与 Agent 配置快照
└── system-crash-*.ips    # 近 24 小时内的操作系统核心崩溃转储（macOS .ips）
```

## 2. 采集指标与安全保障
- **精准生命周期切片**：根据 `run=<runId>` 精确圈定发生故障的进程执行范围，避免整文件或固定行截取造成的失真。
- **事故特征提取**：自动提炼崩溃时的 `sessionId`、`agent`、`providerID`、`modelID` 与错误信息。
- **机密数据脱敏**：采集 `opencode.json` 时自动对所有 `apiKey` 实施掩码处理（仅保留前 4 位和后 4 位）。
- **版本控制隔离**：事故产物目录 `.diagnostics/` 已配置于 `.gitignore`，严防测试与排查数据污染代码仓库。
