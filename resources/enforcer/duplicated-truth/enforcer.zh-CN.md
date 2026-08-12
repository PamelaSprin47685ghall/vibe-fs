# duplicated-truth — Enforcer 中文版

## 定义
同一个事实有多个可独立写入、且都被当作 authoritative 的表示时，truth 被复制了。

问题不是“数据重复”。同一个事实可以有 cache、index、read model、API view、日志副本；只要它们是**单向派生**的，就没有多重主权。真正的缺陷是两个地方都能说“我这里改了，所以事实现在就是这样”。从这一刻起，disagreement 不再是异常，而成为合法可表示状态。

## 何时触发
- DB column 与 config file 都可修改同一个业务设置；
- 内存 map 与 journal 都可各自写入“当前状态”，重启时再猜谁更真；
- old/new representation 双写且双方都可独立接受 mutation；
- cache miss 能自己创造新值，而不是回到 source；
- 两个 subsystem 都维护同一个 lifecycle status，并靠 reconciliation 定期“对齐”。

## 不要误判
- projection/cache/read model 可完全从唯一 source 重建，不是 duplicated truth；
- event history 与当前 folded state 不是两个事实：后者若只是历史的函数，仍是一条 authority chain；
- display copy、metrics、logs 不能写回事实，不构成第二 owner；
- 两个不同事实长得一样，不应因为 shape 相似就合并。

## 刀口
问：**当两份表示不一致时，系统能否不靠 heuristic、不靠 timestamp、不靠“谁最后写”直接知道谁错了？**

如果不能，说明模型已经授权了多个 truth owner。

## 与近邻区分
`snapshot-as-truth` 是一个 derived representation 被抬成 authority；`duplicated-truth` 更一般：任何两个 writable authority 都算。

`compatibility-cruft` 可能保留两种 representation，但只要旧形态 decode-only、不能反向写回，就不必触发本规则。

## 例子
- 正例：feature flag 同时能在 DB 与 YAML 修改，应用启动时选“修改时间较新”的那个。
- 近邻：DB 是唯一 writer，Redis 只是可删除 cache。
- 反例：journal 是 fact source，snapshot 有 source digest，失配即丢弃重放。

## 提醒
同步机制不能治愈 duplicated truth。同步只是承认你允许两个权威先互相矛盾，再安排仪式收拾残局。
