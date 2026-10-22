import subprocess, shutil
f='src/bayes/Exact.fs'; orig=open(f).read()
M=[('M1 duplicate observation id counted twice','List.distinctBy (fun factor -> factor.ObservationId) group, []','group, []'),
('M2 naive log-sum-exp underflows','peak + Math.Log(values |> List.sumBy (fun value -> Math.Exp(value - peak)))','Math.Log(values |> List.sumBy Math.Exp)'),
('M3 likelihood upper bound relaxed','value < 0.0 || value > 1.0','value < 0.0 || value > 2.0'),
('M4 unqualified factors admitted','List.filter (fun factor -> factor.Qualified)','List.filter (fun _ -> true)'),
('M5 duplicate fault names the first key','Some(key, _) -> Error(ExactFault.DuplicateHypothesisKey key)','Some _ -> Error(ExactFault.DuplicateHypothesisKey(List.head keys))'),
('M6 prior-only skips the hypothesis contract','''let priorOnly (hypotheses: Hypothesis list) : Result<Posterior, ExactFault> =
    checkHypotheses hypotheses
    |> Result.bind (fun () -> normalizedPrior hypotheses)''','''let priorOnly (hypotheses: Hypothesis list) : Result<Posterior, ExactFault> =
    normalizedPrior hypotheses'''),
('M7 dropped members not reported','''        group
        |> List.tail
        |> List.map (fun factor -> factor.ObservationId, "conservative-rule: joint factor not declared")''','        []'),
('M8 zero prior lifted','Math.Log mass))','Math.Log(max mass 1e-9)))')]
try:
  for name,a,b in M:
    assert orig.count(a)==1, name
    open(f,'w').write(orig.replace(a,b))
    bld=subprocess.run(['node','scripts/build.mjs','bayes'],capture_output=True,text=True)
    if bld.returncode: print(name,': BUILD FAIL'); continue
    t=subprocess.run('node --test src/bayes/tests/*.test.mjs',shell=True,capture_output=True,text=True)
    fails=[l for l in t.stdout.splitlines() if l.startswith('# fail')]
    print(name+':', fails[0] if fails else t.stdout[-200:])
finally:
  open(f,'w').write(orig)
  subprocess.run(['node','scripts/build.mjs','bayes'],capture_output=True)
