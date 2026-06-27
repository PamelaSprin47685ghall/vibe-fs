1. 将所有的 subagent-like 工具的提示词和描述中不再体现 subagent 说法，防止 llm 认为慢而不积极调用
2. reviewer PASS 允许带意见, 返回的工具结果的 feedback 字段直接写进 markdown 正文而不是 front matter
3. 把 with-review 模式提示词增加更严厉的要求，要求把用户需求做到不能再做为止，严禁缩水
4. 目前 opencode 端 revert 对话，被 revert 掉的部分中的 with-review /loop 仍然触发，与历史记录唯一真理的假设矛盾
5. mimo 端目前泄露了 plan 和 memory 相关工具，这两个对任何 agent 禁用
