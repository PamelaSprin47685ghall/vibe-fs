# view / delivery 增量整合证据

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
