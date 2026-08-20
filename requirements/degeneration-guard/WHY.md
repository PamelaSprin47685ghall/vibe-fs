# degeneration-guard — WHY

Provider attempt 可能在尚未正常结束前陷入高重复、低信息的退化循环生成。若不及时截断，会持续污染 transcript 尾部并推迟进入正常恢复。本包在污染扩大前将该 attempt 主动终止，并将其桥接至标准的 `provider-attempt-recovery`。

**degeneration-guard 保证：输出退化循环在流式阶段被纯传感器及早发现并截断，且强杀动作仅作为一次已确认失败桥接回标准恢复机制，不成为平行的重试控制器。**

## 核心不变量与张力

- **纯流式传感器 vs 业务控制器**：检测器仅基于 token 序列维护加权相异度指标，不把 delta 文本拼装为业务事实，不自发做业务决策。
- **固定参数滴定 vs 特例森林**：检测参数在构建期从仓库语料纯粹重放滴定并固化在构建产物中，严禁在运行期按角色、模型或语言动态放宽阈值。
- **有界内存与严格绑定**：检测器状态内存有界（仅依赖有限词表），生命周期严格绑定单次 `ProviderRunIdentity`，禁止跨 attempt 复用。

## 违反边界的失败意义

- 严重退化的循环输出持续消耗资源并污染历史记录。
- 检测器绕过 FallbackController 自行修改 Offset 或重试状态。
- 滴定参数被动态修改或侵入 tracked 生产源码。
- 检测器内存随长 token 流无界增长。
- `LoopKillArmed` 被写入持久化 Journal 冒充恢复协议。

## DEPENDS ON

- `provider-attempt-recovery`
- `host-boundary`
