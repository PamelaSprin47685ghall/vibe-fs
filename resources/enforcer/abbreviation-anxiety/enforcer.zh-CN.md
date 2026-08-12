# abbreviation-anxiety — Enforcer 中文版

## 定义
缩写的问题从来不是“名字短”。真正的缺陷是：代码引入了领域本来不存在的私有密码，读者每次遇到它都必须先解码，才能开始理解。

`HTTP/URL/UUID` 之类公开词汇无需翻译；`prcAmt/xfrCtx/mgrCfg` 之类局部发明则把一个概念变成两种语言：领域语言和仓库暗号。

## 何时触发
- 一个承载 domain meaning 的标识符，熟悉领域的人也无法立即展开；
- 同一缩写在仓库里可能有多个扩展；
- 读者必须靠附近代码、comment 或 glossary 才知道名字是什么；
- 缩写节省了几个字符，却让 grep、review、口头讨论都多一次翻译。

## 不要误判
- 行业标准缩写、协议名、官方产品名保留原拼写；
- wire/generated code 的正式字段名不要擅自扩写；
- `i/j/x/y` 这类局部数学/循环变量没有 domain meaning 时无须长名；
- 名字完整但含义错误，应归 `misleading-name`。

## 刀口
把实现遮住，只看名字。一个 competent domain reader 能否不借上下文就知道它说什么？不能，就说明代码强迫读者学习私有 vocabulary。

## 例子
- 正例：`acctRecnCtx`，需要先猜 account/reconciliation/context。
- 近邻：`httpRequest`，HTTP 已是公开领域词。
- 反例：`reconciliationContext`，直接使用领域名称。

## 提醒
好的短名来自共享语言，不来自把词砍掉几个字母。不要把 typing 成本省给作者，再把 decoding 成本永久转嫁给每个读者。
