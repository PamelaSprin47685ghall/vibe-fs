- 目前的错误行为: investigator agent 不注入 caps
- 正确的行为: 也要注入

- 目前的错误行为: 某回合调用了单个工具没有成功触发 user hint
- 正确的行为: 提醒 llm 并行调用工具

- 目前的错误行为: warn warn tdd warn reuse 在 tool before 中删除后再也没有了
- 正确的行为: 在 tool after 中恢复

- 目前的错误行为: warn warn tdd warn reuse 如果 llm 没写就严正拒绝
- 正确的行为: schema 强调必须写，但假如真的不写，也没事，但在工具返回时狠狠批斗 llm 一顿

- 目前的错误行为: todo 每项写不够 1024 字就拒绝
- 正确的行为: schema 强调必须写够，但假如真的不写够，也没事，但在工具返回时狠狠批斗 llm 一顿

- 目前的错误行为: 紧急要求 todowrite 的提醒几乎在对话一开始必然触发
- 正确的行为: 去除 bug 并在正确的时机触发。可能是对话开始没读到 llm context 或者计算错的 bug?

请你给出保姆级别修复方案，但不写代码。
