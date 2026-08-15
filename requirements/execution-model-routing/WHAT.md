# execution-model-routing — WHAT

本文件是 `execution-model-routing` 包的唯一 normative 语义合同。每条命题 = 当前世界必须同时成立的事实。证据指针 → `PROOF.md`。

## EMR-001：唯一模型调度 authority = `~/.config/opencode/wanxiangshu.mjs`；缺失时原子创建推荐模板

Wanxiangshu 的 managed model routing 只能由 `~/.config/opencode/wanxiangshu.mjs` 的 default export 决定。禁止从 `opencode.json`、环境变量、Host-final agent inventory、内建 lane/model 表或其它运行时默认值补齐/覆盖。

若该文件在 plugin load 时不存在，Wanxiangshu 必须先确保 `~/.config/opencode/` 目录存在，再以 create-if-absent 的原子方式写入随当前版本发布的推荐 MJS 模板；随后立即 import **实际落盘的文件**。并发 plugin/process bootstrap 中若另一方先创建成功，当前方只加载 winner 文件，不覆盖、不 merge。已有文件永远不得自动改写，即使产品版本更新了推荐模板。

因此推荐模板只是“首次生成的可编辑配置”，不是隐藏 fallback：一旦创建，它和用户手写文件完全同权，之后唯一 authority 就是该文件自身。创建目录/文件失败、import/evaluation 失败、default export 不是同步函数时，plugin load fail closed；不得退回旧 model inventory。

## EMR-002：scheduler ABI 只有 `role + running → target | null`

scheduler 的唯一调用合同是：

```js
export default function route(role, running) {
  // return { model: "provider/model", reasoning: "none|low|..." }
  // or null
}
```

- `role` 是当前需要物理模型的 managed `EffectiveAgent` 精确名（如 `fast-coder`、`deep-browser`）。
- `running` 是当前进程全部已取得且尚未释放的 ModelTarget multiset，元素形状固定为 `{ model: string, reasoning: string }`；重复元素必须保留，数组顺序无语义。
- 非 `null` 返回值必须同时包含完整非空 `provider/model` 与非空 `reasoning`；裸 `modelID` 非法，因为 provider 也必须由同一个 MJS authority 明确决定。非推理模型用显式字符串（推荐 `none`），不得靠缺字段猜测。
- `null` 的唯一含义是“按当前 occupancy 暂时不能安排”。
- scheduler throw、返回 Promise、返回其它值或非法 target 都是配置程序错误，fail closed；不得当作 `null`、provider failure 或 fallback 信号。

Wanxiangshu 不向 scheduler 暴露 SessionId、transcript、prompt、Host client 或业务状态。模型策略只以当前角色和 occupancy 为输入。

## EMR-003：`running` 是 lease multiset；重复次数就是占用次数，并跨 root/worktree 插件实例共享

managed `(SessionId, EffectiveAgent)` 每成功取得一个稳定 model lease，就向 `running` 贡献一个 `{model, reasoning}` 元素，直到该 lease 被释放。同一 SessionId 的两个 EffectiveAgent 即使取得完全相同 target，也贡献两个重复元素；runtime 不去重。

occupancy registry 是同一 OpenCode OS 进程内的 module-level shared truth：root workspace 与 worktree 虽产生不同 plugin instance，必须观察同一 multiset。不同 OS/OpenCode 进程不共享本地 occupancy。

## EMR-004：required demand 由 occupancy 事件驱动重试；`null` = 等待，不是失败

第一次需要新 lease 时，runtime 以当下 `running` 快照调用 scheduler。若返回 target，必须先原子记录该 lease/occupancy，再允许下一次调度决策观察状态；并发调用不得让两个决策看见同一旧快照后同时提交。

若 required execution 返回 `null`：

- 不调用 provider；
- 不推进 AABB/FallbackCursor；
- 不产生 business/provider failure；
- 不 busy-loop、timer poll 或跨模型自行降级；
- demand 保持 pending，直到 occupancy acquire/release 事件改变 `running`，再事件驱动重新调用 scheduler。

