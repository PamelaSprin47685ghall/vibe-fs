namespace Wanxiangshu.Repository.Knowledge.Casebook

/// Durable Casebook event vocabulary. The Casebook store, its integration rule
/// and the persistence vocabulary join these names instead of restating them.
module CasebookEventTypes =
    let Captured = "EngineerCaseCaptured"
    let Refreshed = "EngineerCaseRefreshed"
    let Accessed = "EngineerCaseAccessed"
    let Evicted = "EngineerCaseEvicted"

    let LegacyCaptured = "InspectorCaseCaptured"
    let LegacyRefreshed = "InspectorCaseRefreshed"
    let LegacyAccessed = "InspectorCaseAccessed"
    let LegacyEvicted = "InspectorCaseEvicted"

    let all =
        [ Captured
          Refreshed
          Accessed
          Evicted
          LegacyCaptured
          LegacyRefreshed
          LegacyAccessed
          LegacyEvicted ]

    let isCasebookEvent eventType = all |> List.contains eventType
