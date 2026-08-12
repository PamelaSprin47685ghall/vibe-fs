# math-flavored-name — Enforcer 中文版

## 定义
数学味命名的问题不是用了 `x/Δ/σ`，而是代码并没有一个共同 formal model，却借数学符号制造“高度抽象”的外观，把普通 domain concept 压成只有作者知道的记号。

数学符号之所以能压缩，是因为读者共享定义。没有共享模型时，压缩只是把字符成本换成 reverse-engineering 成本。

## 何时触发
- 普通 billing/workflow/domain code 用 `x/f/σ/monoid` 代替实际业务名；
- reader 必须追 assignment 才知道 symbol 是什么；
- algebraic vocabulary 只是 decoration，不对应可陈述的 law；
- public API 暴露 notation，但 domain discussion 从不用它。

## 不要误判
- Kalman filter、linear algebra、parser algebra 等真正 formal scope 使用标准 notation；
- 几行 loop 的 `i/j`；
- `map/fold` 若模块确实使用其标准 algebra；
- 真正 false guarantee 应归 `misleading-name`。

## 刀口
一个熟悉相关算法/领域的人会自然用这个符号指同一概念吗？不会，就别拿数学外观替 domain language。

## 提醒
Notation 只有在共享数学模型存在时才是压缩；否则只是高密度的私有缩写。
