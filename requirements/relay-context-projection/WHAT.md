# relay-context-projection — WHAT

## [001] 下一迭代 provider 投影保留完整物理历史

物理 transcript 与 durable audit 不删除任何消息，下一迭代的 provider projection 也不删除：与 audit 相同，前任 epoch 的全部消息——raw messages、tool calls、results、nudge、迟到旧 run parts 与内部 loop wake——原样出现在继任迭代的 provider 上下文中，其中包括 accepted retirement 的 suicide tool call 与其 tool result。不再存在基于 retirement cut 的消息过滤：不得按 cut 位置删除、截断或折叠任何前任消息，不得用最近 N 条、时间戳或文本搜索 suicide 推断消息边界。逻辑迭代重开不等于物理失忆，继任者看见的前缀就是前任真实经历过的历史。

## [002] ProjectionCut 是 durable retirement binding，职责仅为请求身份判定与 stale 拦截

accepted retirement 的 `ProjectionCut = { ProviderRunId; ToolCallId }` 仍是闭合 RetirementSummary 的 load-bearing retirement binding，随 durable transaction 落盘，记录前任最后一次 provider run 的身份与 suicide 请求位置。它的职责仅限于：判定一条后续 provider 请求属于哪一次 attempt，凡携带已退休 ProviderRunIdentity 的迟到 parts 与后续请求一律在 transform 边界按 stale attempt 拦截，只能进入 audit 或 stale diagnostics，不得作为新迭代的活跃请求。cut 不再定义任何消息过滤边界，不得据此排除、隔离或重排消息；cut 只认位置、role 与 typed message identity，不认文本、不猜 run、不等物理 arrival。真实用户需求、typed authority 消息与新迭代 owner-admitted `runtime/manager-assess` 资源从不属于 stale attempt，不得被 cut 拦截或删除。

## [003] projection 不转述工作区，不序列化领域标识

下一迭代的 provider 上下文由完整物理消息历史与显式资源构成，但 projection 不得转述共享工作区的内容：工作区是共享执行状态，Manager 直接检查它，projection 不代理叙述它。AuthorityRevision、SnapshotId 与 phase ID 永不序列化进 provider 消息。secret 不得进入 provider 消息。当前动作由事实约束后的显式资源与 capabilities 表达，不由对前任输出的概括或摘要代理。

## [004] 每一迭代使用同一 review-first 起点：独立评审前任的工作

所有迭代使用同一 review-first 结构：先独立评审前任的工作，再决定自己的动作。首任迭代不设流程特例，只是「前任」这一位置由用户交付的任务本身填充：第一任陈述当前权威需求与接手时的状态，不伪造任何前任 commit、测试或结论——这里的前任就是交付任务的用户。后继迭代评审的是真实前任留下的完整工作：provider 历史（含 suicide 与失败过程）、共享工作区状态与显式工作记录，而不是仅凭静态仓库快照。工作区是共享执行状态，Manager 自行检查工作区，projection 不负责叙述工作区内容。

## [005] 物理 SessionId 与用户线程保持连续

迭代切换不得为了缩上下文创建新的用户聊天线程或删除历史。SessionId 可以跨迭代复用；IncumbencyId 和 provider context 不得复用。

## [006] projection 是确定性映设，不生成合成列表

projection 只是对 durable facts 的确定性映设：相同 facts 产生相同 provider 可见集合，不生成任何合成消息与合成列表。不得包含 hidden reasoning、完整 transcript 摘要、token 或 credential。不得改写、概括或拼接前任消息来制造一份「更干净的上下文」。

## [007] crash recovery 不得回退 cut

已 committed retirement/cut 在 Host crash 后仍是 provider 请求身份判定的依据。任何退休后开启的新 active 迭代一律以上一次 LatestRetirement cut 判定 stale 请求，包括 Accepted 退休经显式证书失效后重开的迭代。两种 outcome 都在 transform 边界中断已退休 attempt 的后续 provider 请求；只有 Continue 自动激活下一迭代。迟到旧 ProviderRunIdentity parts 只能进入 audit 或 stale diagnostics，不能作为新迭代的活跃请求。crash recovery 不得删除已 committed cut，也不得据此删除已保留的历史消息。

## [008] 完整历史包含 authority 链、前任全部消息与内部 wake

初始 root authority message 与之后每个 durable accepted AuthorityRevision 对应的物理 authority message 都是 Road 的权威输入，projection 必须按 typed message identity 保留它们；前任的 user/assistant/tool/nudge 原始消息、内部 loop wake continuation（即使形如 user message）与 suicide tool call/result 同样作为历史保留，不得因其非 authority 身份或形状而被移除。保留历史不等于放宽 authority 判定：不得把形如 user message 的内部 wake 解释为新的 authority input，不得以文本匹配例外挑选保留对象；authority 判定仍按 typed message identity。

## [009] 前任工作与交互对继任可见，固定 DevOps 执行事实如实呈现

下一任 Manager 的 provider 上下文包含前任的完整物理历史：前任的私有过程、prompt 历史、suicide 与交互细节如实可见，继任者据此评审前任的工作，而不是凭静态仓库快照或摘要猜测前任做过什么。固定 DevOps 的最新执行产物、修改事实与运行结果通过共享工作区快照与显式工作记录/检查接口对新任可见，与 provider 历史相互印证；投影严禁把前任过程概括为结论替继任答题。不得假定 Manager 分身并发多路交互：同一物理 SessionId 上的交互仍归属确定的 IncumbencyId，继任上下文是同一物理会话的历史延续，不是多名 Manager 并行对话的混合体。
