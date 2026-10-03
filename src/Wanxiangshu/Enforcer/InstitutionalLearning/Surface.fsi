namespace Wanxiangshu.Enforcer.InstitutionalLearning

[<RequireQualifiedAccess>]
module InstitutionalLearningSurface =
    val evaluate: experience: string -> ruleNames: string array -> candidate: obj -> obj
    val learn: experience: string -> candidate: obj -> ruleSnapshots: string array array -> obj
    val revision: ruleNames: string array -> string
    val empty: unit -> obj

    val commit:
        session: string ->
        occurrence: string ->
        kind: string ->
        experience: string ->
        revision: string ->
        disposition: string ->
        frozen: string ->
        resurfaced: string array ->
        state: 'state ->
            obj

    val born: session: string -> occurrence: string -> candidate: obj -> state: 'state -> obj
    val bornRules: state: 'state -> obj
    val frozen: session: string -> occurrence: string -> state: 'state -> obj
