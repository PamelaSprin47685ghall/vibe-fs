# crash-reconciliation 的测试与证明范围

WHAT 是合同。这里说明当前证据，不为恢复增加隐含规则。用正式 `requirements/verification-system/tests/run.mjs` 选择本目录编号文件；先完成标准构建。TODO 表示尚未证明，不能计为验收通过。

## 当前实际执行

| 文件 | 证明对象 | 尚不能推出 |
|---|---|---|
| 001、006、008 | 实际 quiescence gate 的 owner 隔离、单次消费、active tool、撤销、exact release | OS 重启，真实 Host abort 解码及物理发送处的完整授权链 |
| 002、005、009、010、012 | 实际 child resolver、completion decoder、handle projection 和恢复结果映射 | 磁盘恢复、blob-before-fact、实际 join 消费，或整个完成投递幂等 |
| 003、007 | 实际恢复决策及 TurnUnknown 不作为业务终态发布 | 未知外部 effect 的物理查询和全体普通入口重入 |
| 011、013、014 | 实际闭包、permit membership、合并与授权函数 | 每次生产 join/await 都调用检查，或已有全家族的物理恢复证据 |
| 015 | 实际附加会话分类及 SyncDelegate 的观察/创建端口，包含并发共用创建 | 重启替换时先证明消失、Close 再 Link 的完整链 |
| 016 | 实际 Blogger scope 的显式 dispose 取消 waiter，新 scope 不继承 flight | 真实进程死亡后的全部能力清空 |
| 018 | 实际插件 config hook 不注册显式续传命令；另保留旧文件名的结构审计 | 真正重启归位、中断历史可见性、全部观察者不触发副作用 |
| 020 | 实际 canonical codec/fold、child load selection/void、取消结算桥接、JoinDrain 与 Blogger stale 判定 | 持久 append 成功、插件加载顺序、真实命令不重放、模型绑定及进程崩溃 |
| 021 | durable handle 按 id/byname 查询、真实 binding cache fallback、由已折叠 Fission fact 解析 lane | 所有 reuse/placement/await 入口的接线、真实重启、物理资源所有权 |

011 的接缝构造已授权的输入夹具，调用生产 permit/membership 函数；它不实际恢复这些成员。009 的 trace 检查器确实拒绝所给的乱序夹具，但合法 fixture 不是产品真实执行记录。所谓 `crashScenario` 的旧函数名实际仅驱动 handle projection，002/012 不再把它称为进程崩溃证明。

015 的受控 SessionPort 在观察发送后明确拒绝自己挂起的请求，并等待调用结束。这样关闭测试自己创建的生命周期，不扩大正式监督时限，也不伪造 Host 已接纳。

## 撤下的误导证据与缺口

004、017、019 及 020 的整链部分保留 TODO。原 JoinSurface 自行复制校验规则，未调用实际 join；旧施工已删除并同步编译和清单。原 DevOps crash helper 虽启动进程，却未运行命令，把“在途”“未重放”“唯一权威”直接写成固定答案；已删除。类型/源码名称、空数组、常量返回和独立调用的拼接，不再冒充整个恢复证明。

020/021 的 LoadRecoverySurface 只转换 canonical fact、调用实际 owner 并投影结果。JoinDrain 的受控 append port 将实际产生的事实交 production Fold；没有写磁盘，不声称 CAS 或 crash 持久性。主动取消产生可收取 completion；加载 ChildRunVoided 不产生 completion，二者不是同一场景。Blogger 在同一已打开请求的投影上追加 abandon，再证明下一请求可物化，不用空投影自证。

020 的追加失败回归把受控 IJournalWriter 接到真实 AgentJournal，再调用 ChildWorkRecovery。分别观察 writer 不可用与提交结果未知：必须传出 JournalAppendException，不能宣称已结算或继续下一条；空投影是无需追加即可成功的对照。这不证明真实磁盘故障或插件加载在普通任务之前完成；后者仍是整链 TODO。

绑定/Fission 测试会在 finally 移除自己的 resolver/cache。canonical journal JSON 是持久协议载体，不是 Fable 内部 tag/fields 对象。018 的旧 `/continue` 材料链随新版合同退役；本轮逐项覆盖迁移表在 `proposals/20模块迁移-恢复与委托-2026-09-28.md`。

完整进程中断、持久事实重开、物理发送/完成提交切点见 GAP-149。33 的真实 OS crash 测试可作为相关机制证据，但不替代本包所有工具、DevOps 与 family 恢复场景。provider failure 的分类和计账归 37，已移去本包重复且不相关的断言。

019 原合同中的独立 registry、精确 source symbol/proof title 与当前规范所有权的关系待决（GAP-150）；测试不会自行创建另一份权威清单。020 沿用 DevOps 模型和 handle 身份的既有待议（GAP-129、GAP-132）。
