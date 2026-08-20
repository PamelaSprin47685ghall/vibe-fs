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

## 破裂后果

- Schema 与运行时门禁脱节，导致越权执行或误导模型决策。
- 派生副本（如 StrengthReplica）或低档位角色获得超出预期的修改权限。
- 内部专用工具泄漏至交互式会话，导致内部状态被非法操纵。
- Host 配置异常时降级为全局放行，造成系统级安全漏洞。

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
