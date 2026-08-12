# tool-error-ignored — Main

把 tool failure 重新连接到它所否定的 premise。

对每个 tool call 先声明用途：它是在获取 fact、执行 mutation、建立 verification、还是做可选 enrichment？只有这样，failure 才能有正确 policy：stop、retry、recover、degrade、mark unknown，而不是统一 `catch/log/continue`。

常见 repair：

- fact query fail → 返回 `Unknown/Unavailable`，不能当 empty/not-found；
- mutation fail → 后续不得假设世界已改变；
- verification fail → completion claim 降级或 work 继续；
- optional enrichment fail → 明确保留 core result，并记录 enrichment unavailable；
- transient effect fail → 只有 idempotency/unknown-outcome protocol 允许时才 retry。

常见假修复：

- catch everything，返回空数组/null/default；
- log error 后继续 success branch；
- tool fail 就自动 retry，不区分 non-idempotent/known-invalid；
- 把 nonzero exit 当 warning，因为“别的测试都过了”；
- failed grep/read 当成“仓库里没有这个 symbol”；
- mutation command 输出 error，却仍跑 verification 并把旧文件 green 当新代码 green。

验证要故意让 tool 失败，并观察**下游 claim 是否被正确收回**。Query failure 不得变成 negative fact；write failure 不得产生 success event；test failure 不得进入 verified completion；optional telemetry failure也不能把整个产品 result 误判失败。

Error handling 的核心不是“所有 failure 都 fatal”，而是保持 epistemic integrity：知道什么、做成什么，只能由实际 outcome 决定。

如果某个 tool error 的协议现在只能靠 parsing prose，先解决 typed identity/stringly error；如果 failure 可能已产生 unknown external effect，再接 idempotency/reconciliation，而不是盲 retry。

完成时每条 failure path 都能回答“这次 error 让哪条 premise 失效，因此哪条后续行为被禁止/改变”。

> 正确处理 error，不是看到红就停止；是绝不让已经被证伪的前提继续伪装成成功。