# partial-write-assumption — Main

删掉你无法观察的 recovery state。

从 storage/effect boundary 的真实 contract 出发，而不是从你想象的底层物理实现出发。把 caller 真正能区分的 outcome 全部列出来，recovery 只围绕这组状态设计。

很多 boundary 的诚实 state space 比工程师直觉里小：

```text
known committed
known not committed
unknown
```

`Unknown` 不是建模失败，反而经常是 timeout、process death、transport loss 后最准确的事实。保留它，直到 authoritative lookup、idempotency protocol、transaction query、checksum、commit marker 等 boundary-owned evidence 把它解决。

如果 torn/partial data 真的可能出现，就建模**证明 partial 的证据**，例如：

- length prefix + checksum；
- explicit commit marker；
- WAL/page sequence semantics；
- store 暴露的 transaction state；
- 能区分 accepted/rejected 的 provider status endpoint；
- durable multipart manifest 与 per-part commit。

如果 abstraction 已承诺 atomicity，就不要根据“发生过 crash”、elapsed time、可疑 file size、或者底层 folklore 自己推断 partial。

常见假修复：

- 任意 crash 后“保险起见”truncate 最后一次 append；
- 从未校验 checksum 就 rewrite record；
- 加 `HalfWritten` state，但没有任何 API outcome 能诚实构造它；
- 用 timeout duration 猜 write 进行到哪里；
- 为了 second-guess atomic abstraction，把 filesystem internals 泄漏给 caller；
- test mock 出 impossible outcome，再把支持这些 mock 当 resilience；
- 因为不喜欢 `Unknown`，就把它压成某个 guessed physical state。

验证也应从 contract 推导。Fault-inject 每一个**documented outcome**，证明 committed / not-committed / unknown / 明确 partial/corrupt 状态都能正确恢复。

还要做 abstraction boundary 检查：caller 不应越过 store 去看 contract 故意隐藏的 implementation residue。如果 recovery 真需要那条信息，就让 abstraction 正式暴露 typed fact；否则就是 caller 越权。

特别警惕 destructive recovery。Truncate/delete/rewrite 必须有 positive corruption evidence 或 owner 明确规则。**“我不确定”不是“这条 history 可以删”的证据。**

完成时应形成一一对应：

```text
observable boundary outcome ↔ recovery branch
```

现实状态一个不少；幻想状态一个不多；不存在 precondition 只能写成“内部大概发生了这个”的 branch。

> Recovery 更安全，往往不是因为想象更多，而是因为想象更少、证据更强。