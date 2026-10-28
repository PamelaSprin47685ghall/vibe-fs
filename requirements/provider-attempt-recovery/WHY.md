# provider-attempt-recovery — WHY

provider 一次失败不应抹掉任务，也不应引发无限重试。由逻辑运行持有的失败预算约束自动恢复，不让后来的无关任务替它买单；精确的失败身份防止并发观察者与迟到回调把同一预算花两次。

换执行者不等于换做事的人与授权。持久的参与者身份让责任、语言与上下文在重试之间保持。失败计账与目标选择回答的是不同问题：计账只数数，选目标只看模型配置，两者互不越界。

宿主传输活动、不可用的回复内容与确认的 provider 失败是三种不同证据。类型化策略许可防止清理动作、含混的接受或诊断文本凭空造出新请求；RequestKind 防止维护性工作或投机成功冒充业务任务恢复。

已提交的失败解释了为什么需要恢复，但不证明宿主已停止发送、也不证明新进程可以续接回调。确切的终态观察防止物理尝试重叠。反过来，已接受却未执行的输入是真实义务：要么用所需能力恢复那份材料，要么报告它的终局失败。

## DEPENDS ON

- `participant-identity`
- `execution-failure-policy`
- `execution-model-routing`
- `interaction-authority`
- `context-compression`
- `prefix-stability`
