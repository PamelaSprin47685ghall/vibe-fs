# degeneration-guard — 存在理由

## 一句话 WHY

provider attempt 可能在尚未正常结束前进入高重复、低信息的退化生成；若不提前截断，会持续污染
transcript 尾部并推迟进入正常 recovery。本包在污染扩大前把该 attempt 主动终止，并桥接到标准
`provider-attempt-recovery`。

## 为什么这个 WHY 不可替代

「输出已经退化成循环」是一个**流式过程中的病理**，不是一次失败（`provider-attempt-recovery`），
也不是崩溃（`crash-reconciliation`）：attempt 还在跑，物理请求还没结束，但继续跑只会浪费时间、
污染历史、延迟恢复。需要在 attempt 内部、文本流边上放一个**纯传感器**——它只读取窄 token 多样性特征，
不把 stream delta 积分成业务事实，不成为第二套 retry controller，命中时只做一件事：停止当前
物理 attempt，然后交回标准 recovery。

## 世界什么时候 RED

- 明显退化的 attempt 可以无限污染历史（没有止损）；
- detector 自己成为新的业务 truth / retry controller（绕过 FallbackController 直接改 Offset、
  直接发 probe/squash）；
- detector 按角色/自然语言动态放宽阈值（不可测，特例森林）；
- calibration 把派生数值写回 tracked source，导致 build 改工作树、源码 mtime 与语料集合互相污染；
- production source 持有某次仓库快照的 calibration 数值，而不是由当前构建输入生成唯一运行产物；
- detector 的内存随流长度增长（不是 bounded）；
- detector 状态跨 attempt 复用或泄漏（生命周期没有绑定单次 ProviderRun）；
- LoopKillArmed 写进 Journal 或日志当恢复协议（它不是 durable 状态）。

## 与相邻包的边界

| 看似邻近的事实 | 归属 | 为什么 |
|---|---|---|
| 命中后的 retry cursor / budget | `provider-attempt-recovery` | 本包只发「止损信号」，不拥有预算 |
| 崩溃后临时状态丢失 | `crash-reconciliation` | LoopKillArmed 崩溃丢失是安全侧，不是恢复输入 |
| transcript 语义真伪 | `semantic-trace` / 各 domain owner | detector 不拼装业务事实 |
| 恢复槽是否 armed / 压缩产物 | `context-compression` | 本包桥接后由 recovery 决定 |
| 用户主动 abort | `effect-accounting` / `interaction-authority` | 用户中止与清理中止不得自动 AABB |
| NEEDHELP 传感器 | `interaction-authority`（WATCH） | 两个传感器分离，一个 abort 一个 cause |

## 本包不拥有的（DOES NOT OWN）

retry cursor/budget（`provider-attempt-recovery`）、transcript semantic truth、任何特定 detector 算法必须永久存在、
arbitrary quality judgement（不是「质量评分器」）。
