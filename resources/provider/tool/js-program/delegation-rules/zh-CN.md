所有委托成员都返回**数据**，不返回渲染后的文字；渲染由宿主替你完成，返回值保持 JSON。

```
{ ok: true, ... }          成功
{ ok: false, code: "..." }  拒绝（稳定的 kebab 码）
```

## fork

```js
await js.fork("为解析器模块建索引");                          // 只给任务
await js.fork({ charge: "为解析器建索引", name: "parser-index", calling: "engineer" });
```

- 载荷：字符串（任务）或 `{ charge, name?, calling?, keywords? }`；缺 `name` 时由任务文本派生。
- 结果：`{ ok: true, name, agentId, outcome }`，`outcome` 为 `created` | `nudged` | `commissioned`。
- 拒绝码：`name-required`、`charge-required`、`unknown-calling`、`calling-conflict`、`name-already-belongs`、`warm-start-unavailable`、`context-unavailable`、`not-a-delegation-role`、`placement-failed`、`dispatch-uncertain`。

## join

```js
const { records, byName } = await js.join();
```

- 结果：`{ ok: true, records: [{ name, agentId, role, status, workRecord }], byName: { [name]: record } }`。
- `status` 为 `completed` | `undelivered` | `failed` | `abandoned`；`workRecord` 是子工作经 `js.deliver` 交付的终值记录（`undelivered`、`failed` 或 `abandoned` 时为空）。
- 按成员归属收拢：收的是本会话创建的子工作。拒绝码：`context-unavailable`、`not-ready`、`nothing-to-join`、`join-in-progress`、`interrupted`、`abandoned`。

## fission

```js
const group = await js.fission({ charge: "探索三种设计", lanes: 3 });
const { records } = await js.join();          // 收拢本组的 lane
```

- 同身份多 lane：每条 lane 是同一参与者对任务某一分片的执行，宿主负责收拢。
- 结果：`{ ok: true, groupId, lanes: [{ index, sessionId }] }`。
- 拒绝码：`fission-unavailable`、`lane-start-failed`、`not-ready`、`context-unavailable`、`invalid-prompt`。

## dag

```js
const outcome = await js.dag({ nodes: [
  { id: "spec",  charge: "写出接口规格" },
  { id: "impl",  charge: "实现它", deps: ["spec"] },
  { id: "probe", charge: "并行探索两种设计", deps: ["spec"], lanes: 2 },
] });
```

- 节点按依赖波次推进：本波就绪节点各自 fork，随后一次 `join` 收拢；下游节点的 prompt 会带上依赖节点的记录。
- 结果：`{ ok: true, results: { [id]: record }, skipped: [id...] }`。
- **节点失败会让程序 park**——与 `where` 同一条恢复通道。parked 载荷为 `{ dag, node, code, deps, done, pending }`；注入值决定：
  - `"retry"`——重跑该节点；
  - `"skip"`——标记跳过（下游看到 `null`）；
  - 其他任意值——直接当作该节点的结果继续；
  - `{ abort: reason }`——以该原因让程序失败。
