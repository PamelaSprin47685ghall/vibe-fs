# CUT-02-R6-E：取消监听与单次执行准入

输入主仓`3c8f385c7`，库`df14f48 / 0.6.8`不变。预算固定包b1f7804的非作者Node/Bun对照已证实旧P1，`ebfbe3d83`已有同一根因，不能归为预算新增回退。R12-U1继续暂缓。

## 目标与边界

ExecutorTool的try/finally现包住真实await，而不是只包Task创建。Host abort从等待开始到收束只解绑一次。Runner在入口及取得许可后检查取消；仅在LargeGate.acquire和runSpawned两个明确等待边界，按同token已取消的事实将拒绝转为ProcessCancelled，未取消的异常保留原错误。底层waitForExit拒绝及kill职责不变。排队取消不释放holder的许可；交接取消释放自己的许可且零目标启动。取消结果只确认请求，不证明目标已退出，也不声称区分同刻所有I/O根因。

不增加取消适配器或第二套状态，不变更库、物理监管协议、持久记录、信号终结协议或R12-U1。真实目标/后代收尾另用物理观察证明，不能用取消文案替代。

## 反例与归因

固定包旧反例：`/private/tmp/process-wire-budget-review-exact.cxnJg3/late-abort-{node,bun}.log`，真实AbortController add后立即remove，取消时无listener，释放LargeGate后实际stdout为started-after-abort。

本包正式新增五项在生产修改前2pass/3fail，`/private/tmp/process-cancellation-red.log`。排队工具listener=0；直接Runner排队取消抛异常；同步release后cancel仍进入host。完成与exit7解绑两个正控通过。原测试draining只要求Runner拒绝，现在按RunProcess有类型结果断言ProcessCancelled，物理后代退出断言保留；底层等待reject/kill-once测试保持不变。

本轮首个实现误以为Fable的OperationCanceledException构造和类型判断一致，642完整组256项250pass/4fail/1skip/1TODO，`process-cancellation-node.log`。生成构造实际是Util.Exception，catch却检查AsyncBuilder.OperationCanceledException；没有命中。非作者保存失败Node/Bun及642生成JS于`/private/tmp/process-cancellation-review-642.uS5oIw/`。最终改为上述局部owner转换，删除spawnHost被前一分支完全覆盖的旧判型；不是去改公共Foundation或泛捕整个执行流程。

另有两项施工/夹具纠正：编译期间调整tracked测试触发mid-build mutation，第一次编译不作通过，停止编辑后重建；新双语测试须显式刷新现有语言缓存，ready用pending→rename原子发布完整PID。首轮Bun失败后其错误使其他文件报嵌套test；根的原始stderr被后一次运行覆盖，不能冒称保全了该完整总计，仅保留外层exit1、工具摘录及非作者独立失败日志。最终Bun完整原始日志另见下文。静态门禁的控制金字塔通过提取acquireLargeGate局部等待函数修正，未放宽门禁。

## 验收与交付

643完整Process256项254pass/0fail/1integration skip/1TODO，`/private/tmp/process-cancellation-node-final.log`。最终645含新增非取消故障负控：正式十文件114项113pass/0fail/1integration skip/0TODO，10/10文件完成、owned accepted=true，`process-cancellation-formal.log`；Bun六个完整文件79/79，预先准备独立HOME，`process-cancellation-bun-delivery.log`和`process-cancellation-bun.{stdout,stderr}.log`。这是Bun原生物理证明，不称正式监督。实际OpenCode Host网络/持久/冷读1/1，format/static/diff通过。

注册DevOps工具以SDK真实context.abort在目标ready后取消，两种语言均返回256字节内“请求/未确认”文案，不造exit_code、不开模型会话；目标消失另查，已有同组后代draining仍查确切收尾。受控未取消waiter故障保持原样reject/killCount1，证明不会把所有错误改成取消。

固定主包`/private/tmp/wanxiangshu-cancel-4d594fd.tgz`，SHA-256 `4d594fd547b791b2dd961ace0c8763140b08b06b0242a039e522460f1eaaa162`，2076 members/展开13402497 bytes，库仍0.6.8。默认独立消费通过。非作者同包Node/Bun各8项观察通过，覆盖Runner和工具排队/同栈交接零启动、监听、许可归属、实际继任正控、非取消错误及双语真实ready取消/目标ESRCH。2076产品文件及archive前后相同、库ESM落包内，临时运行目录清理；证据`/private/tmp/process-cancellation-review-exact.4zZLQr/`。八个声明依赖的借用明示，不称独立npm安装。

根的独立npm及声明Host peer扩展隔离验收通过：Node/Bun各五文件61/61，加006物理取消/非取消故障/底层kill-once四项各4/4；Bun128MiB五次均1/1，完整PTY025为124/124。`/private/tmp/process-cancellation-isolated.log`及`/private/tmp/process-cancellation-main-package-evidence-jKfxRg/`保留全部内层输出/状态；安装2076产品文件与archive前后相同、只使用包内库、临时运行目录清理。只复制smol测试oracle和requirements/scripts夹具，生产代码取固定包。随本原子提交正常推送master，未部署Host或发布registry。R6-D原生终结协议、旧PTY首轮失败、正式监督及R12-U1均不随本包关闭。
