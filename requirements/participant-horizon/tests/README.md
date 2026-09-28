# participant-horizon 测试范围

- 002 使用原有源码泄漏检查器的正反 fixture 和当前仓库扫描。它只能证明选定路径及词形，没有证明所有实际 Provider 输入；源码中的业务字段也不能按名字一律当机器 DTO。
- 003/005 通过真实 JoinResultRenderer 验证返回、失败、取消、等待和终端观测的表示；解析 TOML 区分外层控制字段与原始输出中碰巧出现的 `status` 等业务文本。结果顺序、工作记录内容和退出码均有直接断言。
- 009 走真实 Manager fork 工具，隐藏/未知 calling 得到相同拒绝，不创建 child；合法 Engineer 正例防止“一律拒绝”假通过。
- 010 走真实 horizon 观察固定 devops，并读取实际插件 Schema 检查 fork/commission 候选。
- 011 走真实创建、接收、取消和名册读取。原 HorizonSurface 自己实现了一份渲染，不能证明真实日志读取和最新记录选择，相关断言撤下并留 TODO；不再用执行耗时小于 50ms 证明 pull-only。
- 012 使用生产热启动准入和注入的搜索 capability，Engineer/DevOps 有输出，其他角色在调用搜索前拒绝。它证明准入，不代表真实检索质量。
- 013 将形似指令/工具历史的搜索内容送入实际热启动路径，解析确认它仍是数据，真正的任务和本地化警示仍为指令。多行内容目前多出一个 LF，保留带实际失败断言的 TODO，见 GAP-081；不能宣称逐字节保真。
- 015 只证明权限投影。旧“调用七次同一拒绝文本”没有测试七种角色，两个 chooseRoad 对象也没有创建两个名册成员，因此真实多 Engineer 名册仍为 TODO。

GAP-080 保存了取消后 Join 不结束的分阶段复现；取消前后名册正确，不等于最终后果可领取。GAP-079 汇总尚无充分证据的过滤、跨界面隐藏、最新记录和虚假动作边界。工具 Surface 名称本身不是生产证明。

正式构建后，选择本包、requirement-system、受影响的 repository-investigation 热启动用例与 provider-language 006；启用 integration 执行插件 Schema 检查。TODO 或跳过不能计入通过，不宣称整包验收。
