参见 /AGENTS.md

## Prompt Authority、Logical Run 与 Synthetic Continuation [NORMATIVE]

`PhysicalUserMessage ≠ AuthorityTurn`。`role=user` 只是 Host 的运输角色；只有 Authority Root 才能创建 Logical Run、选择 agent/model/variant、成为 Fallback root、重置 repair 预算、更新 LastAuthorityProfile 或决定 Companion eligibility。

- `HumanRoot` 与 `AgentOwnerRoot` 是 Authority Root。AgentOwnerRoot 必须显式给出 agent/model/variant。
- `InteractionRepair`、Manager/Reviewer Guard、ReviewConfirmation、BusyAgentNudge、provider retry 与 Host compaction continuation 都是 Continuation：复用 LogicalRunId 与 AuthorityRootUserMessageId，不建 completion/run，不更新 LastAuthorityProfile、不重置 Fallback/repair、不得改变 Companion eligibility。
- `LastAuthorityProfile` 是最后 Authority Root 的 profile；真人省略字段只从它继承，绝不从最后 physical user message 或 B retry 继承。用户显式 model 永远开启新的 A-side epoch，覆盖旧 fallback side。
- 所有插件 continuation 必须通过 PromptDispatcher 两阶段协议：先 `PluginPromptClaimed(PromptKey, Origin, LogicalRunId, AuthorityRoot)`；带 `wanxiangshu_prompt_key`/origin/run/root metadata 发出；接受后 `PluginPromptAccepted(PromptKey, HostMessageId)`；失败 `PluginPromptAbandoned`。Host 无法关联 acceptance 时 fail-closed 为 `HostContractUnsupported`。
- 来源解析顺序：accepted HostMessageId → claimed PromptKey → Host compaction/synthetic provenance → registered AgentOwnerRoot → proven external prompt acceptance (`HumanRoot`) → `UnknownOrigin`。Unknown 不得变成 HumanRoot、改变 profile、启动 fallback、改变 Companion，或发送 continuation。
- `\u200B`、空白、固定文本、时间与内容长度都不是身份。bare continuation 不构成 Companion semantic delta；其后正式 assistant output 才可能构成 delta。
- Review witness 必须同时区分 confirmation 的 PhysicalUserMessageId 与原 Reviewer AuthorityRootUserMessageId。

## Event-Stagger 规则

第一个 canary 立即启动；canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）。
