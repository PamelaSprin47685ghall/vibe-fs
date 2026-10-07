namespace Wanxiangshu.Repository.Knowledge.Casebook

[<Sealed>]
type CasebookSettlementOwner =
    new: onFatal: (CasebookSemanticCutIncident -> unit) -> CasebookSettlementOwner
    member Observe: failure: CasebookAppendFailure -> unit
    member Redeliver: incident: CasebookSemanticCutIncident -> unit
