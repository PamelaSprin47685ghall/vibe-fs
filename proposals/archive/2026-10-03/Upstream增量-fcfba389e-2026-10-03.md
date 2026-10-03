# Upstream增量与PR冲突：fcfba389e（2026-10-03）

合并前HEAD为`f00ffcc1f`，[b776同步](Upstream增量-b7768f478-2026-10-03.md)已经完成并推送。交付核对发现upstream又增加`3f9aa1636`和`fcfba389e`（100文件），PR51为CONFLICTING。继续普通merge，23个文本冲突按现行WHAT解决，不把上游“CI恢复、业务不变”或历史全量数字作为等价与验收证据。

## 合并边界

- 保留exact physical acceptance / AdmittedWork。拒绝无journal、未准入或retired work先发布Join任务再迟到补权，拒绝将WorkNotAdmitted吞为成功。
- 保留固定DevOps原回调、独立取消episode和detach清理；拒绝永久teardownTask与取消前全局关闭observer。墓碑、加载恢复、scoped work及Guard交接保持已验证实现。
- Sphinx保留typed Revision、native MCP、canonical @2与旧版本semantic cut；制度学习保留candidate机械BIRTH和诚实的两次Append PARTIAL，不改成已具备生产CAS/事务。重复重排helper不替代清晰owner。
- 保留实际同Decision共享完成、exact physical/user边界和真实Predictor配置；不允许global测试开关、任意最后user回退或无Requested启动扩大生产授权。Host最终请求仍经既定冻结与prefix/probe判定。
- 吸收Strength显式projection session和Boot.Timer；只有ProjectionNotVisibleYet可有限重读，Rejected立即拒绝。仅跳过真实空assistant placeholder找实际完成源，不跳过有正文的assistant或外来user。正式spec013阳性在原已编译产物上取得缺bootstrap红例，配置与跨user两条新增回归用于保既有合同；三者由本次统一产物复验，未把后两条旧产物诊断冒称incoming编译红。
- 清理零字节`.fsproj.tmp`，吸收libicu镜像依赖、已有静默预算环境透传及不缩小证明的夹具接线。生产journal、Manager与dispatch已有边界保留；独立jobguard没有本批正式候选发布证明，不盲加。
- 现行verification-system-010/021不允许TODO override变成完整验收，也不允许三次canary/请求重跑选择绿、无新物理证据扩版本围栏，或以WXS_SKIP_STRENGTH_CANARY裁剪唯一Long Stroke。恢复单次canary和完整冻结场景，原699/3351事件预算不变，恢复被删除的behavior009 TODO。
- cold-boundary的“Chronicle records”关键词误删普通用户历史，正式harness反例可红；改按完整双语资源的实际render前缀及合成ack识别有限例外。英文正式正例继续覆盖，任意普通正文仍保持固定。
- 复核再发现混合text/media行与非法ack也能被整体删除，补正式红例后只承认恰好一个text part和恰好一个`.` ack。拒绝新增journal flat payload fallback：缺stream/payload的伪装行也可被提取成Bound，不能当合法Strength事实；保原观察路径，完整typedstream合法性仍归WHAT015与Long Stroke欠证。

## 正式证据与口径

gen80首次扩展39包617文件，617/617排空，3268 pass、4 fail、56 skip、309 TODO。四红均为concern002/004的正式夹具：incoming将journal-only runtime当SDK runtime，另只建立user而未提供exact assistant供Host准入绑定。不能放宽ProviderStarted门禁或删Pair Hint断言，改用真实夹具接口和合法物理身份；原始失败见[首次617文件](baselines/2026-10-03-sync-fcfba389e-unit-gen80-red.log)。这批红不是由TODO造成，也不靠同输入重跑取绿。

修正后局部concern整包15 pass、0 fail、6 TODO；dispose后的旧Started租约不被恢复，重启使用新physical ingress承载冻结历史，严格保留历史hint前缀、当轮新occurrence、公告不重复与owner消息隔离，未改生产权限。正式全批结果仍以统一重新构建后的编号日志为准。混合帧及ack两红的290项harness由288 pass/2 fail修至290 pass/0 fail，见[帧形状红例](baselines/2026-10-03-sync-fcfba389e-cold-shape-red.log)和[修正后](baselines/2026-10-03-sync-fcfba389e-cold-shape-green.log)。

本批重新冻结全部输入并用仓库Fable构建；运行受影响编号套件、自动发现的完整integration及harness。原始输入范围和实际汇总分别见[编号范围](baselines/2026-10-03-sync-fcfba389e-unit-scope.txt)、[构建](baselines/2026-10-03-sync-fcfba389e-build.log)、[编号测试](baselines/2026-10-03-sync-fcfba389e-unit.log)、[integration](baselines/2026-10-03-sync-fcfba389e-integration.log)、[静态检查](baselines/2026-10-03-sync-fcfba389e-check.log)、[格式](baselines/2026-10-03-sync-fcfba389e-format.log)。这些原始结果绑定本批输入，不能拿b776的gen78/79数字充当新证书。

统一gen81结果：170 Surface/829模块，1796 sources/885 artifacts新鲜；617/617文件排空，**3272 pass、0 fail、56 skip、309 TODO**，47.10秒。完整integration37/37排空，**387 pass、0 fail、18 TODO**；package3 pass/0 fail/1 TODO，harness**290 pass/0 fail**，入口74.8秒正常收束。Node26关键26文件为182 pass/0 fail/2 skip/17 TODO，实际corpus6 pass/0 fail/1 TODO。各正式supervisor进程组回收accepted=true；静态门禁、全仓F#格式和非日志diff通过。

此表回填改变文档corpus；交付前仅刷新最终文档输入并做独立定向复验，不把gen81全批数字冒称文档后全批重跑。见[文档后构建](baselines/2026-10-03-sync-fcfba389e-build-docs-final.log)、[Node26定向复验](baselines/2026-10-03-sync-fcfba389e-node26-docs-final.log)和[corpus重推导](baselines/2026-10-03-sync-fcfba389e-corpus-docs-final.log)。回填后没有业务源码/测试改动；原始log与范围txt不参与源码文档corpus，归档不改变已验收输入。

因果红绿见[TODO绕过红例](baselines/2026-10-03-sync-fcfba389e-todo-red.log)、[修正后](baselines/2026-10-03-sync-fcfba389e-todo-green.log)、[普通用户历史红例](baselines/2026-10-03-sync-fcfba389e-cold-red.log)、[完整harness修正后](baselines/2026-10-03-sync-fcfba389e-cold-green.log)。临时CLI输出不充当业务证明，正式断言保留在编号021和harness中。

Strength的[旧产物placeholder反例](baselines/2026-10-03-sync-fcfba389e-placeholder-old-artifact-red.log)与实际spec013正式断言对应；不是本批incoming全量编译的证书。最终实际corpus重推导另见[corpus复验](baselines/2026-10-03-sync-fcfba389e-corpus.log)。

默认并发与原判决静默/Host启动/隔离编译预算不增加。TODO/skip不计通过；局部PR可评审不等于完整release、OS crash/真实TERM/KILL验收。当前产品台账仍7 OPEN、130 PARTIAL，Sphinx219 PARTIAL/222 OPEN，Blogger077与制度学习181仍PARTIAL。

交付将正常推送master和现有PR51分支；以远端相同head、包含最新上游祖先及GitHub MERGEABLE为合并交付证据，不自行合并远端PR。后续产品施工仍从[总计划](../../TODO施工总计划-2026-10-03.md)认领。
