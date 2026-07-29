# STATUS/history-and-migration — 发布历史与迁移

## 0.4.0 发布历程

### RC 序列

| 版本 | 说明 |
|------|------|
| 0.4.0-rc.2 | 开发里程碑，Prompts/Authority 类型，后撤回 green claim |
| 0.4.0-rc.3 | 首个真实 RC，276 tests, 29 gate-testkit, 17 canaries ×3 |
| 0.4.0-rc.4 | 修复 Prompt Authority 问题，后被 rc.5 取代 |
| 0.4.0-rc.5 | 冻结版本，全角色 system prompt，session-wide A。277 tests |
| 0.4.0-rc.6 | ReviewConfirmation 关联修复。281 tests |
| 0.4.0-rc.7 | debounce + resolveForSession AABB。18 canaries ×3 |
| 0.4.0 最终 | rc.7 仅版本变化。281 tests, 29 gate-testkit, 18 canaries ×3 |

### 发布策略

默认私有交付：`private: true`, `license: SEE LICENSE IN LICENSE`。生成 tarball 但不公开发布到 npm。需要正式许可证和商业授权审查后才可改为公开。

## 0.4.x → 0.5.0 迁移

### 破坏性变更

- 所有 Agent 需要显式 `fast-*` 或 `deep-*` 名称
- `build`/`plan` alias 移除
- 模型绑定只读 `opencode.json`
- 模型环境变量全部移除
- 不持久化/覆盖 model ID
- Fallback 预算内循环 AABBAABB（Cursor 无限定义；自动恢复上限默认 12 连续失败）
- 不因 retry 次数杀死 Logical Run
- Blogger/Executor 为内部 fast/deep pair
- Pre-0.5.0 journal 不支持

### 迁移步骤（无自动迁移器）

1. 停止 OpenCode
2. 归档/删除旧 runtime journal
3. 删除全部模型环境变量
4. 在 `opencode.json` 配置 20 个 Managed Agent
5. 将所有调用中的旧名称改为 `fast-*` 或 `deep-*`
6. 修改 Manager/Orchestrator 调用——必须传 Agent
7. 重新启动
8. 检查 Config Gate + smoke test
