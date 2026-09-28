"""Run with Python 3 and .NET 9 SDK. No third-party packages or network required."""
import json
import re
import struct
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONFIG = ROOT / 'UnityProject/Assets/AssetRaw/Configs/LevelConfigs'
CODE = ROOT / 'UnityProject/Assets/GameScripts/HotFix/GameLogic/Game'


def read(path):
    return path.read_text(encoding='utf-8-sig')


manifest = read(CONFIG / 'TextGraphicDataScriptableObject.asset')
assert '  TextGraphicDataList: []' in manifest, 'Manifest must not embed all geometry'
chunks = re.findall(r'  - resourceName: (\S+)\n    characters: (.+)', manifest)
graphics = {}
for name, characters in chunks:
    chunk_path = CONFIG / 'Graphics' / name / (name + '.asset')
    assert chunk_path.with_suffix('.asset.meta').exists()
    chunk = read(chunk_path)
    actual = []
    for block in re.split(r'(?=^  - character:)', chunk, flags=re.M)[1:]:
        character = json.loads(re.search(r'character: (.+)', block)[1])
        assert character not in graphics, f'Duplicate graphic: {character}'
        graphics[character] = block.count('    - points:')
        actual.append(character)
    assert ''.join(actual) == json.loads(characters), f'Index disagrees with {name}'

levels = []
level_text = read(CONFIG / 'TextLevelDataScriptableObject.asset').split('  characterToTone:')[0]
for block in re.split(r'(?=^  - levelName:)', level_text, flags=re.M)[1:]:
    character = json.loads(re.search(r'baseCharacter: (.+)', block)[1])
    assert character in graphics, f'Missing graphic: {character}'
    answers = []
    for answer in re.split(r'(?=^    - answerCharacter:)', block, flags=re.M)[1:]:
        sets = []
        for raw in re.findall(r'strokeIndices: ([0-9a-f]+)', answer):
            binary = bytes.fromhex(raw)
            indices = list(struct.unpack('<' + 'i' * (len(binary) // 4), binary))
            assert all(0 <= i < graphics[character] for i in indices), (character, indices)
            sets.append(indices)
        answers.append(sets)
    levels.append(answers)
print(f'PASS: {len(chunks)} separate-directory chunks, {len(graphics)} unique graphics; all {len(levels)} levels covered.', flush=True)

# Compile the exact production lookup implementation, not a rewritten version.
source = read(CODE / 'CorePlay/CorePlayGamePlay.cs')
lookup = source.split('// BEGIN STROKE_ANSWER_LOOKUP')[1].split('// END STROKE_ANSWER_LOOKUP')[0]
with tempfile.TemporaryDirectory(prefix='te-perf-checks-') as directory:
    tmp = Path(directory)
    (tmp / 'Check.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>')
    (tmp / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
    (tmp / 'Lookup.cs').write_text('using System.Collections.Generic; using GameLogic.Data; namespace GameLogic.GamePlay.CorePlay {' + lookup + '}', encoding='utf8')
    (tmp / 'Program.cs').write_text(read(Path(__file__).with_name('LookupTests.cs')), encoding='utf8')
    (tmp / 'levels.json').write_text(json.dumps(levels), encoding='utf8')
    subprocess.run(['dotnet', 'run', '--project', str(tmp / 'Check.csproj'), '-c', 'Release',
                    '-p:RestoreConfigFile=' + str(tmp / 'NuGet.Config'), '--', str(tmp / 'levels.json')], cwd=tmp, check=True)
