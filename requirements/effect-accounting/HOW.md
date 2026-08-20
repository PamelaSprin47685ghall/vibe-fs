# effect-accounting — HOW

## 架构机制与副作用状态投影

`effect-accounting` 统一管理跨外部系统的副作用生命周期：

1. **类型化事实流与编排时序**：
   - 业务流程严格执行“先意图事实、后物理调用、再确认事实”的执行顺序。
   - 状态投影将未匹配确认事实的记录维护为 `Requested`，匹配成功后跃迁至 `Created` / `Published` / `Accepted` 并永久锁定。

2. **PublishClaim 三分支判定**：
   `classifyPublishClaim` 在处理发布未决事实时，直接比对 Git 目标头部的物理快照：
   - `TargetHead = RebasedCommit` → 物理操作已完成，补发 `Published` 事实；
   - `TargetHead = ExpectedHead` → 分支未被篡改，执行原子推进；
   - 其它情况 → 目标引用已被并发修改，作废当前 Claim 并触发重试链路。

3. **未知结局（Outcome-Unknown）捕获与门禁**：
   底层存储写失败时抛出 `WriteUnknown`，上层门禁关闭后续调用准入并保留现场，由统一的崩溃对账机制根据外部物理见证裁决，禁止同进程进行盲目的就地重发。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| EFFECT-ACCOUNTING-001 | `requirements/effect-accounting/tests/effect-facts.test.mjs` |
| EFFECT-ACCOUNTING-002 | `requirements/effect-accounting/tests/join-missing-final-report.test.mjs` |
| EFFECT-ACCOUNTING-003 | `requirements/effect-accounting/tests/runtime-persist-order.test.mjs` |
| EFFECT-ACCOUNTING-004 | `requirements/effect-accounting/tests/blogger-request-materialized.test.mjs` |
| EFFECT-ACCOUNTING-005 | `requirements/effect-accounting/tests/reconcile-before-retry.test.mjs` |
| EFFECT-ACCOUNTING-006 | `requirements/effect-accounting/tests/write-unknown-explicit.test.mjs` |
| EFFECT-ACCOUNTING-007 | `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs` |
| EFFECT-ACCOUNTING-008 | `requirements/effect-accounting/tests/blogger-request-materialized.test.mjs` |
| EFFECT-ACCOUNTING-009 | `requirements/effect-accounting/tests/effect-facts.test.mjs` |
| EFFECT-ACCOUNTING-010 | `requirements/effect-accounting/tests/pre050-effect-marker.test.mjs` |
| EFFECT-ACCOUNTING-011 | `requirements/effect-accounting/tests/todo-accepted-precise-ref.test.mjs` |
| EFFECT-ACCOUNTING-012 | `requirements/effect-accounting/tests/effect-facts.test.mjs` |
