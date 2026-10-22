所有委托成员返回数据；宿主负责把结果排版为 TOML。

```js
{ ok: true, ... }           // 成功
{ ok: false, code: "..." }  // 拒绝
```

`fork`、`resume`、`commission`、`devops`、`dag`、`advisor` 的最后一个参数都是可选的 `carry` 数组，默认 `[]`。整数引用可见锚点的完整历史区间，字符串表示字面文本，按数组顺序传递。只加入对方需要的背景。

**父方本回合不传给子方。** 任务只写在 `charge` 或 `intent` 中，无需在 `carry` 里再说一遍。传递没有前缀参数；接收方看到的是排版清晰的 TOML 材料表。非法数组或不可见锚点返回 `VIEW_DECLARATION_INVALID`，不会创建部分工作。`join`、`horizon`、`fission` 不带 `carry`。

## fork

```js
await this.fork("为解析器模块建索引");
await this.fork({ charge: "为解析器建索引", name: "parser-index", calling: "engineer" }, [1, "只需列出公开入口"]);
```

- 载荷：字符串（任务）或 `{ charge, name?, calling?, keywords? }`；缺 `name` 时由任务文本派生。
- 结果：`{ ok: true, name, agentId, outcome }`，`outcome` 为 `created` | `nudged` | `commissioned`。
- 拒绝码：`name-required`、`charge-required`、`unknown-calling`、`calling-conflict`、`name-already-belongs`、`warm-start-unavailable`、`context-unavailable`、`not-a-delegation-role`、`placement-failed`、`dispatch-uncertain`。

## resume

```js
const resumed = await this.resume({ name: "audit-road", charge: "继续那轮审计" }, [4, "目前已核对解析入口"]);
```

- 载荷 `{ charge, name }` 把下一份任务交给已有的被委派者。`name` 必填；`calling` 不接受，因为这次延续已有工作。
- 结果：`{ ok: true, name, agentId, outcome }`。

## commission

```js
await this.commission({ charge: "独立审计解析器", name: "parser-audit" }, [1]);
```

- 把目的独立的工作托付给一条 Manager 道路。载荷是任务字符串或 `{ charge, name?, calling? }`，不含 `keywords`；缺 `name` 时由任务派生。`calling` 省略时按角色推导，显式给出与推导不一致时以 `unknown-calling` 拒绝。
- 结果：`{ ok: true, name, agentId, outcome }`。

## devops

```js
const answer = await this.devops("找出那条偶发失败的测试", [1]);
```

- 同步请教本任务绑定的 DevOps，等它交回调查结果后返回。没有另一个子工作要 `join`。`intent` 写调查问题。
- 结果：`{ ok, kind, status, returned: [{ text }] }`。席位尚未绑定或已关闭时以 `assessment-pending-for-devops` 拒绝。

## join

```js
const { records } = await this.join();
```

- 结果：`{ ok: true, records: [{ name, agentId, role, status, returned: [{ text }] }] }`。
- 收拢本会话创建的子工作。`status` 为 `completed` | `failed` | `abandoned` | `voided`；完成要求有效交付与已证明的终态同时成立。仍能交付的子工作保持活跃，`join` 等待；交付已不可能时以 `voided` 结清并报告一次。逐条阅读 `returned` 即得子方交回的材料与当前报告，非完成状态时该数组为空。
- 拒绝码：`context-unavailable`、`not-ready`、`nothing-to-join`、`join-in-progress`、`interrupted`、`abandoned`。

## fission

```js
const group = await this.fission({ charge: "探索三种设计", lanes: 3 });
const { records } = await this.join();
```

- 同身份多 lane：每条 lane 承担同一任务的一部分，宿主负责收拢。
- 结果：`{ ok: true, groupId, lanes: [{ index, sessionId }] }`。
- 拒绝码：`fission-unavailable`、`lane-start-failed`、`not-ready`、`context-unavailable`、`invalid-prompt`。

## dag

```js
const outcome = await this.dag({ nodes: [
  { id: "spec", charge: "写出接口规格" },
  { id: "impl", charge: "实现它", deps: ["spec"] },
  { id: "probe", charge: "并行探索两种设计", deps: ["spec"], lanes: 2 },
] }, [1, "解析器当前入口见所附记录"]);
```

- 节点按依赖波次推进：本波就绪节点各自 `fork`，随后一次 `join` 收拢；下游收到依赖节点的记录。
- 同一份 `carry` 发给全部节点，节点不得覆盖它。
- 结果：`{ ok: true, results: { [id]: record }, skipped: [id...] }`。
- 失败节点使程序 `park`，载荷为 `{ dag, node, code, deps, done, pending }`。注入 `"retry"` 重跑，`"skip"` 跳过（下游看到 `null`），`{ abort: reason }` 使程序失败，其他值作为该节点的替代结果。

## horizon

```js
const { agents, ptys } = await this.horizon();
```

- 结果：`{ ok: true, agents: [{ label, status, target, workState, record, turnsUsed, turnsLeft, timeUsedMs, timeLeftMs }], ptys: [{ terminal }] }`。
- `status` 为 `returned` | `still-away` | `did-not-return`；`workState` 为 `latest` | `none` | `unavailable`，为 `latest` 时 `record` 带上最新工作正文。
- 四个计量数表示已用与估计剩余的回合数、时间。任一来源缺失时四个数都是 `0`。
- 不可用时返回 `{ ok: false, code: "horizon-unavailable" }`。

## advisor

```js
const advice = await this.advisor("缓存淘汰策略是否合理？", [1, "目前采用 LRU"]);
```

- 请教本会话的专属顾问；顾问没有工具，以散文作答。`intent` 单独写问题，`carry` 补充判断需要的历史。
- 同一会话持续使用同一位顾问，宿主只追加尚未交过的材料和新问题，无需你维护交付游标。
- 结果：`{ ok: true, advice }` 或 `{ ok: false, code: "advisor-unavailable", reason }`。
- 把回答作为后续判断的依据；本次请教不会自行改写你的会话记录。
