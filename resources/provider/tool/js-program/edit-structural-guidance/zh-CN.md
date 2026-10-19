Read 可用时，结构重组才使用 {{fileMatches}} + text() + rewrite()。

{{fileCall}} 把底稿读成不可变 UTF-8 快照，并解析**有序 anchor**；返回不可变 FileView。

matches 是 Array<[beginAnchor, endAnchor, pattern]>，pattern 为非空字符串或 RegExp。anchor 是位置名，不是匹配文本。每个 FileView 内建 ^（文件起点）与 $（文件终点），不得声明这两个名字。

按序匹配：每个 pattern 从当前游标开始搜索；命中后 cursor = match.end。重复文本不需要全局唯一。调用方 RegExp 的 g/y 标志与 lastIndex 被忽略。

text(from, to) —— 默认 text("^", "$") —— 返回两个已解析 anchor 之间的原样子串；反向切片失败。from/to 可以是已声明的名字、^、$，或临时位移 `name+N / name-N`（例如 h1+200、$+0）。N 是 JS 字符串下标增量——与 String.length / slice 同一单位——**不是行号**，也不是 UTF-8 字节数。位移不存储；结算后的游标夹在 [0, file_len] 闭区间内。

推荐流程：
1. 声明定位 span 所需的最小 begin/end anchor（读或写都用它）。
2. 让 Host 解析这些位置。
3. 用 text(from, to) 读取。相邻标题切出正文：text("h1end", "h2")。
4. 要写就用 text(...) 切片加新内容拼出完整结果文件。
5. 近似匹配只产生诊断，绝不获得写权限。

如果你正准备手算结构边界，先问：{{fileMatches}} + 有序 anchor + text() 是否已经拥有这层边界？如果是，就用它。后面一旦结果违反明显不变量，把它当成停止信号，不要把它解释成「再猜一次也许就好了」。
