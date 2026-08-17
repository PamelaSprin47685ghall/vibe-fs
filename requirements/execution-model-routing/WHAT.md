# execution-model-routing — WHAT

本文件是 `execution-model-routing` 包的唯一 normative 语义合同。每条命题 = 当前世界必须同时成立的事实。证据指针 → `PROOF.md`。

## EMR-001：唯一模型调度 authority = `~/.config/opencode/wanxiangshu.mjs`；缺失时原子创建推荐模板

Wanxiangshu 的 managed model routing 只能由 `~/.config/opencode/wanxiangshu.mjs` 的 default export 决定。禁止从 `opencode.json`、环境变量、Host-final agent inventory、内建 lane/model 表或其它运行时默认值补齐/覆盖。

若该文件在 plugin load 时不存在，Wanxiangshu 必须先确保 `~/.config/opencode/` 目录存在，再以 create-if-absent 的原子方式写入随当前版本发布的推荐 MJS 模板；随后立即 import **实际落盘的文件**。并发 plugin/process bootstrap 中若另一方先创建成功，当前方只加载 winner 文件，不覆盖、不 merge。已有文件永远不得自动改写，即使产品版本更新了推荐模板。

因此推荐模板只是“首次生成的可编辑配置”，不是隐藏 fallback：一旦创建，它和用户手写文件完全同权，之后唯一 authority 就是该文件自身。创建目录/文件失败、import/evaluation 失败、default export 不是同步函数时，plugin load fail closed；不得退回旧 model inventory。

## EMR-002：scheduler ABI 只有 `role + running + previous → target | null`

scheduler 的唯一调用合同是：

```js
export default function route(role, running, previous) {
  // return { model: "provider/model", reasoning: "none|low|..." }
  // or null
}
```

- `role` 是当前需要物理模型的 managed `EffectiveAgent` 精确名（如 `fast-coder`、`deep-browser`）。
- `running` 是当前进程全部已取得且尚未释放的 ModelTarget multiset，元素形状固定为 `{ model: string, reasoning: string }`；重复元素必须保留，数组顺序无语义。
- `previous` 是同一未删除 Session 最近一次**成功物理 execution admission** 使用的 `{ model, reasoning }`，或 `null`。它只是接续对话的选择提示：exact terminal 释放 active lease 后仍保留，新的 Session / 已 drop 的 Session 必须传 `null`；它自身不贡献 `running` occurrence，也不恢复上一 execution 的 lease。
- 非 `null` 返回值必须同时包含完整非空 `provider/model` 与非空 `reasoning`；裸 `modelID` 非法，因为 provider 也必须由同一个 MJS authority 明确决定。非推理模型用显式字符串（推荐 `none`），不得靠缺字段猜测。
- `null` 的唯一含义是“按当前 occupancy 暂时不能安排”。
- scheduler throw、返回 Promise、返回其它值或非法 target 都是配置程序错误，fail closed；不得当作 `null`、provider failure 或 fallback 信号。

Wanxiangshu 不向 scheduler 暴露 SessionId、transcript、prompt、Host client 或业务状态。模型策略只以当前角色、occupancy 与上一成功 target 为输入。

## EMR-003：`running` 是**当前物理 provider execution** 的 lease multiset；不是 live-session 计数

managed provider execution 在 `chat.message` admission 以 **`(SessionId, PhysicalUserMessageId)`** 取得一个 model lease 后，向 `running` 贡献一个 `{model, reasoning}` 元素。`SessionId` 只是可复用容器，不是 occupancy 生命周期；同一 session 同时最多有一个 current physical execution。该 execution 在 idle/abort/delete 时释放，**或在同一 SessionId 出现新的 PhysicalUserMessageId 时被新 execution 原子 supersede**。因此正确性不得依赖 Host 一定先发 idle：新物理 user material 本身就是旧 execution 不再拥有 capacity 的证据。

同一 `PhysicalUserMessageId` 的 Host/provider retry 必须复用同一 target；同一 physical id 若观察到不同 EffectiveAgent，fail closed。不同 session 的并发 physical execution 即使取得完全相同 target，仍各贡献一个重复元素；runtime 不按 target 去重。

occupancy registry 是同一 OpenCode OS 进程内的 module-level shared truth：root workspace 与 worktree 虽产生不同 plugin instance，必须观察同一 multiset。不同 OS/OpenCode 进程不共享本地 occupancy。

