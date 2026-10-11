"""Reproduce exact old toolbar arithmetic and old measurement-width contract.
This is source/geometry evidence, not WPF execution or font-metric evidence.
"""
import hashlib
import json
import subprocess

base = '5a55d0474cbe1b500f047ff5b05a1c8dceda1918'
source = subprocess.check_output(['git', 'show', base + ':src/KuaiYi.App/OverlayPresentation.cs'])
assert b'overlayFrames.Open(MeasureBody(),Preferences())' in source
assert b'var plan=overlayFrames.Plan??' in source
assert b'Content!.ToString()!.Length*15+10' in source
available = 220 - 48
visible = []
for label in ['回看', '收起', '停止', '复制', '设置']:
    width = len(label)*15 + 10
    if available >= width:
        visible.append(label)
        available -= width
assert '设置' not in visible
measurements = []
for name, count in [('short', 30), ('long', 300)]:
    def fixture_height(width):
        per_line = int((width-32)/15)
        return 80 + (count+per_line-1)//per_line*24
    old_height, correct_height = fixture_height(220), fixture_height(420)
    assert old_height > correct_height
    measurements.append({'text': name, 'characters': count, 'oldMeasuredWidth': 220,
                         'openedWidth': 420, 'oldHeight': old_height,
                         'correctHeight': correct_height, 'result': 'Fail'})
print(json.dumps({'baseCommit': base, 'sourceSha256': hashlib.sha256(source).hexdigest(),
                  'kind': 'Old source arithmetic counterexamples, not WPF execution',
                  'toolbar': {'widthDip': 220, 'visible': visible, 'settingsReachableFromToolbar': False,
                              'required': True, 'result': 'Fail'},
                  'bodyMeasurement': measurements}, ensure_ascii=False, indent=2))
