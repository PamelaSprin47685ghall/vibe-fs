# 兼容旧事件日志

## 一、原则

新增事件时不要破坏旧 `.wanxiangshu.ndjson`。建议提高事件 schema version，并提供纯迁移规则。

## 二、对旧 fallback 事件

旧 `fallback_continue_injected` 没有 continuation ID 时：
* 可以恢复"曾存在一次 fallback attempt"；
* 不能据此解除 cancellation；
* 不能据此覆盖当前真人模型；
* 重启后若状态不明确，保守结束旧 episode，不自动发送新 prompt。

## 三、对旧 assistant snapshot

缺少 humanTurnId 时：
* 可用于展示；
* 不可作为 nudge model 的高优先级来源。

## 四、对旧 review loop

如果 `loop_activated` 中有 task，恢复到 ReviewProjection。

如果 active 状态没有 task：
* 标记损坏；
* 不发 review nudge。

## 五、对旧 budget state

没有 phase ordinal/generation 时：
* 从当前上下文建立新 phase；
* 不沿用全历史 todo 数量；
* 首次恢复时执行一次保守测量。

## 六、迁移规则要求

事件结构变更每条携版本号，旧版逐级升级转最新语义。升级函数纯且幂等，不读时钟不碰网不依赖环境。否则同一历史不同时间重放出不同世界。

## 七、物理载体

NDJSON 一行一个自包含事件，追加只碰末尾，恢复逐行读取折叠。普通 JSON 数组追加要改已有结构，风险和语义都错。恢复时首行损坏应在损坏处截断，不跳过后续行。事件前后相扣，缺了中间后续事实就建在错基上，宁可少恢复一步，不恢复矛盾态。

历史变长格式演化机器故障需要少而硬的约束：快照只是书签非真理，要记录已提交事件计数、最后提交序号和完整状态前缀。恢复按计数与提交序号校验连续性，对不上就弃快照从头重放，不靠内容 hash、文件大小、字节数或修改时间猜测对齐。

生产环境每个 workspace 使用一个 NDJSON journal，事件携带 `sessionId`；进程内由该路径唯一的 `JournalWriter` 串行追加，跨进程再用排他锁防止并发写入撕裂历史。按 session 过滤只产生 projection 视图，不创建独立事实日志。测试用例使用独立 workspace，因此天然隔离。

## 八、审核验证

旧事件重放后：
* fallback 事件不能解除 cancellation；
* 旧 assistant model 不能覆盖 human turn route；
* 旧 budget phase 不沿用全历史 todo；
* 旧 review loop active 但无 task 标记为损坏；
| 迁移函数纯且幂等。
