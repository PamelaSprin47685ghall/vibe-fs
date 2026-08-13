file(path, matches = []) 读取本次 transaction 的不可变 UTF-8 snapshot，
可按序解析 anchor，并返回不可变 FileView。

matches 是 Array<[beginAnchor, endAnchor, pattern]>，pattern 为非空
string 或 RegExp。Anchor 是位置名，不是匹配到的文本。
每个 FileView 都有内置 anchor ^（文件首）与 $（文件尾）。不要
把 ^ 或 $ 声明为自定义名。

有序匹配：每个 pattern 从当前 cursor 向前搜索；匹配后
cursor = match.end。源文本重复出现时不必全局唯一。
调用方 RegExp 的 g/y 标志与 lastIndex 被忽略；匹配使用自己的向前
搜索。零宽 RegExp 合法（begin 偏移可以等于 end 偏移）；begin
与 end 名称仍必须不同。

Anchor 声明拒绝：空名称；保留的 ^/$；重复名称；同一声明中 begin == end；
空字符串 pattern。按声明顺序找不到 pattern 则失败。

file.text(from, to) — 默认 text(from = "^", to = "$") — 返回两个已解析
anchor 之间的精确原文 substring。字符串 pattern 内容必须非空。
反向切片失败。FileView 不可变：rewrite() 不会改变先前
返回的视图。

from/to 可以是已声明名、^、$，或临时位移 name+N / name-N
（例：h1+200、h1-40、$+0）。N 是已解码 JS 字符串上的下标增量，
与 String.length / slice 同一单位，不是行号，也不是 UTF-8 字节数。
file_len 即 source.length。位移不入库。若整串已是
已声明名，则该精确名获胜。否则最后一个 [+-]digits 是 delta；
基名递归解析。结果 caret 裁剪到闭区间
[0, file_len]，因此 $+N 与 ^-N 停在 EOF / 文件首。

推荐工作流：
1. 只声明定位 span 所需的最少 begin/end anchor（读或编辑）。
2. 让 Host 解析这些位置。
3. 用 text(from, to) 读取。相邻标题可切出正文：
   text("h1end", "h2")。命中附近窗口是 text("h1", "h1+200")
   （200 是字符串下标，不是 200 行）。
4. 编辑时，用 text(...) 切片加上新内容拼出完整的结果文件。
5. 只有在 anchor-and-splice 确实不便时才使用 indexOf / replaceAll。

优先：
  f.text("^", "begin") + "newString" + f.text("end", "$")
