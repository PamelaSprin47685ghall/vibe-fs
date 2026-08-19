# execution-model-routing — WHAT

本文件是 `execution-model-routing` 包的唯一 normative 语义合同。每条命题 = 当前世界必须同时成立的事实。证据指针 → `HOW.md`。

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
- `running` 是当前进程全部真实 provider capacity token 的 ModelTarget multiset，元素形状固定为 `{ model: string, reasoning: string }`；重复元素必须保留，数组顺序无语义。idle token 显示 owner target；token 被 descendant 正在使用时显示该 step 的 target；borrow 不增删 occurrence。
- `previous` 是同一未删除 Session 最近一次**成功物理 execution admission** 使用的 `{ model, reasoning }`，或 `null`。它只是接续对话的选择提示：exact terminal 释放 active lease 后仍保留，新的 Session / 已 drop 的 Session 必须传 `null`；它自身不贡献 `running` occurrence，也不恢复上一 execution 的 lease。
- 非 `null` 返回值必须同时包含完整非空 `provider/model` 与非空 `reasoning`；裸 `modelID` 非法，因为 provider 也必须由同一个 MJS authority 明确决定。非推理模型用显式字符串（推荐 `none`），不得靠缺字段猜测。
- `null` 的唯一含义是“按当前 occupancy 暂时不能安排”。
- scheduler throw、返回 Promise、返回其它值或非法 target 都是配置程序错误，fail closed；不得当作 `null`、provider failure 或 fallback 信号。

Wanxiangshu 不向 scheduler 暴露 SessionId、transcript、prompt、Host client 或业务状态。模型策略只以当前角色、occupancy 与上一成功 target 为输入。

## EMR-003：`running` 是真实 provider capacity token multiset；不是 live-session / active-execution 计数

`chat.message` 取得的是稳定 execution binding，不保证新建 capacity token。普通 admission 会为该 execution 建一枚 own token；EMR-010 lineage borrow admission 只取得 target binding，沿祖先已有 token 在 provider-step 时使用，因此 active execution 可以没有 own token。基础 token 总数才是 `running.Length` 的唯一真相。

同一 `PhysicalUserMessageId` 的 Host/provider retry 必须复用同一 target；同一 physical id 若观察到不同 EffectiveAgent，fail closed。不同 unrelated ordinary executions 若各自拥有 token，即使 target 完全相同也贡献重复 occurrence；borrower 与 lender 共享同一 token，不得双计数。一个 execution 同时最多拥有一枚基础 token。

occupancy registry 是同一 OpenCode OS 进程内的 module-level shared truth：root workspace 与 worktree 虽产生不同 plugin instance，必须观察同一 multiset。不同 OS/OpenCode 进程不共享本地 occupancy。

## EMR-004：required execution demand 只在 `chat.message` 物理执行准入产生；`null` = 等待，不是失败

**发送/排队不是 execution admission。** `fork`、repair、continuation 等 synthetic user message 的 dispatch 必须先完成 PromptKey/claim 并异步 enqueue；不得在 `SendPrompt`、tool 调用栈或 `promptAsync` settle 前抢 model slot。唯一 required managed demand owner 是 Host 已经接收该物理 user message 后的 `chat.message` 路由边界。

该边界第一次需要新 execution binding 时，runtime 以 capacity decorator 给出的定向 scheduling view 调 scheduler。若返回 target，必须先原子记录 binding；普通 admission 同时建立 own token，borrow admission 不新建 token。并发调用不得让两个普通 token acquisition 基于同一旧 snapshot 超额提交。

若 required execution 返回 `null`：

- 不调用 provider；
- 不推进 AABB/FallbackCursor；
- 不产生 business/provider failure；
- 不 busy-loop、timer poll 或跨模型自行降级；
- demand 保持 pending，直到 occupancy acquire/release 事件改变 `running`，再事件驱动重新调用 scheduler。

pending demand 必须可由 execution cancellation / abort、session delete、plugin shutdown，或**同 SessionId 更晚的 PhysicalUserMessageId**移除；被 supersede 的旧 pending demand 必须取消，不能日后抢到槽并复活旧请求。**业务 retire/completion 本身不是 capacity 证据**。每次 occupancy 变化后，runtime 按 pending 到达顺序各重试一次；某个较早 demand 仍返回 `null` 不得阻止后续不同 role 获得 scheduler 当前允许的 target。

pending demand 被 supersede / owner cleanup 移除是**预期 lifecycle outcome**，不得编码成 exception。routing owner 必须返回封闭 typed outcome（acquired / superseded）；真实 scheduler/invariant break 才允许 exception。若 supersede 发生在真实 `chat.message` hook 正在等待 model slot 时，旧 hook 必须成功短路且不再执行 model projection、PromptIngress 或 execution capability commit；不得让该正常取消穿过 plugin fatal membrane 变成 process fatal。

可丢弃优化若其 owner 明确规定“不等待”（例如 Strength K0），可以在 `null` 后放弃该 optional demand；这不是 required execution 的降级。

## EMR-005：模型选择策略全部属于 MJS；runtime 不再拥有 lane、容量表或候选算法

