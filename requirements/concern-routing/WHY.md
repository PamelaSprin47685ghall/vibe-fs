# concern-routing — WHY

跨参与者交互需要一条明确的路：谁委派谁、结果汇到哪、交付给谁、问题问给谁。编程面的 `fork` / `join` / `deliver` / `ask` 已经回答了这四个问题。若再设一套按关注点寻址的邮箱（订阅、发布、自动订阅、Pair Hint 捎带投递），同一件事就有两条通信路径和两份代次状态：消息可能走邮筒也可能走工具，责任边界随之模糊，恢复时还要调和两套记录。

因此交互路由只有一条边界：跨参与者与用户沟通全部走编程面与显式生命周期工具，durable 事实只记工作与交付，不记邮箱。

## DEPENDS ON

- `participant-identity`
- `delegation`
- `repository-programming`
