# 最终验收标准

全部修复完成后，应满足以下可观察结果。

## 一、Esc

* Esc 后旧轮次发送 prompt 数量严格为 0。
* 迟到 zws/message.updated/busy/idle 不会解除取消。
* 单次 Esc 不错误标记为 Abort。
* 双次 Esc 后旧 continuation 调用数为零。
* `session.error` 缺失时仍能识别 Abort。
* 重复 idle 不产生重复状态转移。
* 旧 generation 的 message.updated 不恢复 session。
* 已派生但未发送的 action 被取消。
* 已经排队的 prompt 可被识别和失效。
* `Cancelled + EventHandlingActive=true` 必须返回不继续。
* `Cancelled + AwaitingBusy=true` 必须返回不继续。
* `Cancelled + BusyCount>0` 必须返回不继续。
* `Cancelled + Phase=Retrying` 必须返回不继续。
* Cancelled session 必须被视为 settled。
* 旧 continuation 的迟到 busy/idle 不能重新增加 BusyCount。
* Abort 空输出不会产生 EmptyOutputError。
* 缺少 assistant abort metadata 时 translator 仍可识别取消。
* 已生成但过期的 SendContinue 不会调用 Host。
* Cancelled 后所有旧 continuation lease 失效。
* 同一 Abort 被多个 Host event 重复报告时只取消一次。

## 二、warn 字段

* 每个适用工具 schema 都展示 `warn`、`warn_tdd` 或 `warn_reuse`，并以 description、examples 及 `x-wanxiangshu-soft-required` 元数据强调合规；不得使用 Host 强制执行的 `required`、`minLength` 或单值 `enum`。
* 缺失、空白或非规范控制字段时真实工具仍执行；最终结果保留原始输出并追加一次 `WANXIANGSHU_COMPLIANCE_REPRIMAND`，不改变 success、不触发 fallback，且告知 LLM 不要重复已成功调用。
* 下游参数中控制字段出现次数为 0。
* registry tool schema 中软字段 metadata 正确；没有把软字段放入 Host 强制 required/minLength。
* MCP 敏感能力被单独审计。
* 原地删除后真实 execute 收不到控制字段。
* 替换 `output.args` 的回归测试明确失败并被检测。
* 其他插件替换 args 时产生兼容性警告。
* malformed business args、权限/安全拒绝、解析失败或净化后控制字段泄漏时真实 execute 次数为零；软合规缺失不属于这些硬门禁。
* after 缺失不影响安全。
* 自有 wrapper 存在 clean assertion。
* 软合规缺失不产生 session.error 或 fallback；硬错误仍 fail closed。

## 三、context budget

* 精确阈值处稳定触发。
* token API 不可用时仍有保守触发。
* 一次 episode 只发送一次初始 budget nudge。
* 成功 todowrite 后 phase 正确重置。
* 最后一条 assistant 无 usage 时能找到最新有效 observation。
* 不重复累加 cache.read。
* 大型 tool result 在本轮估算中被计入。
* transform 新增内容被计入。
* limit=0 时进入保守默认。
* work backlog commit 后下一轮必然重建 phase。
* 同一 budget episode 不产生无界重复 nudge。
* compaction 后 context generation 正确变化。
* 第一轮无论 CAPS 多大都不触发 emergency todowrite。
* 无真实 token usage 的 bootstrap 阶段不触发。
* 达到真实增长阈值后能正确触发。
* investigator 永远不会收到 todowrite emergency。

## 四、日志

* 默认运行 stdout 中 `DEBUG:` 数量为 0。
* 敏感 prompt、工具参数不进入日志。
* stderr 不包含普通能力探测。
* debug=false 时没有 debug 级日志。
* debug=true 时结构化字段完整。

## 五、compaction/fallback

* compaction 完成后的普通 nudge 数量为 0。
* fallback episode 中普通 todo/review nudge 数量为 0。
* 每个终止事件只有一个 owner。
* `experimental.session.compacting` 打开 block。
* autocontinue planned 时普通 nudge 为零。
* summary assistant 不被视为真人完成。
* synthetic continue 不经过 chat.message 也能识别。
* `session.compacted` 不立即解除 block。
* auto-continue settle 后才解除。
* compaction Abort 时 cancellation 优先。
* 用户输入相同文案不被误判。

## 六、模型

* todo/review nudge 使用当前真人轮次模型。
* 旧 fallback injected model 不影响新真人轮次。
* variant 和 reasoning 设置不丢失。
* 真人 user model 成为 HumanTurnRoute。
* `chat.params` 按 owner 分类。
* compaction/title 不污染 human route。
* nudge 请求显式携带 model/variant/agent。
* fallback attempt route 不跨 turn。
* 字符串、User object、Assistant flat fields 均可解析。
* 旧 generation observation 被丢弃。
* Host 实时查询结果与当前 turn 不匹配时不得覆盖。

## 七、review task

* 每条 review nudge 都包含完整 `original_task`。
* active loop 缺 task 时不发送任何失忆 prompt。
* compaction、重启、needs revision 后原任务保持不变。
* review nudge 只包含 `original_task`，不重新生成激活字段 `task`。
* task 只存于权威 ReviewProjection。
* snapshot 正确派生 active task。
* prompt 使用 `original_task`。
* compaction 后任务仍可恢复。
* review nudge 不会被误识别为新的 loop activation。

## 八、跨 Host 一致性

OpenCode、Mimocode、Mux、OMP 对同一组输入断言：
* warn 缺失不拒绝；
* todo 报告短不拒绝；
* 工具执行后结果保留原文，并在所有结果规范化完成后追加一次一致的批评；
* 控制字段在历史中恢复；
* investigator 有 CAPS；
* investigator 有并行提示；
* 开局不出现 emergency todowrite。

## 九、最重要的取舍

三个原则保留：
* 宁可少自动继续一次，也不能违反用户 Esc。
* 宁可少发一次 nudge，也不能在 compaction/fallback 后重复抢控制权。
* 宁可因状态缺失阻止 review continuation，也不能让 LLM 在没有原始任务的情况下猜。

真正要删除的不是某个零宽字符，而是整个系统对"根据文本、时间和最近消息猜测状态"的依赖。只要 provenance、generation、owner、routing context 和 durable projection 建立起来，这 7 个问题会一起消失。