## EMR-004：required execution demand 只在 `chat.message` 物理执行准入产生；`null` = 等待，不是失败

**发送/排队不是 execution admission。** `fork`、repair、continuation 等 synthetic user message 的 dispatch 必须先完成 PromptKey/claim 并异步 enqueue；不得在 `SendPrompt`、tool 调用栈或 `promptAsync` settle 前抢 model slot。唯一 required managed demand owner 是 Host 已经接收该物理 user message 后的 `chat.message` 路由边界。

该边界第一次需要新 execution lease 时，runtime 以当下 `running` 快照调用 scheduler。若返回 target，必须先原子记录该 lease/occupancy，再允许下一次调度决策观察状态；并发调用不得让两个决策看见同一旧快照后同时提交。

若 required execution 返回 `null`：

- 不调用 provider；
- 不推进 AABB/FallbackCursor；
- 不产生 business/provider failure；
- 不 busy-loop、timer poll 或跨模型自行降级；
- demand 保持 pending，直到 occupancy acquire/release 事件改变 `running`，再事件驱动重新调用 scheduler。

pending demand 必须可由 execution cancellation / abort、session delete、plugin shutdown，或**同 SessionId 更晚的 PhysicalUserMessageId**移除；被 supersede 的旧 pending demand 必须取消，不能日后抢到槽并复活旧请求。**业务 retire/completion 本身不是 capacity 证据**。每次 occupancy 变化后，runtime 按 pending 到达顺序各重试一次；某个较早 demand 仍返回 `null` 不得阻止后续不同 role 获得 scheduler 当前允许的 target。

可丢弃优化若其 owner 明确规定“不等待”（例如 Strength K0），可以在 `null` 后放弃该 optional demand；这不是 required execution 的降级。

## EMR-005：模型选择策略全部属于 MJS；runtime 不再拥有 lane、容量表或候选算法

Wanxiangshu runtime 只拥有：scheduler 加载、ABI 校验、process-shared occupancy、串行 acquire/release、pending demand 与 Host model 投影。下列策略全部只能写在 `wanxiangshu.mjs` 内：

- 哪些 role 共享一组模型；
- fast/deep/Browser 是否分池；
- 模型优先级；
- 每个 `{model, reasoning}` 或模型族允许多少占用；
- 满载时是否尝试同策略中的第二候选；
- 是否在当前 role/capacity 仍允许时优先延续 `previous` target；
- 任意基于 `role + running + previous` 可确定的其它资源选择规则。

runtime 不得重建“七个 lane”、`max_sessions` schema、first-free candidate、模型能力分类或其它第二套调度算法。MJS 返回非 `null` target 后，runtime 只做结构校验并接受该选择。

## EMR-006：managed lease 只在一个物理 execution 内稳定；session continuation 重新调度但可偏好上一 target

成功分配后，当前 execution 的 **`(SessionId, PhysicalUserMessageId) → {EffectiveAgent, ModelTarget}`** 在该 physical user material 内稳定；Host/provider retry 若仍引用同一 physical id，继续使用同一 lease，且 EffectiveAgent 不得漂移。新的 PhysicalUserMessageId 即使没有先看到 idle，也必须原子 retire 旧 lease / cancel 旧 pending，再为新 execution 调度。该次 scheduler 调用必须额外收到同一未删除 Session 最近一次成功物理 execution 的 target 作为 `previous`；MJS 可以优先返回它，但 occupancy/role policy 不允许时仍可返回其它 target 或 `null`。因此 continuation 有稳定偏好而无永久绑定，禁止跳过 scheduler 直接复用上一 lease。

AABB 保持原代数：A/A 使用当前 SelectedAgent，B/B 使用其 peer。peer/tier 选择仍属于 authority/fallback；scheduler 只在每个物理 execution admission 时把当时 EffectiveAgent 映射到 target。A 与 B 的 `{model, reasoning}` 可以完全相同，也可以不同；不得以物理 target 是否相同判断 peer 是否成立。

Strength/assistance/fallback 改档仍通过既有 EffectiveAgent authority；scheduler 不自行发起 tier/peer 切换。

## EMR-007：physical execution identity / end evidence 释放 occupancy；session/业务 lifecycle 不拥有槽

