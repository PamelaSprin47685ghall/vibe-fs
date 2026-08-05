1. 新功能: 允许 join 的过程等待中被新的 user 消息打断，此时 join 的返回值是一个特殊值，表示优先处理新的 user 消息而不是继续等待。
2. 修改提示词: [sub-session 复用] 让 orch/manager 优先考虑复用已有的 sub-session 而不是 fork 新的 sub-session，这样可以利用前缀缓存。
3. 修改格式: sub-session tools/join 返回值里面，work_record 字段不再作为 toml 的一部分列入，而是作为注释放在开头，因为属于 parent 可执行的 instruction-like 内容。
4. 如果 blogger 不调用工具，不视为网络错误换 AABB，而是走 nudge 机制（就像 review nudge 的实现），仅当 nudge 彻底失败以后才走 AABB 机制。
5. orch/manager 调用 join 的时候，不仅仅给一个结果，如果有积压的结果，允许在一个 join 结果中一次性打包发送。
6. 目前的 enforcer 是 bool 一堆编码的，这不好，改为每次恰好提一个意见叫做 tip。enforcer 可以看到自己之前每轮提的 tip 是什么 [放进工作记录里面一起格式化进去]，用提示词要求最近太密集的建议不要重复发，注意多样性，不要唠叨，但犯的严重或者又犯了也可以反复提醒。是一个参数，参数名是 tip，是一个枚举，枚举值是这 120 种选一个，不能不选。
7. 调用 coder 需要加一个参数 tdd, 取值是枚举 red 或者 green 表示这个修改是 TDD red 阶段还是 green 阶段，required 必填，而且工具说明是必须用 TDD 方法开发。
8. 每次 transform 最后 [最后一个 user 消息或者 tool result 消息的后面] 加一个伪造的 assistant 思考 "让我遵循结对编程的理念，用中文进行对话式思考。" 这样可以让 assistant 更指令遵循。
