# N04 C-R0 产物一：实际消费闭包

本轮只读调查。没有启动挂载、OS能力探针、Fable或原生测试；下列“已有证据”引用此前有限验收，不能当作本轮新证书。C-R1新增测试源码已交主agent统一验收。本文件不改变WHAT016或关闭T418/T419/GAP-055。

## 实际入口与边界

`scripts/lib/verification-readonly-inputs.mjs:81`是唯一当前readonly view入口。第83行在非Darwin平台消费前拒绝；未发现Linux挂载或sandbox实现。第71行消费登记仅做active检查、边界revalidate、保留实际Promise和signal，并不约束操作回调可调用的OS能力。

四个执行期路径保留原绝对地址，皆为同一mountPoint的不同直接子目录。原身份owner在挂载期不能用旧inode证明新卷；临时view核image dev/ino、mounted root/四role根身份、owner非函数字段和全部成员库存。结束先停止新登记、abort并allSettled实际consumer，再核唯一owned image/device、detach、验证原身份/库存并归还原owner。

| 消费来源 | 当前实际路径与owner | 已捕获/受保护内容 | 仍需独立证明 |
| --- | --- | --- | --- |
| source | `source.sourceRoot`；prepareGitSourceCandidate | explicit tree/blob、普通文件字节/mode及完整库存；挂载后同路径进入UDRO | 执行期实际加载图是否仅来自此source；原workspace脚本不因此受保护 |
| SDK | `sdk.toolRoot/dotnet-sdk`；selected archive/SDK owner | bundle、dotnet、host/fxr/shared/packs；实际版本/list-sdks/list-runtimes/info准入 | 系统native/dylib加载；所有runtimeconfig/probing路径；是否存在额外搜索 |
| tools | `tools.toolRoot`；restored-tool owner | 原manifest、selected Fable DLL、packages/resolver、完整普通成员库存；deps.json中managed runtime资产 | deps中的native/runtimeTargets和运行时动态加载闭包 |
| project | `project.projectRoot`；locked restore owner | 原fsproj/ProjectReference、assets/props/targets、选定packages及完整普通成员库存 | resolver所有绝对路径确实解析到view；完整多工程/全部profile仍非本切片 |
| Fable执行外壳 | selected dotnet调用selected Fable DLL；`verification-fable-project.mjs:96` | privateDotnetEnvironment；NUGET_PACKAGES指向只读project；ArtifactsDir/JS输出指向独立可写compileRoot | 外壳Node、monitor模块和OS命令尚不属于四owner |
| Node/monitor | `process.execPath`启动workspace的`verification-tool-monitor.mjs`及diagnostics import | 原monitor真实group drain/PID/pipe/terminal所有权 | Node executable及动态库、monitor脚本/imports尚未作为固定readonly执行来源 |
| 生成JS消费 | Node加载compileRoot中Identity/Quiescence产物 | compile receipt绑定完整输出、active view内revalidate；过期receipt拒绝 | JS是上一阶段输出、下一阶段输入；运行期物理保护未由四输入view覆盖 |
| 镜像操作OS来源 | `/usr/bin/hdiutil`、`/usr/bin/plutil`；helper的execFile | 绝对命令路径、create/convert/attach/info/detach生命周期 | executable/OS版本、DiskImages daemon、系统库不属于四owner固定快照 |
| group检查OS来源 | `/bin/ps`；monitor的execFileSync | 实际PGID/终止/退出与回收观察 | 系统ps/库版本和可信OS前提 |
| Git | preparation从环境Git执行，明确tree/blob且禁replacement/lazy fetch | materialized selected source与tree绑定 | Git launcher不在readonly消费scope；N08全流程须单列其选择与加载来源 |

## FD和路径的准确边界

当前monitor stdio显式为pipe/IPC，实际tool stdio为ignore/inherit/inherit，源码没有把准备期文件FD映射给tool。这个事实不是完整的运行时FD清单证书：下一调查须列coordinator→monitor→tool实际继承FD，并区分pipe/IPC、cwd/目录句柄和文件句柄。

原可写文件FD不会因路径被mount覆盖自动改指镜像副本。单独证明该FD能改隐藏的原准备目录，不等于证明consumer读到了被改变的snapshot。正式例必须报告实际读取字节和所读对象身份；原workspace并行编辑与自有prepared roots也不得混为同一目录。

当前source只接受Git普通blob；project/tools的captureOrdinaryVerificationFiles拒绝symlink。SDK archive materialize可接受可解析、完整库存内的relative内部link。C-R1小namespace fixture不能冒称所有真实prepared-role均接受link；已有SDK内部link替换须另选真实archive owner证明。

## 结论

四owner单工程Fable有限证据保留；全流程Node/monitor/Git/native/OS和生成产物下一阶段消费闭包未完成。N08不能仅把`verify.mjs`外面包一层当前view就声称完整固定输入。
