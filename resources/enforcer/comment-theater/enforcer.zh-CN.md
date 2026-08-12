# comment-theater — Enforcer 中文版

## 定义
Comment theater 是让 prose 替 executable structure 承担本可机械表达的意义：解释坏名字、旁白下一行、为 tangled control flow 道歉、用“注意必须……”补一个类型/结构缺口。

Compiler 会约束代码，不会约束注释。把可编码的 invariant 留在 prose，就等于创建一份无法自动保持同步的第二 specification。

## 何时触发
- `// increment i` 复述下一行；
- comment 负责解释变量真实含义而不是 rename；
- comment 说某 field “只有 X 时有效”，类型仍允许其它状态；
- 长段 prose 解释 nested branch 为什么这样走；
- “ugly hack / must call before Y” 没有对应结构化约束。

## 不要误判
- rationale、外部 protocol quirk、数学推导、安全约束、incident provenance；
- public API contract 中签名无法表达的 caller obligation；
- spec/source link；
- legal/license text。

## 刀口
删掉 comment 后若代码不清楚，先问：名字、类型、case、function boundary、test 能不能承载同一事实？能，就修结构；不能，comment 才真正有工作。

## 提醒
Comment 应保存**代码无法自然编码的知识**，而不是替代码完成表达能力本来就足够完成的工作。