active lease 的普通完成释放必须携带 **exact physical identity**：Host terminal `message.updated` 的 assistant `parentID` 指向触发该 provider execution 的 `PhysicalUserMessageId`，只有 `(SessionId, parentID)` 与 current lease 完全匹配时才能删除该 occurrence。`finish="tool-calls"` 只结束一个 provider step，同一 physical execution 仍会经工具继续，因此不得释放；只接受 assistant error，或已 completed 且 finish 不是 `tool-calls` 的最终 assistant。迟到的旧 terminal 对已经 reuse 到新 PhysicalUserMessageId 的同一 SessionId 必须是 no-op。

`SessionIdle` 与 typed `AttemptAborted` 只带 SessionId，因此只能作为 wake / quiescence / abort observation，**不得直接删除 active model lease**；否则 A 的迟到 coarse signal 可以误删刚由 B 的 `chat.message` 建立的 lease。`SessionDeleted`、scope cleanup 与 plugin shutdown 因为销毁整个 owner，可按 SessionId 强制清理 current lease/pending。除此之外，同一 SessionId 的**新 PhysicalUserMessageId**是旧 execution 被 supersede 的直接物理证据，必须在新 admission 内原子释放旧 lease，即使 Host 没有先发 exact terminal。每个实际 lease 删除一个 `running` occurrence，重复 cleanup 幂等，并触发 EMR-004 pending-demand 重算。

同一 admission 还必须先使上一 terminal 的 idle-derived continuation permit 失效：`chat.message`
已经证明新 physical material 存在，不能把旧 idle authority 保留到后续
`experimental.chat.messages.transform`。否则旧 repair/encouragement 可在这段窗口发送一条新的
physical user message，合法 supersede 刚建立的 model lease，随后原 execution 到 `chat.params` 时只剩
exact binding 而无 lease。该 quiescence ingress barrier 由 CRASH-006 拥有；EMR 只消费其“不允许旧
idle continuation 抢占新 physical execution”结论。

业务层的 handle completed/retired/join/finality、Companion close 等**不得直接决定槽是否释放**：它们描述工作语义，不证明 provider execution 的物理状态。反过来，释放槽也不表示 session 永久结束；同一 SessionId 可继续 reuse/reopen。

Host `ProviderRetry` / `ProviderFailure` 若仍处于同一物理 user execution，不提前释放；否则一次 upstream retry 会被误当成新 execution 并与旧 attempt 重叠计数。`chat.params` 对 exact binding 的复核必须在这种迟到 coarse signal 下保持稳定；不得用“取消 live lease 校验”来掩盖错误 release。

## EMR-008：`opencode.json` model 不再具有 authority；不校验 fast/deep model 互异

Wanxiangshu 可以向 Host managed-agent config 投影必要的 mode/permission/prompt/guardrail 字段，但实际 provider request 的 managed ModelTarget 必须来自本包 scheduler lease。`opencode.json` 中已有 managed agent `model` 值不得被读取为 routing truth，也不得覆盖 MJS 选择。

启动配置不再执行 `fast-X.model <> deep-X.model` 校验，也不因两个 EffectiveAgent 最终取得相同 `{model, reasoning}` 而失败。peer existence/对称性仍由 `participant-identity` 保证。

## EMR-009：`chat.message` 是唯一 managed model admission；dispatch message 保持 model-free

真实外部用户请求仍可决定 managed EffectiveAgent/档位；plugin synthetic prompt 则由 PromptAuthority 决定 EffectiveAgent。两者在发送/排队阶段都不得把 Host `model` / reasoning 字段当成 managed binding authority，也不得提前占 capacity：plugin dispatch 的 `OpenCodePromptOptions.Model` 必须保持 `None`。

Host 接收物理 user message 后，`chat.message` 是唯一 required managed model admission owner：它必须同时取得 `PhysicalUserMessageId` 与合法 EffectiveAgent，以 `(SessionId, PhysicalUserMessageId)` 调用/复用 MJS execution lease，并把 `{providerID, modelID, variant}` 写入 Host mutable message。`chat.params` 只验证刚由 chat.message 记录的 current exact binding；随后 messages transform 再以 trailing physical user id + PromptKey（若有）二次核对同一 execution。缺失 physical id 的 managed admission fail closed。发送栈与 `chat.message` 同时 required-acquire 属重复 authority，禁止。

非 managed Host 会话不受本包接管。
