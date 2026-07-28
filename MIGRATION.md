# 行为迁移总账 (MIGRATION.md)

## Event-Stagger 规则

第一个 canary 立即启动；canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）。

## 0.5.0 破坏性迁移（无自动迁移器）

当前开发轨：**`0.5.0-rc.1`**。从任何 `0.4.x` 升级必须手动完成以下步骤；**不要**尝试混用旧 journal、旧 Agent 名称或旧模型环境变量。

1. 停止 OpenCode。
2. 归档或删除旧 Wanxiangshu runtime journal。`0.5.0` **不支持** pre-0.5.0 journal；启动发现旧 schema 时直接失败。
3. 删除全部模型环境变量：`WANXIANGSHU_MODEL_A`、`WANXIANGSHU_MODEL_B`、`WANXIANGSHU_BLOGGER_MODEL`、`WANXIANGSHU_EXECUTOR_MODEL`、`WANXIANGSHU_AGENT_MODELS`。即使残留也必须完全不起作用。
4. 在 Host 最终 `opencode.json` 中显式配置全部 **20** 个 Managed Agent（`fast-*` / `deep-*`），并为每个 Agent 绑定非空且 pair 内互异的 `model`。
5. 将所有自定义调用、脚本、canary、工具参数中的旧名称（`manager`/`coder`/`reviewer`/`build`/`plan`/…）改为准确的 `fast-*` 或 `deep-*`。
6. 修改所有 Manager / Orchestrator / Inspector / Coder 调用：新建工作必须显式传 Agent；禁止省略。
7. 重新启动 OpenCode / 插件；确认 Config Gate 通过。
8. 检查启动日志一次出现：`Wanxiangshu 0.5.0 model configuration source: OpenCode config.agent`。
9. 运行 smoke / canary（至少 explicit-agent-only 与 fallback 12-round）。
10. **不要**混用 `0.4.x` journal 或 config；**不要**期望自动把旧 `reviewer` 猜成 fast 或 deep。

### 语义要点

- **配置 SSOT**：OpenCode 宿主最终解析后的 `opencode.json.agent`。万象术不二次读盘、不维护模型 catalog、不覆盖 Prompt `model`。
- **Agent 决定模型**：只向 Host 发送 `EffectiveAgent`；`Model = None`。
- **Fallback**：`A/A/B/B/...` 按 modulo-4 cursor **无限循环**。Provider retry **不会**因累计次数杀死 Logical Run。
- **公开工作**：必须显式选择 `fast-ROLE` 或 `deep-ROLE`。
- **内部**：`fast-blogger`/`deep-blogger` 与 `fast-executor`/`deep-executor` 不向 LLM schema 暴露；每个新内部 Logical Run 固定从 fast 起步。

完整冻结条文见 `next/Doc/SSOT.md` 与仓库根目录 `0.5.0.md`。
