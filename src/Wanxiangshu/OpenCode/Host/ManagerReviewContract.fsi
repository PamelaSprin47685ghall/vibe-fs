namespace Wanxiangshu.OpenCode.Host

module ManagerReviewContract =

    /// Decorates the manager review tool definition by adding
    /// the contract property to parameters and appending contract to required.
    /// Non-review tools are returned unmodified.
    val decorateDefinition: toolInput: obj -> toolOutput: obj -> unit

    /// Hides the contract argument by saving its original descriptor and key position
    /// under a private module Symbol on the args object with enumerable:false, configurable:true,
    /// and deleting the contract property. Refuses before mutation when restoration is impossible.
    val hide: args: obj -> unit

    /// Restores the contract descriptor and key order on the same args object.
    /// Idempotent (no-op if not present). Refuses non-extensible or incompatible
    /// property descriptors before changing business fields or dropping the saved original.
    val restore: args: obj -> unit

    /// Wraps the registered js-manager executor. Contract is hidden only while
    /// that executor runs and restored on its actual completion or exception.
    val wrapReviewExecutors: tools: obj -> unit
