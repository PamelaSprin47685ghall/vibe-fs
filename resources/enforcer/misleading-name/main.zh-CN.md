# misleading-name — Main 中文版

## 现在该做什么

让 name contract 与 real contract 对齐：若强 guarantee 本来就是目标，增强实现；若现实现才是目标，rename 到真实语义。同步 public API、tests、docs、events，别保留会继续传播谎言的 alias。

## 为什么重要

False name 会把 caveat 成本乘以所有 call sites。每个读者都必须知道“虽然叫 durable 但其实不 durable”，这说明名字已经成为 anti-documentation。

## 修复策略

用平实语言命名真实的 domain fact、owner 与 guarantee。同步更新 public types、tests、docs 与 call sites，让词汇收敛，而不是保留继续传播谎言的 misleading aliases。

## 决策分支

- 若更强的名字才是 intended contract，增强实现直到声明为真。
- 若现实现才是 intended contract，rename 让名字匹配那个更弱的 guarantee。

## 常见假修复

- 加 comment “despite the name…”。
- 前缀 `Real/Actual/Safe`，实现仍无对应 guarantee。
- 只改内部名，provider/public surface 继续说旧谎。
- 为“兼容”保留 misleading alias，却没有真实 consumer。

## 验证

让不熟实现的人只看名称与类型，写出他们认为存在的 guarantees；应与 tests/implementation 实际证明的内容一致。

## 完成条件

名字是可靠的 compressed contract，不需要团队记忆一套“名字虽这么叫但别当真”的 caveats。