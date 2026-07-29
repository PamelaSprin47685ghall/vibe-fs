# STATUS/plan-sidecars — 未来功能：Sidecar/Enforcer

本文件内容尚未进入当前 0.5.0 产品轨。 仅作规划参考。

## Sidecar Supervisor

每个被观察的工作 session 可以绑定两个长期 sidecar：

- Prefetcher sidecar — 快速模型，唯一工具 `prefetch(paths)`。预测主模型即将需要的文件，宿主读取后注入。
- Enforcer sidecar — 快速模型，唯一工具 `command(prompt)`。根据编程原则决定是否纠偏。

## 核心原则

1. Sidecar 调用不阻塞主模型
2. 主模型历史一旦发送就不再修改
3. Synthetic ID 和内容确定性生成
4. Prefetch 持久事实存储在旁路账本
5. Enforcer 是软纠偏，不取代工具层硬约束

## 实施顺序

1. 基础设施（canonical view, semantic delta, sidecar binding）
2. Prefetch shadow（只记录不注入）
3. 确定性 overlay
4. Compaction / rewind
5. Enforcer shadow
6. Enforcer command
7. 扩大 session allowlist
