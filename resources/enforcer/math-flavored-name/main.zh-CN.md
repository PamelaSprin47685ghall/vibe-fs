# math-flavored-name — Main 中文版

## 现在该做什么
普通 domain code 使用 domain nouns/actions；只有在窄而真实的 formal algorithm scope 中保留标准数学 notation，并让公式/类型把 symbol 的意义固定下来。

## 为什么这很重要
伪数学命名经常制造“抽象感”，却不提供数学带来的 law、proof 或共同词汇。读者只能先解码符号，再回到本来可以直接写出的业务概念。

## 常见假修复
- 把 `x` 换成 `value/data/item`，仍然没有语义。
- 加 glossary 解释 `σ`，继续让 public API 使用它。
- 为显得函数式给普通 workflow 起 `monoid/functor` 名，但没有对应 law。
- 反过来把真正算法里的标准符号全部改成长业务名，破坏算法可读性。

## 验证
在不读实现的情况下，declaration/call site 应能告诉读者这是哪个 domain fact；formal scope 则应能清楚映射到标准公式。

## 完成条件
数学符号只压缩真正共享的数学；其它代码直接说业务世界里的名字。
