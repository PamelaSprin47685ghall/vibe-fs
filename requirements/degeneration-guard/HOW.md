# degeneration-guard — HOW

## 架构与核心机制

### 直接语料包络

- `LoopDetector` 继续使用 `o200k_base` token 与指数衰减加权相异度 $D_t$；状态只有 `Normal | TooRepetitive | TooRandom`。
- half-life 固定为 `256` 个 `o200k_base` token。该值定义 detector 的语言记忆尺度，不从源码物理行长、formatter 或文件布局推导。
- build 每次直接从 repository SSOT 派生：`git ls-files` 给出 tracked paths；corpus helper 只接受正常人工可读 source/document 类型，拒绝 vendor/dependency、generated、fixture/golden 与结构化数据，再以 fatal UTF-8 decode 作为必要条件。入选文本按 path 顺序连接为单一连续 token stream。令 $D_0=X$，一次 token/history replay 递推 $D_t(X)=\lambda^tX+b_t$ 并保存 $b_t$，由 $X=mean(D_t(X))$ 直接解出 self-consistent normal prior，再只扫描 $b_t$ 计算当前 `minimum` / `maximum`。没有任意启动 seed，也没有第二次 token/history replay。生成文件只是 ephemeral runtime import，不是配置。
- 生产判定只比较 `D_t < minimum` 与 `D_t > maximum`。Beta、quantile、variance/std threshold 全部删除。
- `lastSeen[token]` 是唯一算法 scratch；每 token 更新 $O(1)$，空间受 tokenizer vocabulary 上界约束。

### Sensor-owned interruption + continuation

1. `LoopSensor.Observe` 只消费 Host text/reasoning delta，并为每个 eligible session 持有 fresh detector。
2. 首次异常把 `DegenerationKind` 写入进程内 armed map，然后调用 `InterruptAttempt`；后续 delta 因 armed 状态被忽略。
3. Host 原有 reconciliation 在 `TurnAborted` 分类点调用 sensor 的 consume operation。该 operation 原子取走 anomaly，并由 sensor 自己调用注入的 continuation port；因此 continuation 的时间位置与旧 abort classification 点一致。
4. continuation port 只负责物理发送：`TooRepetitive`/`TooRandom` 映射到各自 provider-language resource，并以 `PromptAuthority.DegenerationGuard` 发送同一 LogicalRun continuation。
5. consume 返回 typed `AbortCause.DegenerationGuard`；Application/Fission 收到后只 yield/no-op。没有 fallback ledger 写入，没有 nudge/AABB 第二恢复者。
6. abort 或 continuation 物理调用失败只回滚/记录当前 guard 的进程内状态；不得改道另一恢复协议。

## 依赖关系

DEPENDS ON:
- `host-boundary`
- `interaction-authority`
- `dispatch-protocol`

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| DG-001 | `requirements/degeneration-guard/tests/loop-detector.test.mjs` |
| DG-002 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-003 | `requirements/degeneration-guard/tests/loop-detector.test.mjs` |
| DG-004 | `requirements/degeneration-guard/tests/loop-detector.test.mjs` |
| DG-005 | `requirements/degeneration-guard/tests/loop-detector-memory.test.mjs` |
| DG-006 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-007 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-008 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-009 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-010 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
| DG-011 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs`, `requirements/interaction-authority/tests/continuation-origin.test.mjs` |
| DG-012 | `requirements/degeneration-guard/tests/loop-sensor.test.mjs` |