Wanxiangshu runtime 只拥有：scheduler 加载、ABI 校验、process-shared capacity token ledger + lineage borrowing mechanism、串行 arbitration、pending demand 与 Host model 投影。provider 限额数值与 target 选择策略仍只在 `wanxiangshu.mjs`：

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

execution 的普通完成释放必须携带 **exact physical identity**：Host terminal `message.updated` 的 assistant `parentID` 指向触发该 provider execution 的 `PhysicalUserMessageId`，只有 `(SessionId, parentID)` 与 current binding 完全匹配时才能 retire 该 execution 自己拥有的 token。borrower 没有 own token 时只删 binding。`finish="tool-calls"` 只结束一个 provider step：它把正在使用的 token 变回 owner-family 可仲裁的 idle token，但不结束 execution binding、也不删除基础 token occurrence。迟到旧 terminal 由 EMR-010 fence 拒绝。

physical adapter 若在 provider dispatch 前决定 suppress 当前 execution（例如 Enforcer `StopPhysicalRun`），必须从当前 wire transcript 取得 exact trailing `PhysicalUserMessageId`。adapter 先发起 Host abort、立即退出正在被 abort 的 transform callback；不得在该 callback 内 await 同一 abort，否则形成 self-deadlock。Host 接受 abort 后才以 `(SessionId, PhysicalUserMessageId)` 精确释放 lease；abort 失败则保留 lease，等待真实 terminal/supersede/delete 证据。

`SessionIdle` 与 typed `AttemptAborted` 只带 SessionId，因此只能作为 wake / quiescence / abort observation，**不得直接删除 execution binding / own token**。`SessionDeleted`、scope cleanup 与 plugin shutdown 因为销毁整个 owner，可按 SessionId 强制 retire current binding、own token 与等待。除此之外，同一 SessionId 的**新 PhysicalUserMessageId**是旧 execution supersession 的直接证据：同 provider own token 可原子移交给新 binding；不能复用时则 retire，若旧 provider step 尚 in-flight，真实 ledger 必须保留该 retiring token 直到 step terminal，绝不提前制造假空槽。

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

“managed”由 `chat.message` 的 typed admission 决定，不能由后续 Host hook 上出现某个 managed agent 字符串反推。若同一 exact physical material 已由其它规范 owner 明确分类为非业务 provider material（当前唯一实例：CRASH-018 `/continue` disclosure-only），`chat.params` 必须保持该分类，不得凭 agent label 要求一个从未建立、也不应建立的 MJS / `SessionExecutionBinding` lease。

非 managed Host 会话不受本包接管。

## EMR-010：provider capacity 独立成可抢占 token；只沿 session lineage 借用

ModelTarget execution binding 与 provider capacity token 必须分离。`chat.message` 仍唯一决定当前 physical execution 的稳定 target；每个 provider request 真正形成前，`experimental.chat.messages.transform` 必须先取得该 target provider 的 capacity token。

capacity arbitration 必须由独立 F# owner 实现。基础 ledger 只保存真实 token multiset；borrowing decorator 在其上维护 session lineage、借用、祖先 recall、provider-step fence 与等待。`wanxiangshu.mjs` 不接收 parent/child/session id，不保存借贷状态；其它 Host/Tool 主流程不得复制借贷判断。

descendant 可把一枚同 provider 的祖先 token 当作定向 credit，但该 token 在真实 ledger 中始终存在，无关 session 继续把它视为 occupied。若多个 provider credit 同时可见，一个 borrowed scheduler 决策必须能由**恰好一枚**同 provider token 单独解释；否则退回完整 `running` 普通调度，禁止把多枚 credit 合成虚假全局空闲。

token 只在 provider-step 边界转移。descendant 当前 provider request 已发出时，ancestor recall 必须等该 step 的 assistant completed/error observation；不得 abort 正在飞的 request，也不得先让 ancestor 发出第二个同 token request。step 结束后，token owner 本人优先，其次近 descendant，再次远 descendant，同 ancestry depth 按请求顺序。

borrower 每次新 transform 都重新申请 step token。若祖先已召回，它只有两条合法路：MJS 在完整 `running` 上仍返回当前 execution 的 exact target → 建立自己的普通 token；否则等待祖先再次闲置并重借。借来的 token 可沿 child→grandchild 继续转借，但基础 token 数不因层数或子数增加。

Fission fresh lane 虽不是 delegation child，capacity lineage 必须绑定到被替代 logical owner；多 lane 竞争同一 owner credit，不复制免费 capacity。不同 provider 永不互借。

provider-step terminal 必须有 anti-stale fence：step 开始时记录 wire 已可见 assistant ProviderRunIdentity；只接受 fence 之外的新 assistant terminal 结束当前 step。若 terminal event 丢失，下一次 transform 观察到新增 assistant run 本身即可补证上一 step 已结束。

若 transform 后段在 provider dispatch 前决定 suppress 当前 run，不能等待不存在的 assistant terminal，也不能先释放再赌 abort 成功。只有 Host abort 已明确返回成功后，physical adapter 才可调用 capacity owner 的 pre-dispatch suppression 边界归还当前 step token，再 retire exact execution；abort 失败必须保留 token。
