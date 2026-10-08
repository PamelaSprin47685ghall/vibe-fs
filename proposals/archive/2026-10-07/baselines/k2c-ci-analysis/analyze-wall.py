import collections
import json
import pathlib

root = pathlib.Path('/private/tmp/vibe-fs-ci898e3f7ae-37631505588')
out = pathlib.Path('/private/tmp/vibe-fs-ci898e3f7ae-analysis')
data = json.loads((root / 'finite-observations.json').read_text())
starts = {row['entryFile']: row for row in data['fileStarts']}
costs = {row['entryFile']: row for row in data['costs']}
drains = {}
for n, line in enumerate((root / 'verify-logs/latest/unit.log').read_text().splitlines(), 1):
    if not line.startswith('runner: file lifecycle '):
        continue
    row = json.loads(line[len('runner: file lifecycle '):])
    if row['type'] == 'runner:file-drained' and row['entryFile'] in starts:
        assert row['entryFile'] not in drains
        drains[row['entryFile']] = {**row, 'rawLine': n}

assert len(starts) == 813 and len(drains) == 811
horizon = max(row['elapsedMs'] for row in starts.values())
files = []
for name, start in starts.items():
    drain = drains.get(name)
    cost = costs[name]
    end = drain['elapsedMs'] if drain else horizon
    assert end >= start['elapsedMs']
    files.append({
        'file': name.split('/requirements/', 1)[1],
        'startMs': start['elapsedMs'],
        'endMs': end,
        'complete': drain is not None,
        'observedOuterWallMs': end - start['elapsedMs'],
        'validWorkerWallMs': (cost.get('interval') or {}).get('wallMs'),
        'costStatus': cost['status'],
        'startLine': start['rawLine'],
        'drainLine': drain['rawLine'] if drain else None,
    })

events = []
for row in files:
    if row['observedOuterWallMs'] == 0:
        continue
    events.append((row['startMs'], 1, row['file']))
    events.append((row['endMs'], -1, row['file']))
events.sort()
occupied = set()
previous = 0
occupancy = collections.defaultdict(float)
segments = []
peak = 0
for time, change, name in events:
    if time > previous:
        occupancy[len(occupied)] += time - previous
        segments.append({'fromMs': previous, 'toMs': time, 'occupiedSlots': len(occupied), 'files': sorted(occupied)})
    if change == 1:
        assert name not in occupied
        occupied.add(name)
    else:
        assert name in occupied
        occupied.remove(name)
    peak = max(peak, len(occupied))
    previous = time
assert not occupied and peak == 2

lane_ends = [0.0, 0.0]
lanes = [[], []]
ambiguous = []
for row in sorted(files, key=lambda row: row['startMs']):
    available = [i for i, end in enumerate(lane_ends) if end <= row['startMs']]
    assert available
    if len(available) > 1:
        ambiguous.append({'file': row['file'], 'startMs': row['startMs']})
    lane = min(available, key=lambda i: lane_ends[i])
    lanes[lane].append(row)
    lane_ends[lane] = row['endMs']

completed = [row for row in files if row['complete']]
valid_workers = [row for row in completed if row['validWorkerWallMs'] is not None]
completed_outer = sum(row['observedOuterWallMs'] for row in completed)
completed_worker = sum(row['validWorkerWallMs'] for row in valid_workers)
active_lower = sum(row['observedOuterWallMs'] for row in files if not row['complete'])
lane_idle = 2 * horizon - completed_outer - active_lower
assert abs(lane_idle - (2 * occupancy[0] + occupancy[1])) < 0.001
packages = collections.defaultdict(float)
for row in completed:
    packages[row['file'].split('/', 1)[0]] += row['observedOuterWallMs']
summary = {
    'clock': 'single outer supervisor performance.now()-startedAt; not worker monotonic clocks',
    'horizonMs': horizon,
    'horizonKind': 'last actual file-start, before the scheduled 300000ms backstop; no assumed exact callback timestamp',
    'completedFiles': len(completed),
    'activeFiles': len(files) - len(completed),
    'completedOuterWallSumMs': completed_outer,
    'validCompletedWorkerCount': len(valid_workers),
    'validCompletedWorkerWallSumMs': completed_worker,
    'completedOuterMinusValidWorkerWallMs': sum(row['observedOuterWallMs'] - row['validWorkerWallMs'] for row in valid_workers),
    'activeObservedOuterWallLowerSumMs': active_lower,
    'completedOuterTwoSlotLowerBoundMs': max(completed_outer / 2, max(row['observedOuterWallMs'] for row in completed)),
    'completedValidWorkerTwoSlotLowerBoundMs': max(completed_worker / 2, max(row['validWorkerWallMs'] for row in valid_workers)),
    'occupiedTimeBySlotCountMs': dict(occupancy),
    'peakOccupiedSlots': peak,
    'totalIdleSlotMs': lane_idle,
    'utilizationThroughHorizon': (completed_outer + active_lower) / (2 * horizon),
    'reconstructedLanes': [{'files': len(lane), 'occupiedMs': sum(row['observedOuterWallMs'] for row in lane), 'idleMs': horizon - sum(row['observedOuterWallMs'] for row in lane), 'lastFile': lane[-1]['file']} for lane in lanes],
    'laneIdentityLimit': 'valid interval coloring, not a native PID/worker identity; ambiguous available-slot starts retained',
    'ambiguousStarts': ambiguous,
    'topCompletedOuterFiles': sorted(completed, key=lambda row: row['observedOuterWallMs'], reverse=True)[:25],
    'topCompletedWorkerFiles': sorted(valid_workers, key=lambda row: row['validWorkerWallMs'], reverse=True)[:25],
    'topCompletedOuterPackages': sorted(packages.items(), key=lambda row: row[1], reverse=True)[:15],
    'largestCapacityHoles': sorted((segment for segment in segments if segment['occupiedSlots'] < 2), key=lambda row: (row['toMs'] - row['fromMs']) * (2 - row['occupiedSlots']), reverse=True)[:20],
}
(out / 'wall-analysis.json').write_text(json.dumps({'summary': summary, 'files': files, 'segments': segments, 'reconstructedLanes': lanes}, ensure_ascii=False, indent=2) + '\n')
print(json.dumps({key: value for key, value in summary.items() if key not in ['topCompletedOuterFiles', 'topCompletedWorkerFiles', 'largestCapacityHoles', 'ambiguousStarts']}, ensure_ascii=False, indent=2))
print('TOP OUTER FILES')
for row in summary['topCompletedOuterFiles'][:15]:
    print(json.dumps(row, ensure_ascii=False))
print('TOP HOLES')
for row in summary['largestCapacityHoles'][:8]:
    print(json.dumps(row, ensure_ascii=False))
