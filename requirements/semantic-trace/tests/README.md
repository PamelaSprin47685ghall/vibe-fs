# semantic-trace 测试说明

行为规范见 [WHAT.md](../WHAT.md)。本目录包含从 Host wire 解码到 XTrace 持久化日志、工作记录物化与交付记录各环节的测试。

## 测试覆盖重点

- **001**：驱动真实临时目录与日志格式，核对首段（Opening）、交互部件（Parts）与终结（Terminal）的单调写入、跨进程同键幂等与冲突拒绝。
- **002**：通过真实 Host 解码与捕获验证 typed retry 识别、相同措辞的普通任务保留、元数据不进入正文，以及缺失/空白/重复身份与 legacy trace 时的严格校验。
- **003**：验证游标（Cursor）单调递增并与 Host 轮次解耦，验证并发写入时的串行分配与回退拒绝。
- **004**：验证溯源按 ProviderRun 分段，验证重锚后的新 generation 与历史轮次隔离。
- **005**：验证语义渲染的一致性，验证区间换算为 `wanxiangli/trace` 的 Cut 切片、SHA-256 哈希链维护、分片部件桥接与私有状态隔离。
- **006**：验证基于 Host 稳定身份与半开区间的连续切片，验证缺失、乱序或游标缺口时的 fail-closed 裁决。
- **007**：验证日志追加后关闭并重开的原文核对及重复捕获处理。
- **008**：验证投机候选拒绝与失败探测回退不污染持久 XTrace。
- **009**：验证上下文压缩与重锚只推进 epoch，不删除已有 XTrace 记录。
- **010**：验证 Opening 区间不被普通工具材料滤除、重放幂等与冲突改写拒绝。
- **011**：验证多 lane 并行分流（Fission）溯源部件的因果汇聚与统一终结前沿。
- **012**：验证跨 Resume 边界的独立 InvocationStartCursor/EndCursor 范围隔离。
- **013**：驱动真实日志与事实折叠表面，验证交付记录按三元组与完整围栏读取、材料冲突拒绝、终态判定与幂等重放。

## 运行方式

先执行标准构建：
```sh
node scripts/build.mjs
```

运行测试：
```sh
TESTS_MJS_FILES="$(rg --files requirements/semantic-trace/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```
