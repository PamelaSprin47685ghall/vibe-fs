# Prompt — 证明

行为：`what/prompt.md`。所有权：`shape/prompt.md`。程序：`how/prompt.md`。

## 写入口

| 证明 | 期望 | 条款 |
|------|------|------|
| 无第二 `prompt_async` | 生产路径全部经 Dispatcher | PROMPT-005 |
| 四阶段事实 | Claimed/Submitted/PhysicalAccepted/Abandoned 完备 | PROMPT-005 |
| `accepted-*` 非 Authority | 不得升级为 PhysicalAccepted | PROMPT-005 |

## Profile 与发送

| 证明 | 条款 |
|------|------|
| AttemptExecutionProfile 原子：禁止拼装 | PROMPT-008 |
| StrengthReplica：same-role profile，schema/execution gate 恰为 Read/Glob/Grep，mayCarryProbe=false，成功不清 owner failure count | PROMPT-008、STRENGTH-004/015 |
| 发送 `Agent=EffectiveAgent`，`Model=None` | PROMPT-006 |
| Fire-and-forget 仍完整 claim | PROMPT-007 |

代表：`tests/unit/prompt/*`、`tests/unit/context/attempt-plan.test.mjs`、`tests/unit/strength/authority-policy.test.mjs`。

## Gate D — System prompt 稳定性（PROMPT-014 / ARCH-016）

| 证明 | 期望 | 条款 |
|------|------|------|
| 同 Life system prompt 字节 | T1 / Peer Fallback / Strength / process review / Finality / compaction / reanchor / recovery 前后 **byte-identical** | PROMPT-014、GLORY-075、ARCH-016 D |
| SessionPersona 冻结 | session 创建一次绑定；上述事件不重绑 | PROMPT-014、AGENT-028/029 |
| SessionProviderLanguage 冻结 | 创建绑定；Fallback/Strength/reanchor 不改世界语 | PROMPT-017、HOST-026 |
| T1 revelation | 只经 conversation tool result；禁 system / Persona / Role Law 切换 | PROMPT-014、TODO-015 |
| Binding ≠ Persona | SelectedAgent / ExecutionBinding 变化不改 office system 字节、不冒充自称 | PROMPT-014、AGENT-029 |

语义不变量（§17.1）：`system prompt before T1 == after T1`；`fallback model switch → same Persona / same system prompt / same language`。

## Composition / Library（PROMPT-015/016）

| 证明 | 期望 | 条款 |
|------|------|------|
| SYSTEM 序 | Common Law → Role Law → Office Library；Tools 不并入 system 串 | PROMPT-015 |
| fast/deep 同 Role Law | strength 不造第二人格书 | PROMPT-015/016 |
| Office Library ≠ authority | 书不扩权；无 universal bible；无第二真源复制 | PROMPT-016 |
| Lifecycle orient | Activation/Reawakening/… 只注入 conversation/runtime，不替换 system | PROMPT-015 |
| generic Activation ≠ BlindPlan | 不得触发 Manager phase / system 切换 | PROMPT-015、TODO-015 |

## EN / zh-CN 语言面（PROMPT-017；Gate C 指针）

| 证明 | 期望 | 条款 |
|------|------|------|
| 双语资源 | 每个 provider semantic resource：EN 与 zh-CN 皆存在 | PROMPT-017、ARCH-016 C、HOST-026 |
| localizable vs invariant | prose 按已绑语言；tool 名 / argument / wire / enum / path / command / `exit_code` **原样** | PROMPT-017 |
| child 继承 | attached / InternalLeaf 继承 owner/commissioner；不得再读全局 | PROMPT-017、HOST-026 |

Gate C 静态门与跨域清单见 `proof/architecture.md` ARCH-016；Host 绑定算法见 `proof/host.md` HOST-026。

## 恢复

| 证明 | 期望 | 条款 |
|------|------|------|
| PromptKey 匹配 tail window | 找到则补 PhysicalAccepted | PROMPT-011 |
| 未找到 | Pending，**不**自动重发 | PROMPT-011 |
| 预算耗尽 | Abandoned(UnresolvedAfterRecovery) | PROMPT-011 |

## 禁区

Continuation 不得改 SelectedAgent / 新 Run / 重置 Fallback（PROMPT-003、PROMPT-010）。  
UnknownOrigin fail-closed（PROMPT-004）。

Assistance 专项：`NeedHelpEscalation`/`NeedHelpAdvice` codec+durable claim roundtrip；explicit agent 必须是同-role pair；fast→deep 不改 FallbackCursor，advice 精确回原 deep；system prompt / Persona byte-identical。Cursor User/System 试验 encoder 不能产生 HumanRoot/Opening，生产 Assistant encoder 也不能成为 semantic completion（PROMPT-018、HOST-013）。

## Student / Teacher — G3 已删除；证明迁 SyncDelegate

`StudentLearn` / `StudentCompile` / `StudentCompileNudge` / `TeacherIdleNudge` / QA bootstrap /
SKILL 制品提示 **absent**（G3 clean-break；PROMPT-012 / AGENT-020…022 空缺）。不得再作现行证明行。
后继：SyncDelegate 经 Dispatcher 的首发与 idle nudge（PROMPT-005/003；`SyncDelegateIdleNudge`）；
见 `proof/execution.md` SyncDelegate 行与 HOST-008。

| 证明 | 期望 | 条款 |
|------|------|------|
| 任一插件发送失败或 unknown | 无旁路重发；按 PROMPT-011 保持或关闭 | PROMPT-005、PROMPT-011 |
