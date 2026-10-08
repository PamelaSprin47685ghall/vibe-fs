# N04 C-R0 产物二：actor与实际强制能力

这是源码与官方资料调查，不是新的物理探针结果。平台能力必须在实际host上取得证据；没有能力时只允许明确拒绝对应immutable入口，不允许chmod/watch/hash回退。

## 当前Darwin

| actor/操作 | helper实际强制或观察 | 当前结论 |
| --- | --- | --- |
| 经UDRO role路径打开文件并改写/新增/删除 | readonly device + readonly filesystem；此前真实EROFS | 有限已有证据；暂停consumer的新矩阵待本轮正式验收 |
| 同UID consumer或外部进程访问`inputs.dmg` | backing只做普通文件和dev/ino校验；helper未安装文件写权限能力隔离 | raw open/write/truncate实际结果未测，未证；不能由UDRO名称推断backing不可写 |
| backing/祖先目录持有者rename或替换文件 | immediate owner身份及后续路径检查可发现持续变化 | 持续变化拒绝分支待黑盒；瞬时ABA未证，boundary检查不构成阻止 |
| 同UID actor unmount/detach/remount | helper只在成功尾部与清理读取hdiutil info；无独占mount权限合同 | 具体authorization/设备忙行为待真实host；源码不能称已允许攻击或已拦截 |
| 准备期持有可写原FD | helper未撤销该FD；镜像是复制出的不同对象 | 可否实际绕过消费须独立读取oracle；只改隐藏原目录不是snapshot corruption证明 |
| consumer读取挂载点祖先替换后的新路径 | 每次consume前及成功尾部revalidate；无连续路径锁定 | 持久替换可拒绝的源码分支；瞬时rename→restore与消费交错未证 |
| 外部更高权限actor | 当前helper没有独立OS权限边界 | 当前不提供抵御证书；必须明确受信OS/actor合同，不自动升级权限 |

本机Apple随系统提供的`/usr/share/man/man1/hdiutil.1`区分container blocks与filesystem content。`-readonly`强制结果device只读；`detach -force`可忽略open files；`-notremovable`仅root可用且正常清理需重启。后者不适合现有可回收scope，不能顺手加入当保护方案。上述是命令合同，具体同UID授权和实际成功结果仍待自有资源探针。

[Apple Disk Utility官方UDRO说明](https://support.apple.com/guide/disk-utility/create-a-disk-image-dskutl11888/mac)仅描述镜像格式的使用语义，不提供“同UID任意raw backing写不可达”的证书。

[Apple App Sandbox inheritance合同](https://developer.apple.com/library/archive/documentation/Miscellaneous/Reference/EntitlementKeyReference/Chapters/EnablingAppSandbox.html)要求实际child target具备相应entitlements。当前helper没有设置此合同；不能推断spawn任意Node/dotnet已取得新sandbox，也不能认为约束consumer便约束了另一个同UID外部进程。系统` sandbox-exec`手册明确标DEPRECATED，不能将其作为未经真实加载/回收验证的即插即用答案。

## 当前Linux

现公开入口在任何mount/consumer之前明确拒绝；没有现役Linux保护实现或本轮host能力探针。

| 候选OS能力 | 官方合同支持的范围 | 不能推出的结论 |
| --- | --- | --- |
| read-only bind/mount namespace | 隔离进程看到的mount表；per-mount readonly | 同一底层inode经其它writable mount/旧FD不能改；外部actor的内容写入被禁止 |
| private propagation +去除mount能力 | 可作为阻断consumer改变mount的调查候选 | 同UID外部进程不能rename祖先、换backing；实际host允许创建namespace/能力下降尚未测 |
| Landlock | 限制调用thread及其后代的ambient文件操作；ABI可查询 | 约束未进入domain的外部actor；撤销此前已打开文件FD；缺ABI时允许退化仍履行WHAT016 |
| fs-verity | 文件内容readonly，并在读取时验证内容 | 文件名、目录拓扑、rename/delete、mount身份冻结；当前host/FS具备该能力 |

推论依据：[Linux mount(2)](https://man7.org/linux/man-pages/man2/mount.2.html)明确per-mount readonly不影响其他mount；[mount namespace](https://man7.org/linux/man-pages/man7/mount_namespaces.7.html)只隔离mount视图；[Landlock官方kernel文档](https://docs.kernel.org/userspace-api/landlock.html)明确旧FD不受后来限制且domain仅覆盖其线程/后代；[fs-verity官方kernel文档](https://docs.kernel.org/filesystems/fsverity.html)明确不提供immutable-bit的rename/delete等额外语义。

这些是设计候选，不是本轮决定引入的实现。下一Linux包必须先调查actual ABI/FS/namespace权限、继承FD和外部actor边界；不足则在消费前明确拒绝，不套用文档示例的best-effort降级。
