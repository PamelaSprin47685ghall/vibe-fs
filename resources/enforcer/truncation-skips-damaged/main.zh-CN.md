# truncation-skips-damaged — Main 中文版

## 现在该做什么
Interior corruption 一律 fail recovery；只有 storage protocol 能证明损坏位于未提交 final tail 时，才允许精确截掉该 suffix。需要恢复 interior damage 时，走 authoritative backup/repair protocol，不做 heuristic continuation。

## 为什么这很重要
每个 later event 都建立在 earlier folded state 上。跳过一条 interior fact 后继续 replay，得到的 state 没有合法 historical derivation；“程序启动成功”只是制造了一段伪历史。

## 常见假修复
- 扫描下一个 plausible frame boundary。
- zero-fill gap。
- 为“安全”从 damage 处把所有后续 committed history 一刀截掉。
- 根据 file size/timestamp 猜 tail 是否 committed，而 storage contract 没提供这种证据。

## 验证
分别 corrupt final torn tail 与 interior committed record：前者仅在 contract 允许时精确 truncate；后者必须 deterministic fail closed。任何 replayed record 都必须建立在完整 verified prefix 上。

## 完成条件
Recovery 不再跨越历史空洞制造连续性；每个被应用的 fact 都有完整、可信的 committed prefix。