pending demand 必须可由 request cancellation、session abort/retire 或 plugin shutdown 移除。每次 occupancy 变化后，runtime 按 pending 到达顺序各重试一次；某个较早 demand 仍返回 `null` 不得阻止后续不同 role 获得 scheduler 当前允许的 target。

可丢弃优化若其 owner 明确规定“不等待”（例如 Strength K0），可以在 `null` 后放弃该 optional demand；这不是 required execution 的降级。

## EMR-005：模型选择策略全部属于 MJS；runtime 不再拥有 lane、容量表或候选算法

Wanxiangshu runtime 只拥有：scheduler 加载、ABI 校验、process-shared occupancy、串行 acquire/release、pending demand 与 Host model 投影。下列策略全部只能写在 `wanxiangshu.mjs` 内：

- 哪些 role 共享一组模型；
- fast/deep/Browser 是否分池；
- 模型优先级；
- 每个 `{model, reasoning}` 或模型族允许多少占用；
- 满载时是否尝试同策略中的第二候选；
- 任意基于 `role + running` 可确定的其它资源选择规则。

runtime 不得重建“七个 lane”、`max_sessions` schema、first-free candidate、模型能力分类或其它第二套调度算法。MJS 返回非 `null` target 后，runtime 只做结构校验并接受该选择。

## EMR-006：managed lease 绑定 `(SessionId, EffectiveAgent)`；AABB/peer 只换 EffectiveAgent

成功分配后，`(SessionId, EffectiveAgent) → ModelTarget` 在当前 OpenCode process epoch 内、该 session live 生命周期中稳定。普通 continuation/prompt 不得重新调用 scheduler 换 target；只有该 key 尚无 lease 时才产生新 demand。

AABB 保持原代数：A/A 使用当前 SelectedAgent，B/B 使用其 peer。切到 peer 后只是在同一 SessionId 下首次取得/复用另一个 EffectiveAgent 的 lease。A 与 B 的 `{model, reasoning}` 可以完全相同，也可以不同；不得以物理 target 是否相同判断 peer 是否成立。

Strength/assistance/fallback 改档仍通过既有 EffectiveAgent authority；scheduler 不自行发起 tier/peer 切换。

## EMR-007：lease release 是 occupancy 事件；释放幂等并触发 pending demand 重算

managed/user-facing session 明确 retire/delete 时，runtime 必须释放该 SessionId 持有的全部 `(SessionId, EffectiveAgent)` lease；每个 lease 删除一个 `running` occurrence。重复 cleanup 幂等。

每次真实 occupancy 变化都触发 EMR-004 的 pending-demand 重试流程。

仅切换 A/B、单次 provider failure、普通 idle/completion 不释放 live managed session 的稳定 lease。

## EMR-008：`opencode.json` model 不再具有 authority；不校验 fast/deep model 互异

Wanxiangshu 可以向 Host managed-agent config 投影必要的 mode/permission/prompt/guardrail 字段，但实际 provider request 的 managed ModelTarget 必须来自本包 scheduler lease。`opencode.json` 中已有 managed agent `model` 值不得被读取为 routing truth，也不得覆盖 MJS 选择。

启动配置不再执行 `fast-X.model <> deep-X.model` 校验，也不因两个 EffectiveAgent 最终取得相同 `{model, reasoning}` 而失败。peer existence/对称性仍由 `participant-identity` 保证。

## EMR-009：user-facing model 字段不是 managed model authority

真实外部用户请求仍可决定 managed EffectiveAgent/档位；但其 Host message/request 携带的 `model` / reasoning 字段不得成为 Wanxiangshu managed binding authority。进入 provider 前，managed request 必须使用 `(SessionId, EffectiveAgent)` 已取得的 MJS lease；Host 观察到的实际 provider model/reasoning 必须与该 lease 一致，否则 fail closed。

非 managed Host 会话不受本包接管。
