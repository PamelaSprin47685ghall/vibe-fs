# N04 C-R0 产物三：正式探针与有限施工入口

所有物理扰动只使用独立分配、私有身份可复核的目录/image/device；不操作workspace、用户目录或foreign资源。没有执行本文件探针。

## 本轮C-R1有限包（已写源码，未原生验收）

唯一文件`requirements/verification-system/tests/support/readonly-input-tests.mjs`。复用既有四个allocateVerificationDirectory身份owner、小member、withReadonlyVerificationInputs和runVerificationToolProbe，不引入SDK/Fable假receipt。

1. 在自有output构造五个普通可写control目录。实际write/add/delete/internal rename/new symlink操作均能成功，结果单独核对，排除无效参数/EEXIST/EXDEV假拒绝。
2. 一个真实Node经原consumer登记和monitor启动。它先注册output目录watch，核release是否已存在，读取四role member；ready JSON先写staging再rename发布，随后等待release事件，不使用fixed sleep。
3. 父actor先注册watch再核ready文件，ready与consumer提前结束及test.signal取消竞速。取得actual PID与四role首读后，对每role实际运行上述五操作，记录真实errno，共20次。
4. finally关闭父watch/abort listener并发布release。actual Node再次读四role，写合法output JSON和stdout后退出；原scope观察实际consumer结算，再detach。
5. 先完成四root/member dev/ino/mode/size/bytes与完整清单恢复、PID ESRCH、root仅inputs/output和正常设备/镜像回收，再断言20个errno精确为EROFS、首次/末次读取均为预存字节、合法output实际存在。

原行为若满足，记“新覆盖green”，不称旧缺陷red。正确扰动对照可手工把尝试指向独立writable topology：实际writes成功且所有readonly恢复先成立，最终EROFS oracle必须红。不能只把expected errno改掉制造无效变异。

新symlink目标member真实存在且创建目的名不存在；本包不包含替换已有link。真实source/tools/project不支持预置symlink，SDK archive内部link替换留作独立前提。C-R1继续partial。

主agent原生选择命令：`WXS_TIER_INTEGRATION=1 node --test --test-name-pattern='a paused actual Node consumer keeps four readonly namespaces' requirements/verification-system/tests/016.test.mjs`。Darwin；无SDK archive/模型env依赖。一次create/convert/attach/detach；预计10–15秒仅根据旧scope成本推算，未实测。正式默认unit仅skip此native，不能计通过。

## 后续C-R0能力探针

| 探针 | 最小安全形态与独立oracle | 结果应如何解释 |
| --- | --- | --- |
| raw backing权限 | owned小镜像+exact image/dev；Node actor尝试O_RDWR，并可对明确自有字节位置做原字节同值write；记录errno/实际write返回，关闭FD再由owner清理 | 仅证明是否获得raw写能力，不证明consumer已读过变化；若要后者另设计未缓存member实际读与恢复 |
| 原准备期FD | mount前对owned member打开FD；mount后通过FD读取可辨认原对象并报告fstat，consumer经正式path读镜像；明确两套字节/身份 | 证明隔离或真实绕过；不以可写FD存在本身判snapshot失效 |
| ancestor/rename | actor自己的outer directory owner包住所有fixture；consumer通过实际绝对路径重新open；交换/恢复时有ready/read/release，实际readHex是oracle | 持久替换与瞬时ABA分开；若OS拒绝记录errno，若成功不得靠最终库存相同称安全 |
| unmount/detach能力 | 仅actual owned device，无force起步；真实Node暂停且无打开input handle，再以同UID actor调用hdiutil；记录真正OS结果及info的exact image/device | 成功意味着需后续运行期保护/拒绝证明；失败只属于该权限/生命周期，不能泛称所有actor都被拦截 |
| 准备混代 | actual writer在捕获读操作的明确暂停点变更自有prepared member，恢复后用mounted完整库存和actual consumer bytes核对 | 固定selection+mount库存拒绝是有效正控；只有实际consumer读到错误而scope成功才是业务red |

## C-R2的最小独立包

优先“owned mount持续消失导致不发布成功，并不删除foreign替代”，不是unknown attach模拟。原scope确认owned image/device后，独立actor分配自己的image owner，真实unmount或detach原device，再在自有fixture位置提供可辨认foreign namespace；actualconsumer读取/完成后scope须拒绝。测试必须分别保留生产scope结果、actual child排空、foreign原dev/ino/字节与actor最终自有cleanup结果。原helper仅能处理其证明拥有的资源；不得强制它按原路径detach foreign。

其前提是先取得actual同UIDdetach/替代能力和exact device的真实OS观察。若OS明确不允许，交付该能力结果；不能替换hdiutil二进制、修改production run、或用假plist伪装blackbox。unknown attach是另一包，需要真实daemon结果与取消切点的控制能力，当前不承诺可独立完成。
