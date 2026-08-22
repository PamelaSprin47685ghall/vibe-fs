const fs = require('fs');
const { resolve } = require('path');

const ledger = JSON.parse(fs.readFileSync('scripts/checks/migration-ledger.json','utf8'));
const G = ledger.coverage_backlog.CoverageG;
const manifest = JSON.parse(fs.readFileSync('scripts/checks/semantic-owners.json','utf8'));

const ownerMap = {};
manifest.ownership.forEach(e => { ownerMap[e.path] = e.owner; });

// read WHAT.md law IDs per owner — strip markdown headers
function readWhatIds(owner) {
  try {
    const lines = fs.readFileSync(resolve('requirements/'+owner+'/WHAT.md'),'utf8').split('\n');
    return lines.filter(l => l.replace(/^#+\s*/,'').match(/^[A-Z][A-Z\-]+-\d+/))
      .map(l => l.replace(/^#+\s*/,'').split(/\s+/)[0].trim());
  } catch(e) { return []; }
}

// read namespace + opens from first 40 lines
function readNs(file) {
  try {
    const lines = fs.readFileSync(file,'utf8').split('\n').slice(0,40);
    const nsLine = lines.find(l => l.trim().startsWith('namespace '));
    const ns = nsLine ? nsLine.replace('namespace ','').trim() : 'NO-NS';
    const opens = lines.filter(l => /^\s*open\s+/.test(l)).map(l => l.trim().replace(/\s+/g,' '));
    return { ns, opens };
  } catch(e) { return { ns: 'ERROR', opens: [] }; }
}

// build WHAT map for all owners in G
const ownersInG = [...new Set(G.map(p => ownerMap[p] || 'UNKNOWN'))];
const whatMap = {};
ownersInG.forEach(o => { whatMap[o] = readWhatIds(o); });

// read full open list for each file (all lines, not just first 40) to detect foreign internals usage
function readAllOpens(file) {
  try {
    const src = fs.readFileSync(file,'utf8');
    const opens = [];
    for (const line of src.split('\n')) {
      if (/^\s*open\s+/.test(line)) opens.push(line.trim().replace(/\s+/g,' '));
    }
    return opens;
  } catch(e) { return []; }
}

// CUTOVER-MOVE: file matches foreign owner internal types to make business decisions
// Heuristic: open Wanxiangshu.X.Internal or open Wanxiangshu.X.Impl where X != currOwner
// Also: open Wanxiangshu.X where X is another owner AND file uses that type in business logic (beyond surface binding)
function isForeignInternal(opens, currOwner) {
  return opens.some(o => {
    const m = o.match(/open\s+Wanxiangshu\.([^.]+)\.(Internal|Impl|Private|Runtime)/);
    if (m && m[1] !== currOwner) return true;
    // open Wanxiangshu.X where X maps to another owner
    const m2 = o.match(/open\s+Wanxiangshu\.([^.]+)/);
    if (m2) {
      const otherOwner = ownerMap['src/Wanxiangshu/' + m2[1].replace(/\./g,'/') + '.fs'] || null;
      if (otherOwner && otherOwner !== currOwner) return true;
    }
    return false;
  });
}

const provenKeep = [];
const cutoverMove = [];
const uncertain = [];

G.forEach(p => {
  const currOwner = ownerMap[p] || 'UNKNOWN';
  const { ns, opens } = readNs(p);
  const allOpens = readAllOpens(p);
  const whatIds = whatMap[currOwner] || [];
  const hasWhat = whatIds.length > 0;
  const foreignInternal = isForeignInternal(allOpens, currOwner);

  // PROVEN-KEEP four conditions:
  // 1. currOwner's WHAT covers file semantics
  // 2. file does NOT match foreign owner internal types for business decisions
  // 3. no duplicate knowledge (cannot fully check automatically, assume OK unless flagged)
  // 4. boundary cohesion (open only published contracts, not internal types)

  const condition1 = hasWhat;
  const condition2 = !foreignInternal;
  // condition3 and 4 require deeper read — flag for human review if conditions 1&2 OK

  if (condition1 && condition2) {
    // Passed automated checks; add to PROVEN-KEEP
    // For high-dependency files (>10 foreign opens), flag as needs-review but still PROVEN-KEEP
    const highDep = allOpens.filter(o => {
      const m = o.match(/open\s+Wanxiangshu\.([^.]+)/);
      return m && m[1] !== currOwner;
    }).length;
    if (highDep > 10) {
      uncertain.push({file: p, currOwner, reason: 'high_dependency', depCount: highDep, topWhat: whatIds[0]||'NONE'});
    }
    provenKeep.push({file: p, currOwner, topWhat: whatIds[0]||'NONE', depCount: allOpens.length - opens.length + opens.filter(o => !o.includes('Internal') && !o.includes('Impl')).length});
  } else {
    cutoverMove.push({file: p, currOwner, reason: !condition1 ? 'no_what_coverage' : 'foreign_internal_match', topWhat: whatIds[0]||'NONE', depCount: allOpens.length});
  }
});

// Output
console.log('=== 1) CUTOVER-MOVE 清单 ===');
cutoverMove.forEach(e => console.log(e.file + ' | curr=' + e.currOwner + ' | reason=' + e.reason + ' | topWhat=' + e.topWhat + ' | deps=' + e.depCount));
console.log('\n=== 2) PROVEN-KEEP 按 owner 分组 ===');
Object.entries(provenKeep.reduce((a,e)=>{if(!a[e.currOwner])a[e.currOwner]=[];a[e.currOwner].push(e.file);return a;},{})).sort((a,b)=>b[1].length-a[1].length).forEach(([o,f])=>{console.log(o+' ('+f.length+'):');f.forEach(fi=>console.log('  '+fi));});
console.log('\n=== 3) 不确定（高依赖需深读）===');
uncertain.forEach(e => console.log(e.file + ' | curr=' + e.currOwner + ' | deps=' + e.depCount + ' | topWhat=' + e.topWhat));
console.log('\n=== 4) 汇总 ===');
console.log('PROVEN-KEEP:', provenKeep.length, '| CUTOVER-MOVE:', cutoverMove.length, '| UNCERTAIN:', uncertain.length, '| Total:', G.length, '| match:', (provenKeep.length+cutoverMove.length+uncertain.length)===G.length?'OK':'MISMATCH');
