# SM-05 施工证据

本批范围READY，以[计划](../../proposals/extraction-kanban/UPSTREAM-SEMANTIC-MERGE-2026-10-19.md)为准。主仓合并事务尚未提交，未部署。

- `history-native-01`为generation10基线：291项244pass/37fail/10TODO。包含退役squash调用、旧child LWR断言和三个Trace夹具差异。
- `view-cold-red-01`在新Node进程读到真实持久checkpoint调用身份，却读不到摘要，证伪耐久交棒声明；`plan-user-red-01`证明暖态ANCHOR会取代Plan用户原文。SC-02/03按现存owner收窄合同：U/P/持久账本承担交棒，ANCHOR只处理当前进程仍可见的消息，Plan原文不接受取代。没有引入新的持久摘要层。
- `build-01`通过generation12，1510编译项、171语义表面、825 ESM模块。Fork与Sync只读js.deliver，不从终态散文或历史LWR补值；仅子LWR完成回调退出，真实Attachment及Fission历史消费者仍保留。
- `history-native-02`为287项258pass/19fail/10TODO；剩余19红来自因果夹具的固定2026-10-17观察时间已退出当前UTC保留窗口。`causal-diagnostic-01/02`显示真实文件存在而冷读保留集为空，不修改生产GC规则。夹具改为当前观察时间后，与恢复因果测试合跑的`fixtures-native-03`为106pass/0fail/1TODO。
- 三个Trace差异分别是：缺ProviderRun时不同Host消息不能仅凭partId合并；工具夹具已迁todowrite而断言仍写read；分支认证必须用不同真实body证伪，不能只换外部ref/digest标签。现存原件身份冲突门没有放宽。
- `delivery-native-01`为29pass/26fail/1TODO，26红均为恢复夹具尚未显式填交付格；正控填入js.deliver值后，原AuthorityRoot/physical身份、retry/guard/repair、晚回执拒绝断言保持。新的Sync反例拒绝未交付散文，只返回结构化显式交付值。
- 同组managed-session-lifecycle-028三例通过：历史读取不参与完成；真实HandleWorkCompleted物理append的EIO命中一次、Source返回失败而durable work保留Active；受控PERSIST Source另证join唤醒。
- `plan-user-green-02`是NUL不能直接作为OS argv传递的夹具错误，不放宽原件断言；经JSON传递后`plan-user-green-03`为1/0，暖态与独立冷进程均保留BOM、CRLF和控制字节，摘要不成为用户原件。
- `prefix-native-01`为155pass/25fail/8TODO，含旧.git冷夹具、真实Host旧read工具和mutation等待被自己阻止写入的FailureRecorded。确切mutation子测试PID经核对后TERM回收，父runner正常报告红灯。修复迁移后复验进行中。
- `static-check-02`实际报告51项：49处控制嵌套与2处语言标识差异；387条shard-edge低置信度只是report-only。均未记作通过，结构和格式门禁继续归SM-07收束。
- `static-check-04`退出0：49处控制嵌套按职责提取为局部函数，双语协议标识统一，未豁免规则；387条既有shard-edge提示仍只是report-only。`build-02`因当前FSharp.Core没有Result.iterError失败，改用受支持的Result.defaultWith后`build-03`通过generation13；`build-04`通过generation14。
- generation13的`history-native-03`为276pass/0fail/1skip/10TODO，`prefix-native-03`为180pass/0fail/1skip/8TODO。跳过的独立编译消费者和实际OpenCode前缀Host通过`integration-native-01`启用集成层复验：34pass/0fail/0skip/0TODO，含真实正负编译和原件冷恢复。各组选集重叠，不相加。
- `delivery-formal-01`因未清理的测试计时器触发5秒窗口；`delivery-formal-02`在修改测试后被新鲜度门拒绝。清理Promise.race计时器并按规则刷新后，generation14的`delivery-formal-03`为62pass/0fail/2skip/2TODO，7/7排空、进程组回收accepted=true；正式入口因TODO与skip退出1，不能记作完整通过。`repair-native-04`独跑被定位场景1/0，未改因果断言。
- `delivery-formal-04`开启实际Host用例时仍触发5秒静默，并报告owned termination observation失败。只核对并TERM了本次留下的PID12822（cwd为wxs-user-input-SNolLl/workspace），复查该PID不存在；另两个旧工作区PTY没有触碰。该失败保留。真实Host夹具改为js.deliver后，`guidance-native-01`完整11/0/0skip/0TODO：工具步与普通final后接续均保留不中断、单一工作及冷读Retired断言。
- 两个真实Host测试原有同步spawn改走仓库既有owned probe与有限物理等待begin/end ACK：每例最多30秒，25秒取消，5秒默认监督窗口未放宽。`build-05`通过generation15；`delivery-formal-05`含实际Host为64pass/0fail/0skip/2TODO，7/7排空、回收accepted=true，正式入口仅因旧TODO退出1。`physical-formal-01`复验本轮控制流拆分后的进程与完整PTY生命周期：206/0/0skip/0TODO，8/8排空、回收accepted=true。

CommissionerRecord公共字段/渲染分支曾被自动审批两次拒绝，理由是可能损失父到子上下文并要求单独授权。用户随后明确回复“批准删除 CommissionerRecord（推荐）”；按此授权删除公共字段、渲染分支及双语资源，实际Attachment独立保留。五处生产调用原本均传None；双语TOML隔离和敌意输入断言移到Attachment复验。

剩余：同候选正式历史/前缀/交付回归、完整原件与现实消费者范围核对；新旧日志与TODO不相加、不冒充整包闭合。Blogger全退役与旧GAP清零不在范围。
