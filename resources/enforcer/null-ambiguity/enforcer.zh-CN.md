# null-ambiguity — Enforcer

## 定义
当一个“没有值”的 representation，被拿来代表多个需要不同 meaning、authority 或 caller action 的 outcomes 时，就是 null ambiguity。

`null`、`None`、missing property、empty string、collection entry absent 都可以是完全合法的 optionality 表示。真正 defect 出现在 producer 明明知道更具体 cause，却把它们全压成 absence：**not found、not authorized、not loaded yet、failed to load、not applicable、intentionally redacted、expired、cancelled**。

这些 cause 一旦都变成同一个 empty value，downstream code 就只能开始猜“为什么没有”。

## 支配原则
只有当这个 boundary 上真的只有一种 relevant absence meaning 时，optionality 才是诚实的 domain statement。

`Option<User>` 可以诚实表达“用户可能存在，也可能不存在”。但如果 `None` 同时代表“caller 无权知道它是否存在”“backend failed”“lookup cancelled”，它就开始说谎。这些 worlds 的 cause 与 next action 完全不同。

判断标准不是 style，而是 behavior：

> 如果两种 absence cause 会让正确 caller 采取不同动作，它们跨过 boundary 时就必须保持可区分。

不要为没人需要的区别发明 cases。但也不要先把 distinction 毁掉，再让 caller 从 HTTP status、side-channel flag、log、timing 或 prose 把它拼回来。

## 何时触发
当 caller 必须依赖 context 才能推断 absence cause，而 return value 本身只保留 present/absent 时触发。常见形式：

- `null` 同时表示 “not found” 与 “forbidden”，caller 另看 status/error field；
- async operation 前后都用 `None`，前者表示“尚未 loaded”，后者表示“loaded but missing”；
- cache miss、backend failure、negative lookup 全变 `None`，于是 retry/fallback 错乱；
- empty string 根据 record history 可能表示 unset、redacted、legacy malformed；
- optional data + sibling booleans (`wasFound/wasAuthorized/didFail`) 重新拼 result kind；
- function catch exception 后 return null，让 failure 与 legitimate absence 无区别；
- UI/state 无法区分 loading 与 empty，导致错误 empty-state 闪烁；
- persistence decode 把 unknown/invalid enum 映射成 `None`，corruption 被洗成正常 optionality。

## 不应触发
- 真的只有一种 semantic absence，而且所有正确 caller 都完全相同处理。
- Lower-level optional 在进入 behavior 会分叉的 boundary 前，立刻被包成 richer typed result。
- Security 有意隐藏 absence reason，而且**所有 caller 都被要求 identical behavior**；这时 indistinguishability 是 contract，不是 ambiguity。
- Optional field 精确表示“这个 datum 对该 case 不适用”，没有第二种 cause 共用 representation。
- Collection lookup 只承诺一个已验证 in-memory map 中 key presence，transport/auth/failure semantics 早已处理完。

## 与相邻规则区分
`illegal-state-representable` 看 contradictory field products。Null ambiguity 常会诱发 `nullable value + status flags`，但更早的 root wound 是 distinct absence outcomes 被 collapse。

`expected-failure-as-exception` 看 expected failure 走错 channel。Typed result 可能同时修两者，但中心信息损失是“多个 absence meanings 变一个”时用本规则。

`stringly-typed-error` 是后来用 prose 重建 missing distinction。Collapsed result 用 null ambiguity；机器开始 parse 文本做 control flow 时，再用 stringly error。

## 判定程序
在 producer boundary 列出所有可能 absence reasons。

对每个 reason 写 caller 正确动作：retry、404、403、show empty、keep loading、abort、log corruption、fallback、do nothing。

只有当**所有 relevant callers 本来就应当 identical treatment**时，才把 reasons 合并。

如果当前一个 `None/null` 包含多个 behavior groups，这个 boundary 正在丢 required information。

最后做 anti-overmodeling 检查：caller 真的需要区别吗？不需要，就继续用简单 Option。

## 例子
- positive：`getDocument(): Document?` 对 missing、forbidden、backend timeout、decryption failure 全 return null，caller 只能看旁边 log/status 猜。
- positive：UI model 用 `items: Item[] | null`，null 同时是 loading 与 request failed，spinner/error 还要靠另一个 bool。
- positive：cache API 对 key absent 与 cache unavailable 都 `None`，caller 把 outage 当普通 miss。
- near-miss：`Map.tryFind` 只表示一个 valid in-memory map 中有没有 key，Option 完全足够。
- near-miss：security API 有意对 missing/forbidden 都返回同一 `NotAvailable`，防止 caller 探测 existence；这个 indistinguishability 是明确 policy。
- counterexample：`Found value | NotFound | Forbidden | Unavailable cause` 在 cause 仍被知道时就保留 distinction。

## Nudge
“No value” 不是解释。

如果 absence 会改变 caller 下一步该做什么，就把 reason 保留到那个 decision 真正发生为止。
