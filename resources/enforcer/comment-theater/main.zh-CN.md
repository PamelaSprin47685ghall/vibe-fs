# comment-theater — Main 中文版

## 现在该做什么
把能进入 executable structure 的 intent 移进去：rename、收紧 type、拆清 control flow、建立 constructor/invariant/test。只保留无法由结构自然表达的 rationale 与外部事实。

## 为什么这很重要
“代码 + 旁白”会产生两个变化速度不同的 truth channel。实现改了，comment 还在；comment 改了，compiler 也不会告诉你实现没跟上。

## 常见假修复
- 把 comment 写得更长。
- 建文档解释一个本可由 type 禁止的 illegal state。
- 每个 hack 都写一句 apology，仿佛被承认就不再是 debt。
- 为了“zero comments”删掉真正的 rationale；本规则反对 theater，不反对知识。

## 验证
剩下的 comment 应能回答“为什么/受什么外部事实约束”，而不是“这一行做什么”。

## 完成条件
结构承担可执行意义，注释只保存结构无法自己证明的知识。
