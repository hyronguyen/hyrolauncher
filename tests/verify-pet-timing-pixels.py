from pathlib import Path
import os
os.chdir(Path(__file__).resolve().parent.parent)
from pathlib import Path
from PIL import Image
import json
root=Path(__file__).resolve().parent.parent
report=json.loads((root/'output/verification/pet-timing/report.json').read_text(encoding='utf-8-sig'))
for entry in report:
    with Image.open(entry['source']) as original, Image.open(entry['timed']) as timed:
        assert original.n_frames==timed.n_frames==8
        assert original.info.get('loop')==timed.info.get('loop')
        for frame in range(8):
            original.seek(frame);timed.seek(frame)
            assert timed.info['duration']==300
            assert original.convert('RGBA').tobytes()==timed.convert('RGBA').tobytes(), 'Retiming changed pixels or transparency'
with Image.open(root/'output/verification/pet-timing/no-control.gif') as fixture:
    assert fixture.n_frames==1 and fixture.info['duration']==250
print('PASS: decoded pixels, transparency and loop preserved in all 48 frames; inserted GIF control decodes.')
