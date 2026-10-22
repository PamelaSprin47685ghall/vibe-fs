namespace Wanxiangshu.Execution.Failure

/// Product-side classification of a host-neutral failure fact.
/// The host port reports raw facts; the product owns retry / budget / terminal decisions.
module HostFailureClassifier =

    val classify: failure: Wanxiangshu.Host.Ports.HostFailure -> ExecutionFailure
