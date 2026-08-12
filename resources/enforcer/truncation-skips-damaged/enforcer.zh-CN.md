# truncation-skips-damaged — Enforcer 中文版

## 定义
Durable history 是 causal chain，不是一袋互相独立的 records。Interior corruption 后跳过损坏项继续 replay，等于在缺失前提的情况下继续推导 suffix，制造一条从未存在过的连续历史。

Tail tear 与 interior damage 必须严格区分：若 storage contract 能证明最后一条只是未提交尾巴，可以截掉；一旦损坏之后还有 verified committed record，prefix 已断，后续语义就没有可信起点。

## 何时触发
- checksum fail record 12，却扫描 record 13 继续；
- 找“下一个看起来像 frame header”的位置恢复；
- zero-fill/skip malformed interior entry 后继续 fold；
- 把所有 corruption 都统一处理成“truncate and proceed”。

## 不要误判
- storage contract 明确允许 torn final record，且能证明 verified committed prefix 在它之前结束；
- first interior inconsistency 直接 fail closed；
- uncommitted speculative buffer 被丢弃；
- corruption 发生在尚未进入 authoritative history 的 replica/staging。

## 刀口
找到第一处 damage：**它之后是否存在一条被证明 committed 的记录？** 有，就不能把 gap 当无事发生；suffix 的解释前提已经丢失。

## 提醒
只能截掉“被证明从未 committed 的尾巴”。不能为了启动成功，把历史中间缺失的一段假装成不存在。
