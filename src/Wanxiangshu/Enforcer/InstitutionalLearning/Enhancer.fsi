namespace Wanxiangshu.Enforcer.InstitutionalLearning

open Wanxiangshu.Enforcer

[<RequireQualifiedAccess>]
module InstitutionalEnhancer =
    val rulebookRevision: rules: EnforcerRule list -> string
    val evaluate: experience: string -> rules: EnforcerRule list -> LearningDisposition
