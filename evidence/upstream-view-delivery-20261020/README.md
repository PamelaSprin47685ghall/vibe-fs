# view / delivery 增量整合证据

合并后复核见 [umr-review.json](umr-review.json)。本轮记录34处静态门禁修复后的实际验证，以及新上游21提交、56处冲突造成的PR待整合状态。旧收据与日志保持历史含义。

固定上游 11052493bedb4edbcbe0c341fa677afd44c486f1；开发起点 83875650a。运行工作树未切换，库保持 05f6b5a。日志来自本批独立工作树；各组选集重叠，不能相加。

## UM-1

接收来源：9c87a15c5 的每个终态结果 Pair Hint、45bf5b74a 的保证书参数退役、11052493b 的双语语言提示精简。保留本地原始参数恢复、逐结果原件核对、grounding 末项落点和旧职责。

实施回归候选为 Fable generation 43，171 语义表面、826 ESM 模块实际加载。compilerInputDigest 为 10e1365d1a83f7c1dbbb71498dea199554807f0e19f9d65c8d8e6ffca6c2f4a0，generatedInputDigest 为 2aaf3ad44fea58cbb4037fa29512a82707b1af23d0100a898fab253960f359a1，artifactInputDigest 为 a5efd6747a358f382d5979defc54c7a972570a81777adc948167560b062cf58a。仓库把已跟踪 Markdown 也纳入构建输入；状态文档与本索引冻结并暂存后再构建、复验。最终提交候选以 [um1-delivery.json](um1-delivery.json) 的输入摘要和结果为准，避免更新文档再次漂移候选。

| 证据 | 结果及边界 |
|---|---|
| um1-behavior-red.log | 旧产物 0pass/3fail，覆盖保证书 schema、重复定义和多终态后缀 |
| um1-language-red.log | 1pass/1fail，新语言锚点断言先红 |
| um1-native-01.log | 首轮 172pass/2fail/5skip/2TODO；两红是仍要求保证书的旧断言，已精准改为无该字段 |
| um1-build-01.log 至 um1-build-06.log | 保留全部构建；02 正确拒绝构建期测试语料变化；最终 06 为 gen43，通过 |
| um1-format.log、um1-format-02.log、um1-format-check.log | 只格式化本包修改的 F# 文件；格式检查通过 |
| um1-check.log、um1-check-02.log | 初轮发现两处分支内循环；提取共享的终态映射后静态检查通过，没有改门禁 |
| um1-formal-01.log、um1-formal-02.log | 01 为 174/0/5skip/2TODO；02 新增原件夹具未按当前任期初始化而失败，不能算通过 |
| um1-multi-native-01.log 至 um1-multi-native-04.log | 保留夹具的原型比较、接口包装和任期装配失败；最终按现存 Engineer 当前任期夹具验证，1/0，含真实 Host 重放、完整原件和独立进程冷读 |
| um1-formal-03.log | 最终 gen43，七文件 175pass/0fail/5skip/2TODO，7/7 排空、进程组回收 accepted=true；旧 TODO 仍使入口退出 1 |
| um1-host-01.log | 正式 Host canary 在同步物理调用期间触发 5000ms 静默；无权威汇总，残余组实际回收，accepted=false，不记通过 |
| um1-host-native-01.log | 原生集成 108/1/1TODO，观察器自身仍要求旧 schema；保留失败并改正断言 |
| um1-host-02.log、um1-host-03.log | 使用既有 begin/end ACK 协议声明最多 30000ms 的真实 Host 操作；默认静默仍为 5000ms，原 canary 子进程超时未改。最终 gen43 两文件 109pass/0fail/1TODO，2/2 排空、回收 accepted=true |

真实 Host 证明包括不注入保证书、历史实际传入的 contract 原样恢复、正常/失败/取消及 executor throw 的实际边界、provider 序列化和 grounding 原件；scope 内的 mock 测试另证明同源键顺序和并发参数隔离。未把历史 executor throw TODO 或 GAP-106 转绿。

UM-1 限定源码接收已具备证据。完整发行、独立安装包、UM-2—UM-4 组合及运行切换留给后续包；distribution-007 的旧发行问题仍由 N1 承接。

UM-2 的中间失败与修正过程保存在 um2-*.log：首轮类型不匹配、静态分支门禁、旧 view checkpoint 入口、旧前缀展示断言，以及 Advisor 完整尝试夹具的 authority 冲突均保留。um2-provider011-baseline.log 是旧工作树同名用例的原始 6 pass / 2 fail，作为本轮同类失败的基线证据，不当作接收证明。最终冻结候选以 um2-delivery.json 为准；相关测试、integration checkpoint、剩余旧失败分别列明。

用户扩大范围后的完整合并以 umr-delivery.json 为最终收据。主仓固定11052493b，库合并fc75b85876f61073ec4f256e77729aaf6f63caf3已在自有远端可获取。旧Blogger/Chronicle/WorkRecord/Replica/自动grounding生产路径退役，历史本目录证据只证明当时范围。

umr-* 日志分别记录构建、静态门禁、原生定位、正式监督、完整发行与包消费。中间原生失败用于定位，不替代监督结论；正式汇总、文件排空和资源回收分别核对，TODO与skip不计通过。最终收据记录实际候选digest、提交与推送，未部署。

延期边界：reportMapping统一委派未在固定上游完整接通，保留实际Host入口并撤下未绑定SDK成员。早期缺失wget的两个下载用例跳过，最终使用已有临时GNU wget执行；SemanticTraceSurface便捷数字cursor投影超过Int32会截断，真实journal的64位cut重开另验。本轮不修改该边缘投影，也不关闭原有发行、前缀或通用隔离GAP。

控制流嵌套的静态风格债另记；不放宽门禁、不补空白基线，不用功能测试通过冒充完整发行通过。上游测试的JS边界问题按owner面修复，所有权威结论以收据中同候选的原始日志为准。

最终定向验证复用前批 GNU wget 1.25.0 临时构建，系统未安装该工具的限制由测试PATH解决，发行包不携带它。Advisor正例经真实事件订阅、持久ProviderStarted和TerminalReporter完成，空答复反例仍使用受控精确终态；同步交付另验正式散文不混入reasoning或工具结果。

gen57 clean build、53文件核心398/0、13文件实际资源278/0、4文件checkpoint集成11/0与真实包消费均已取得原始结果。核心3个集成层skip和6个旧TODO仍使该入口退出1，其余两组正式监督退出0，三组均排空且回收accepted=true。静态34处控制流风格债阻断完整发行入口。实际Host旧夹具的锚点断言修正后，最终代次及复验结论见收据，不把中间45/1记作通过。

gen58正式组合420/1的失败为真实Host夹具过早发送第二条prompt，只等数据库stop未等当前轮idle；Manager实际Host已通过。原生idle修正复验确认OpenCode1.18.29的4次provider请求、用户引文、持久工具原件及独立进程冷读取。只改测试准入时序，保留原件断言与静默门；失败和最终复验分别留档，最终收据不累加重叠用例。

gen59组合420/1进一步确认：旧mock只输出完成散文，未真实deliver(view)，因此按新合同被继续推进。最终夹具真实交付两轮并断言tool成功，再观察第三轮原文保真；原生已确认6次provider请求和独立冷读。没有增加生产豁免；最终正式组合结果见收据。
