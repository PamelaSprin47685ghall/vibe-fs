# prefix-stability 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录验证模型出站请求前缀的逐字节稳定性、冷边界重锚与标记注入协议。

## 测试覆盖重点

- **前缀追加律与 epoch 推进（001, 002, 006）**：验证同一 epoch 内请求前缀逐字节不变；验证仅通过 `ScopeReanchored`、`ContextReanchored` 与 `TenureReanchored` 正确推进 epoch 并重置快照。
- **System Prompt 稳定与语言切换（007）**：验证同一 Life 内 system prompt 保持一致，仅全局语言变更允许重建前缀。
- **消息身份与重试行（009）**：验证基于稳定 Host 消息身份定位，未重锚前保留历史重试行，新重锚边界按身份退休旧行。
- **标记注入与原位重放（010, 014, 016）**：验证工具结果倒计时后缀（`[Remaining turnBudget: N]`）首次呈现后锁定自身字节，后续预算递减不回写历史；验证标记不进入业务 trace。
- **比较范围与漂移防护（011, 013）**：验证 provider、model、tools、system 变更作为冷边界处理，禁止随意切换 epoch 掩盖前缀漂移。

## 运行方式

执行本包测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/prefix-stability/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
