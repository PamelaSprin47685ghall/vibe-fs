所有委托成员都返回**数据**，不返回渲染后的文字；渲染由宿主替你完成，返回值保持 JSON。

```
{ ok: true, ... }          成功
{ ok: false, code: "..." }  拒绝（稳定的 kebab 码）
```

凡是把手上的活交出去的成员——`fork`、`resume`、`commission`、`devops`、`dag`、`advisor`——最后一个参数都是一份**报告映射**：`Record<string, null|string>`，把锚点编号映到摘要字符串或 `null`（保留）。宿主把它读作 `{ prefixUntil: 0, thenMap: reportMapping }`，因此不接受 `{ prefixUntil, thenMap }` 包装对象。这份映射就是宿主投影给子工作的上下文；缺省或非法一律以 `{ ok: false, code: "VIEW_DECLARATION_INVALID" }` 拒绝。拉取面成员——`join`、`horizon`、`fission`——不带报告映射。

## fork

```js
await this.fork("为解析器模块建索引", { 1: null });                          // 只给任务
await this.fork({ charge: "为解析器建索引", name: "parser-index", calling: "engineer" }, { 1: null, 2: "解析器清单" });
```

- 载荷：字符串（任务）或 `{ charge, name?, calling?, keywords? }`；缺 `name` 时由任务文本派生。
- `reportMapping` 必填：`Record<string, null|string>`，把锚点编号映到摘要或 `null`；宿主读作 `{ prefixUntil: 0, thenMap: reportMapping }`。缺省或非法以 `{ ok: false, code: "VIEW_DECLARATION_INVALID" }` 拒绝。
- 结果：`{ ok: true, name, agentId, outcome }`，`outcome` 为 `created` | `nudged` | `commissioned`。
- 拒绝码：`name-required`、`charge-required`、`unknown-calling`、`calling-conflict`、`name-already-belongs`、`warm-start-unavailable`、`context-unavailable`、`not-a-delegation-role`、`placement-failed`、`dispatch-uncertain`。

## resume

```js
const resumed = await this.resume({ name: "audit-road", charge: "继续那轮审计" }, { 1: null, 4: "目前的审计结论" });
```

- 把下一份差事交给已经存在的被委派者。`resume` **不带 `calling`**：它延续已在位的那个被委派者，而不是另行呼叫某个角色，所以载荷里带 `calling` 会被拒绝。载荷是 `{ charge, name }`：`name` 指明要续的那条道路，必填——缺失时以 `name-required` 拒绝。
- `reportMapping` 必填：`Record<string, null|string>`，把锚点编号映到摘要或 `null`；宿主读作 `{ prefixUntil: 0, thenMap: reportMapping }`。缺省或非法以 `{ ok: false, code: "VIEW_DECLARATION_INVALID" }` 拒绝。
- 结果：`{ ok: true, name, agentId, outcome }`。

## commission

```js
await this.commission({ charge: "写出接口规格", name: "spec" }, { 1: null });
```

- 编排者自己交活的方式：编排者把差事委派出去，而不是自己做。载荷是字符串（任务）或 `{ charge, name?, calling? }`——与 `fork` 同形，但没有 `keywords`。缺 `name` 时由任务文本派生；`calling` 省略时按角色推导，显式给出与推导不一致则以 `unknown-calling` 拒绝。
- `reportMapping` 必填：`Record<string, null|string>`，把锚点编号映到摘要或 `null`；宿主读作 `{ prefixUntil: 0, thenMap: reportMapping }`。缺省或非法以 `{ ok: false, code: "VIEW_DECLARATION_INVALID" }` 拒绝。
- 结果：`{ ok: true, name, agentId, outcome }`。

## devops

```js
const answer = await this.devops("找出那条偶发失败的测试", { 1: null });
```

- 一次在本次调用内同步完成的 DevOps 咨询：宿主请教 DevOps，当场把答案交回来。没有子工作要 join，也不会留下还在跑的东西。`intent` 是要问的那句话——不承载 charge，与 `fork`、`resume` 不同。
- `reportMapping` 必填：`Record<string, null|string>`，把锚点编号映到摘要或 `null`；宿主读作 `{ prefixUntil: 0, thenMap: reportMapping }`。缺省或非法以 `{ ok: false, code: "VIEW_DECLARATION_INVALID" }` 拒绝。
- 拒绝码：DevOps 席位尚未绑定或已关闭时为 `assessment-pending-for-devops`。
- 结果：`{ ok: true, record }`——调查记录即数据。

## join

```js
const { records, byName } = await this.join();
```

