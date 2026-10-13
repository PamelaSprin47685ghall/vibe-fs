namespace Wanxiangshu.Mission.Work.Review

open Wanxiangshu.Mission.Work

module Model =
    val schemaJson: string
    val tryParse: obj -> Result<ReviewFindings, string>
