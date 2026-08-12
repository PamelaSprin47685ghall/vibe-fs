直接使用生成的 API。不要重新实现 Host 的 filesystem、permission、anchor、snapshot 或 transaction 逻辑。

Anchor 负责定位。JavaScript 负责变换。Mutation 由 Host 暂存并作为一次 transaction 提交。
