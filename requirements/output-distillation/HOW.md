# output-distillation — HOW

## 架构机制

### 蒸馏管线与失败降级

1. **分块映射与归并**：消费流式落盘的 `spool` 文件，以固定字节大小切分 chunks。对每个 chunk 启动私有的 fast 档位 Distiller 进行映射，随后按扇入上限在线分层归并（reduce）。
2. **失败降级策略**：若某一分块的 Distiller 失败或超时，触发 `cancelOwned` 取消关联的内部代理；调用 `partialWithTail` 构造不完整摘要并拼接最后分块的 `raw_tail` 原始文本，保留定位线索，拒绝虚构成功。
3. **私有运行时生命周期**：Distiller 子会话被标记为 `HostOwnedHidden`，生命周期由宿主完全管控并在调用结束同步收口，对外部角色与委托列表完全隐藏。

### 大输出门禁与确定性留尾截断

- **Large Gate 互斥**：对于估算产出大体积日志的进程，执行前必须获取单持有者的 `LargeGate`。未获取到门禁的执行请求在 FIFO 队列中排队，确保全系统同一时刻仅有一个大输出流占用内存与分析资源。
- **留尾截断（ToolResultBound）**：插件工具回传长文本时，在达到宿主全局限制前完成留尾截断。注入固定的截断提示标记并优先保留最新的完整尾部行，消除宿主默认头部截断导致最新日志丢失的不确定性。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| DISTILL-001 | `requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-002 | `requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-003 | `requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-004 | `requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-005 | `requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-006 | `requirements/output-distillation/tests/executor-summarize.test.mjs` |
| DISTILL-007 | `requirements/output-distillation/tests/executor-summarize.test.mjs` |
| DISTILL-008 | `requirements/output-distillation/tests/executor-summarize.test.mjs` |
| DISTILL-009 | `requirements/output-distillation/tests/distiller-role-contract.test.mjs` |
| DISTILL-010 | `requirements/output-distillation/tests/distiller-role-contract.test.mjs` |
| DISTILL-011 | `requirements/output-distillation/tests/large-gate.test.mjs` |
| DISTILL-012 | `requirements/output-distillation/tests/tool-host-codec-full.test.mjs` |
| DISTILL-013 | `requirements/output-distillation/tests/executor-summarize.test.mjs` |
