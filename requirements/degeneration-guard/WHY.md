# degeneration-guard — WHY

Provider attempt 可能在尚未正常结束前向两个方向退化：重复度过高时陷入单调模式，重复度过低时趋近无结构随机乱码。二者都应在污染 transcript 与浪费推理预算前被截断。

**degeneration-guard 保证：以本仓正常语料实际轨迹的经验分位数极值（低侧 97.5% 置信度，高侧 100% 经验最大值）作为唯一正常包络；流式输出越出包络即中断当前物理 attempt，并由 guard 自己在既有 TurnAborted reconcile 时点发送一次针对异常类型的接续。**

## 核心不变量与张力

- **语料经验分位数 vs 连续分布拟合**：正常性来自仓库语料实际轨迹的经验分位数（低侧 $p=0.025$，高侧 $p=1.0$，中央正常覆盖率 0.975），不拟合 Beta/正态等连续概率分布，运行期不引入动态置信区间或经验安全系数。
- **检测 owner = 恢复 owner**：guard 负责 `observe → classify → interrupt → reconcile → continue` 完整闭环；其它 turn/fallback/nudge 逻辑只看见“该 abort 已由 degeneration-guard 接管”，不得再次恢复。
- **固定记忆尺度 + Repository SSOT vs 特例森林**：detector 以 `256` 个 `o200k_base` token 为固定 half-life；每次 build 只从 Git tracked、strict UTF-8、正常人工可读的 source/document text 连续流即时派生正常 envelope。机器生成物、vendor、fixture 与结构化数据即使是 UTF-8 也不属于正常语料。normal prior 是 `X = mean(D_t(X))` 的唯一自洽解，不依赖任意启动 seed。运行期不按角色、模型、语言动态放宽边界，也不存在第二份可手调配置。
- **有界内存与严格绑定**：检测器状态仅保存有限 token 词表的最近出现步数，生命周期绑定单次 `ProviderRunIdentity`，禁止跨 attempt 复用。

## 违反边界的失败意义

- 高重复模式或近随机乱码持续消耗资源并污染历史记录。
- 把连续分布拟合或运行期可调统计阈值当成第二套规则。
- guard 中断后又落入普通 nudge/AABB，使同一失败出现两个恢复 owner。
- 极值或检测参数被运行期修改，或被复制进 tracked 生产源码。
- detector 内存随长 token 流无界增长。
- 进程内 armed 状态被写入 Journal 冒充 durable 业务事实。

## DEPENDS ON

- `host-boundary`
- `interaction-authority`
- `dispatch-protocol`
