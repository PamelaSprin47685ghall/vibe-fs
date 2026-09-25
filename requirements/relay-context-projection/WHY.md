# relay-context-projection — WHY

继任的 Manager 要评判的是前任真实做过的工作，不是一份被修剪过的静态摘要。退休只是逻辑任期闭合，不是物理经历的抹除：前任调过什么工具、在哪里提交 suicide、被拒绝过什么请求、收到过什么迟到回复，都是「前任做了什么」的组成部分。若 projection 按 retirement cut 把这些消息删掉，继任者就只能凭当前工作区猜测前任的意图与过程——把可观察的历史换成不可验证的印象，独立评审也就失去了对象。

连贯性与继任可见性因此要求：物理 transcript、durable audit 与下一迭代的 provider 投影保留同一份完整历史，包括前任 epoch 的全部消息、suicide tool call 与其 result、迟到 parts 与内部 loop wake。逻辑迭代重开保证的是 authority、账本与评价重新开始，不是记忆清零。退休边界仍然刀口清楚，但它切的是 attempt 身份，不是消息：ProjectionCut 用来判定后续 provider 请求属于已退休 attempt 还是新迭代，携带已退休 ProviderRunIdentity 的迟到请求一律按 stale 拦截，只进 audit 或 diagnostics。旧 attempt 不会复活，历史也不会被删。

固定 DevOps 的执行成果同样如实呈现给继任者：通过共享工作区文件、测试产物与显式工作记录可见，与 provider 历史相互印证，而不是靠修剪或污染上下文来传递。继任 Manager 因此既看得见前任完整做了什么，又能从当前权威需求与工作区出发独立判断，不必在「失忆」与「继承他人结论」之间二选一。
