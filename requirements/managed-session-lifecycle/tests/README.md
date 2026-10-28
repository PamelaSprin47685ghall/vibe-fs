# managed-session-lifecycle 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录包含受管会话、子会话生命周期、恢复重放及作废结算的测试。

## 测试覆盖重点

- **001/005**：调用生产 AttachedSessionRuntime，验证同 owner 资源重用、角色绑定保持与显式移除后重建。
- **002/003/011**：验证卫星会话关联、单次创建、冲突拒绝与替换。
- **004/009/014**：验证同步委托过程中的状态流转与 Journal 记录。
- **006—010/015**：验证状态折叠、编解码、快照投影及冷重放一致性。
  - 006/007 验证裸 link 的幂等重放与完成状态保护。
  - 008 验证消费墓碑（HandleWorkConsumed）提交未确认时不交付 payload，防止二次消费。
  - 013 验证跨实例并发消费时的 CAS 仲裁机制。
  - 015 验证重启后根据 durable 投影恢复现有 child 会话，避免不必要的新建。
- **016/017/018**：验证受权取消与异常终止的区分。受权取消记录 Abandoned；非授权终止（如提供方重试、turn abort）保持 Active，重启后复用同一 handle。
- **019**：验证会话删除与取消时的完整执行排空（drain），等待租约归还后再清理注册表。
- **025**：验证 DevOps 会话在 PTY 端口边界上的终止与资源清理。
- **029**：验证父 Join 在 lost-wake 或 session.idle 缺失情境下的可靠唤醒。

## 运行方式

先执行标准构建：
```sh
node scripts/build.mjs
```

运行测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/managed-session-lifecycle/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
跨插件集成用例需启用 `WXS_TIER_INTEGRATION=1`。