- 结果：`{ ok: true, records: [{ name, agentId, role, status, view }], byName: { [name]: record } }`。
- `status` 为 `completed` | `failed` | `abandoned`；子工作只有以 `this.deliver(reportMapping)` 结束才进入记录——未交付的子工作不结算：保持活跃，join 不关闭，你可以继续等待或重派。`view` 是子工作经 `this.deliver` 交回的 TOML 视图（`failed` 或 `abandoned` 时为空）。
- 按成员归属收拢：收的是本会话创建的子工作。拒绝码：`context-unavailable`、`not-ready`、`nothing-to-join`、`join-in-progress`、`interrupted`、`abandoned`。

## fission

```js
const group = await this.fission({ charge: "探索三种设计", lanes: 3 });
const { records } = await this.join();          // 收拢本组的 lane
```

- 同身份多 lane：每条 lane 是同一参与者对任务某一分片的执行，宿主负责收拢。
- 结果：`{ ok: true, groupId, lanes: [{ index, sessionId }] }`。
- 拒绝码：`fission-unavailable`、`lane-start-failed`、`not-ready`、`context-unavailable`、`invalid-prompt`。

## dag

```js
const outcome = await this.dag({ nodes: [
  { id: "spec",  charge: "写出接口规格" },
  { id: "impl",  charge: "实现它", deps: ["spec"] },
  { id: "probe", charge: "并行探索两种设计", deps: ["spec"], lanes: 2 },
] }, { 1: null, 2: "解析器清单" });
```

- 节点按依赖波次推进：本波就绪节点各自 fork，随后一次 `join` 收拢；下游节点的 prompt 会带上依赖节点的记录。
- 一份 `reportMapping` 发给所有节点：同一份映射交给所有节点工作，节点不得替换自己那一份。它是必填项——`Record<string, null|string>`，把锚点编号映到摘要或 `null`，宿主读作 `{ prefixUntil: 0, thenMap: reportMapping }`。缺省或非法以 `{ ok: false, code: "VIEW_DECLARATION_INVALID" }` 拒绝。
- 结果：`{ ok: true, results: { [id]: record }, skipped: [id...] }`。
- **节点失败会让程序 park**——与 `where` 同一条恢复通道。parked 载荷为 `{ dag, node, code, deps, done, pending }`；注入值决定：
  - `"retry"`——重跑该节点；
  - `"skip"`——标记跳过（下游看到 `null`）；
  - 其他任意值——直接当作该节点的结果继续；
  - `{ abort: reason }`——以该原因让程序失败。

## horizon

```js
const { agents, ptys } = await this.horizon();
```

- 谁还在你的视野里，以数据形式给出：`{ ok: true, agents: [{ label, status, target, workState, record, turnsUsed, turnsLeft, timeUsedMs, timeLeftMs }], ptys: [{ terminal }] }`。
- `status` 为 `returned` | `still-away` | `did-not-return`；`workState` 为 `latest` | `none` | `unavailable`，为 `latest` 时 `record` 带上最新工作的正文。
- 四个校准数说的是这个参与者已经花了多少、大概还需要多少：按委任者估算已用与剩余的回合数、已用的 wall-clock、以及按它已经显示出的节奏估计的剩余时间。任一来源缺失时，四个数都是 `0`。
- 拒绝：`{ ok: false, code: "horizon-unavailable" }`。

## advisor

```js
const advice = await this.advisor("Is the caching layer's eviction policy sound?", { 1: null });
```

- 请教这个 session 的**专属顾问**——一位不持有任何工具、只以散文作答的评审者——用在敲定路线之前，或宣布收工之前。同一个 session 永远落到同一个顾问，之后的调用只追加新材料与新 intent，所以这条线是延续的。
- `intent` 是独立的问题 prompt。`reportMapping` 必填，形状是 `Record<string, null|string>`，把锚点编号映到摘要或 `null`；宿主读作 `{ prefixUntil: 0, thenMap: reportMapping }`，也就是 `prefixUntil` 固定为 `0` 的统一声明。先按当前存活区间生成完整映射，再按宿主掌握的已交付事实取 delta。首次拿到完整材料；这份映射不是交付边界。映射要覆盖所有可见编号，null 保留，摘要整段替换，漏项按 null。映射与 intent 分别发送，不合并。
- 结果：`{ ok: true, advice }`（散文即数据）或 `{ ok: false, code: "advisor-unavailable", reason }`；报告映射缺省或非法时以 `VIEW_DECLARATION_INVALID` 拒绝。
- 把回答当作指引：一个 plan、一个 correction，或一个 stop signal。这次请教本身不会往你的会话里写入任何东西。
