module UnifiedStore.StudentQaRevivalFixture

/// P4U2 GATE-NO-MIGRATOR RED fixture: StudentQaStore / QA.md must stay absent under src/.
/// G3 clean-break + Amendment G3.5-A — retired / do-not-migrate (see also student-teacher-absence.mjs).
module StudentQaStore =
    let privatePath = "QA.md"

    let openStore root =
        System.IO.Path.Combine(root, "QA.md")
