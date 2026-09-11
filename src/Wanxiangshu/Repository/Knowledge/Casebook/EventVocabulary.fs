namespace Wanxiangshu.Repository.Knowledge.Casebook

/// Durable Casebook event vocabulary. The Casebook store, its integration rule
/// and the persistence vocabulary join these names instead of restating them.
module CasebookEventTypes =
    let Captured = "InspectorCaseCaptured"
    let Refreshed = "InspectorCaseRefreshed"
    let Accessed = "InspectorCaseAccessed"
    let Evicted = "InspectorCaseEvicted"

    let all = [ Captured; Refreshed; Accessed; Evicted ]

    let isCasebookEvent eventType = all |> List.contains eventType
