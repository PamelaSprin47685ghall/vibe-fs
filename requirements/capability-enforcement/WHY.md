# capability-enforcement — WHY

## 领域动力与核心张力

Office 的权能由其后果模型决定，但在落地到执行层时，面临**模型可见模式（Provider-visible Schema）**与**运行时拦截门禁（Runtime Execution Gate）**可能发生分叉的根本张力：

```text
Schema 有、Gate 无   ──► 产生虚假承诺：模型可见工具但调用即报错
Gate 有、Schema 无   ──► 产生安全隐患：模型虽不可见但可通过伪造调用越权执行
```

若手写多套角色与工具映射，或仅依赖单层机制，配置漂移必然发生。

`capability-enforcement` 的核心不变量：
- **同源派生**：Schema 呈现与 Runtime Gate 必须从唯一的 `Roles.permissions` 权威推导，严禁维护第二份映射矩阵。
- **投影只收窄不扩大**：基于请求类型（RequestKind）的投影可以根据上下文收窄能力，但绝不得突破 Office 的固有权能上限。
- **档位等权**：同一 Office 的 fast 档与 deep 档拥有完全相同的工具权限。
- **内部工具隔离**：运行时合成的内部角色工具（如 Blogger 的 `chronicle`、Bookkeeper 的 `js-bookkeeper`）绝不进入未受托角色的工具面。
- **四层同构**：面向编程的 `js-*` 工具在类型方法、描述文案、示例代码与运行时门禁四层保持严格同构。
- **双层 Fail-Closed**：角色未决时拒绝一切执行；Host 配置异常时优先落地 deny 默认并安全终止进程。
- **权威值分型**：`Evidence / Decision / Witness / Capability / Receipt / PhysicalHandle` 各自只表达一种因果位置；证据与收据描述已经观察或已经发生的事实，能力才准许下一次动作，物理句柄只代表当前进程资源。
- **所有者发行**：权威值由拥有其因果不变量的模块单点发行。调用者不得复刻构造器、从 bool 猜测一次性消费结果，或让 witness 绕过当前 subject/version/digest 准入直接触发效果。
- **精确范围与非耐久能力**：每个权威值显式绑定 subject、版本/序列与必要 digest，声明 freshness 和 multiplicity。进程能力、permit 与物理句柄永不进入 Fact/Event/codec/JSON；重启后只能从当前事实重新准入，不能恢复旧能力。

## 破裂后果

- Schema 与运行时门禁脱节，导致越权执行或误导模型决策。
- 派生副本（如 StrengthReplica）或低档位角色获得超出预期的修改权限。
- 内部专用工具泄漏至交互式会话，导致内部状态被非法操纵。
- Host 配置异常时降级为全局放行，造成系统级安全漏洞。
- stale witness、跨 owner permit 或重复消费被当成成功，令旧观察授权当前物理效果。
- 将进程能力持久化后在崩溃恢复中复活，使重启绕过当前事实与新鲜准入。

## 边界与关系

- `office-capability`：定义各职位的权能事实；本包负责在执行层强制同构执行。
- `participant-identity`：提供身份事实；本包消费其角色分类。
- `attention-regulation`、`concern-routing` 与 `institutional-learning`：提供特定交互效用动作的边界；本包负责其工具可见性与门禁投影。
- `participant-horizon`：定义模型视界的信息准入；本包负责工具面的具体暴露与阻断。

## DEPENDS ON

- `office-capability`
- `participant-identity`
- `attention-regulation`
- `concern-routing`
- `institutional-learning`
