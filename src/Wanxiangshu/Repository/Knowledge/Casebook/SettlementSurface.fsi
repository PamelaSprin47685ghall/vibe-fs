namespace Wanxiangshu.Repository.Knowledge.Casebook

module CasebookSettlementSurface =
    val createOwner: onConflict: (obj -> unit) -> obj
    val describeIncident: incident: obj -> obj
    val isIncident: value: obj -> bool
    val observeIncident: owner: obj -> incident: obj -> obj
    val observeFailure: owner: obj -> failure: obj -> obj
