1. 用户按 Esc 强制退出，但受到 fallback Unicode 的零宽的干扰。
2. warn: warn-tdd: 等几个字段没有强制 llm 加上，同时也没有稳定强制在下游之前剥离。
3. 本来 todowrite 按照数学在给定上下文长度处要求要求 llm 触发，但这个机制不工作。
4. 代码和控制台输出很多 DEBUG:
5. e2e 测试不能正常运行，超时严重
6. 压缩后错误地触发 nudge，其实如果系统自动继续则不 nudge
