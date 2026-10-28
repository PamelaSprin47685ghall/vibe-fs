# effect-accounting 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录包含意图、物理确认与未知结果三态记账的测试。

## 测试覆盖重点

- **001**：驱动真实工作区投影与事实编解码，验证意图（Requested）与物理确认（Accepted/Created/Published）的强类型分型，验证混用或类型不匹配时的编译与运行期防御。
- **002**：驱动 Fork 生命周期，验证缺失有效报告时保持 pending 状态，验证随后真实有效结果完成同一 cell；验证 Requested-only 不盲目重发。
- **003**：在真实分派器的 Host 入口重开磁盘日志，验证 exact PromptKey 的意图可恢复，验证已关闭句柄阻止发送，验证先记账后行动的物理调用顺序。
- **004**：验证工作区物理确认不回退，重复确认保持幂等。
- **005**：验证恢复流程对不可读、无有效记录、错误 PromptKey 或角色不符保持未决状态，仅在正确物理事实匹配时结算。
- **006**：验证 Host 产生副作用但返回 acceptance unknown 时，日志重开后保持待决核对，不盲目假设已发生或未发生。
- **007**：验证取消信号的观察与终态解码，确保取消信号不被包装为完成终态。
- **008**：验证 Assume 运行时的提交顺序与原子 canvas/todos 投影，确保各效果家族独立维护事实。
- **010**：验证编解码器严格拒绝旧版通用事实并接受现行强类型事实。
- **012**：驱动发布流程与受控物理 port，验证变基（`RebasedCandidateReady`）、重新评审、Claim 和物理更新的严格时序，缺少前置确认事实时拒绝发起发布 Claim。
- **013**：验证交付记录（`TaskDeliveryCommitted`）与效果记账分型隔离，交付事实不替代副作用确认，缺失交付事实的终态判定为未交付且不发布完成。

## 运行方式

先执行标准构建：
```sh
node scripts/build.mjs
```

运行测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/effect-accounting/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
