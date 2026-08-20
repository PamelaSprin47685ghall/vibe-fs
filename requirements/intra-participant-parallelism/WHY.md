# intra-participant-parallelism — WHY

## 领域价值与核心矛盾

当一项工作内部存在可分离的并发执行切片时，层级委托（Delegation）会将“增加并发执行容量”错误表达为“创建新的 participant”，导致父子拓扑、句柄、join 义务与责任所有权发生不必要的切分。而单会话内并发模型流又会破坏单线 attempt、前缀稳定性与转录假设。

本包的核心价值在于：**允许同一个 logical participant 临时拥有多个平等的 execution presents，并在整个裂变生命周期中保持 identity、authority、子会话管辖权、外部责任与最终 completion 归属严格唯一**。

## 核心不变量

1. **一人多 Present**：Fission 仅增加物理执行通路（lanes），不创建新的业务身份或句柄；所有 lanes 共享同一个 logical identity 与 responsibility owner。
2. **全有或全无准入（All-or-None Admission）**：所有 lanes 的创建与启动必须原子生效；任一失败则全量回滚，旧 caller 继续运行且不受干扰。
3. **静默中断与单点收口**：旧物理执行者在全量 lanes 建立后静默退休，不产生业务中止；整个 group 最终仅向父级交付一次 terminal completion。
4. **既有债权广播与后续债权亲和**：裂变前的未完成子任务向所有 lanes 广播；裂变后各 lane 新发起的子任务归属于该发起 lane。
5. **Keyed 结果收敛**：各 lane 的工作记录按 lane 索引作为唯一 key 进行合并，以集合并集而非到达时序确定最终产物。

## 破坏后果

- **身份与拓扑发散**：增加并发执行导致外部观察到多个分叉主体，责任归属与完成信箱混乱。
- **孤儿状态与死锁**：部分 lane 启动失败却中断了原执行者，或 group 收敛依赖偶发的时序状态导致无法终结。
- **越权与污染**：用户直面根会话（root session）被裂变替换，导致交互上下文失联。
