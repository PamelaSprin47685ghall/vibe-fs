module UnifiedStore.GitBypassFixture

open Wanxiangshu.Process

/// Phase 1 RED fixture (§37): direct git process outside Infrastructure/Git|Persist.
/// All Wanxiang Git ops must converge on GitGateway ownership.
module FeatureGit =
    let command (dir: string) (args: string list) : Command =
        { FileName = "git"
          Arguments = args
          WorkingDirectory = Some dir
          Environment = None
          Stdin = None
          Deadline = None
          PtyOptions = None }
