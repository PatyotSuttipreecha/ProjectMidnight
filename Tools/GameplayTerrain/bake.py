"""Bake Gameplay's ProBuilder ground. Requires numpy, scipy and UnityPy.
Run from the project root. Keeps the original mesh available as a reference.
"""
from pathlib import Path
import re
import struct
import numpy as np
from scipy.ndimage import distance_transform_edt
import UnityPy

SCENE = Path('Assets/Scenes/Scenes_Gameplay.unity')
OUTPUT = Path('Assets/Scenes/Terrain/Scenes_Terrain_Gameplay.asset')
TEMPLATE = Path('Assets/NatureManufacture Assets/Dynamic Nature - Mountain Tree Pack/Demo/test terrain.asset')
GUID = 'c7fe1e88e62349ec9629b8186c849733'
RESOLUTION = 1025
SOURCE_GO = 1757394959
SOURCE_TRANSFORM = 1757394960
s = SCENE.read_text()
docs = {int(i): (int(k), b) for k, i, b in re.findall(
    r'^--- !u!(\d+) &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', s, re.M | re.S)}
body = next(b for k, b in docs.values() if k == 114 and
            'Unity.ProBuilder::UnityEngine.ProBuilder.ProBuilderMesh' in b and
            f'm_GameObject: {{fileID: {SOURCE_GO}}}' in b)
vertices = np.array([[float(v) for v in p] for p in re.findall(
    r'^  - \{x: ([\d.e+-]+), y: ([\d.e+-]+), z: ([\d.e+-]+)\}', body, re.M)])
# This scene has unit-scale, identity-rotation parents. Reject changed transforms
# rather than silently baking heights in the wrong world coordinates.
transform = SOURCE_TRANSFORM
while transform:
    b = docs[transform][1]
    values = lambda key: [float(v) for v in re.search(
        key + r': \{([^}]+)\}', b)[1].replace('x:', '').replace('y:', '').replace('z:', '').replace('w:', '').split(',')]
    assert np.allclose(values('m_LocalRotation'), [0, 0, 0, 1]), transform
    assert np.allclose(values('m_LocalScale'), [1, 1, 1]), transform
    vertices += values('m_LocalPosition')
    transform = int(re.search(r'm_Father: \{fileID: (\d+)', b)[1])
indices = []
for hexes in re.findall(r'm_Indexes: ([0-9a-f]+)', body):
    indices.extend(struct.unpack('<' + 'I' * (len(hexes) // 8), bytes.fromhex(hexes)))
triangles = vertices[np.array(indices).reshape(-1, 3)]
low, high = vertices.min(0), vertices.max(0)
x, z = np.meshgrid(np.linspace(low[0], high[0], RESOLUTION),
                   np.linspace(low[2], high[2], RESOLUTION))
y = np.full(x.shape, np.nan)
for a, b, c in triangles:
    denominator = (b[2]-c[2])*(a[0]-c[0]) + (c[0]-b[0])*(a[2]-c[2])
    if abs(denominator) < 1e-9:
        continue
    u = ((b[2]-c[2])*(x-c[0]) + (c[0]-b[0])*(z-c[2])) / denominator
    v = ((c[2]-a[2])*(x-c[0]) + (a[0]-c[0])*(z-c[2])) / denominator
    inside = (u >= -1e-7) & (v >= -1e-7) & (u+v <= 1+1e-7)
    height = u*a[1] + v*b[1] + (1-u-v)*c[1]
    y[inside] = np.fmax(y[inside], height[inside])
coverage = np.isfinite(y)
assert coverage.any()
# Nearest covered samples keep the height texture well-behaved around holes.
nearest = distance_transform_edt(~coverage, return_distances=False, return_indices=True)
y[~coverage] = y[tuple(nearest[:, ~coverage])]
base = float(np.floor(low[1]) - 1)
vertical_size = float(np.ceil(high[1] - base) + 1)
normalized = np.clip((y-base)/vertical_size, 0, 1)
encoded = np.rint(normalized*32767).astype(np.int16)
mask = coverage[:-1, :-1] & coverage[1:, :-1] & coverage[:-1, 1:] & coverage[1:, 1:]
# Unity's public SetHoles API applies this footprint once during Editor import;
# it handles native hole storage and LOD layout without guessing its encoding.
Path('Assets/Scenes/Terrain/Scenes_Terrain_GameplayFootprint.bytes').write_bytes(
    struct.pack('<I', RESOLUTION-1) + np.packbits(mask.flatten(), bitorder='little').tobytes())
env = UnityPy.load(str(TEMPLATE))
obj = next(o for o in env.objects if o.type.name == 'TerrainData')
data = obj.read_typetree()
data['m_Name'] = OUTPUT.stem
detail = data['m_DetailDatabase']
for key in ('m_TreeInstances', 'm_TreePrototypes', 'm_DetailPrototypes', 'm_PreloadTextureAtlasData'):
    detail[key] = []
for patch in detail['m_Patches']:
    patch['layerIndices'] = []
    patch['numberOfObjects'] = []
    patch['bounds'] = {'m_Center': dict.fromkeys(('x','y','z'), float('nan')),
                       'm_Extent': dict.fromkeys(('x','y','z'), float('nan'))}
heightmap = data['m_Heightmap']
heightmap['m_Resolution'] = RESOLUTION
heightmap['m_Levels'] = int(np.log2((RESOLUTION-1)//16))
heightmap['m_Scale'] = {'x': float((high[0]-low[0])/(RESOLUTION-1)),
                       'y': vertical_size, 'z': float((high[2]-low[2])/(RESOLUTION-1))}
heightmap['m_Heights'] = encoded.flatten().tolist()
heightmap['m_Holes'] = []
heightmap['m_HolesLOD'] = []
heightmap['m_EnableHolesTextureCompression'] = False
bounds, errors = [], []
# Native terrain patch records: finest patches first, then their parents.
for level in range(heightmap['m_Levels']+1):
    step = 16 * (2**level)
    for row in range(0, RESOLUTION-1, step):
        for col in range(0, RESOLUTION-1, step):
            patch = encoded[row:row+step+1, col:col+step+1].astype(float)/32767
            bounds.extend([float(patch.min()), float(patch.max())])
            errors.append(0.0 if level == 0 else float(patch.max()-patch.min()))
heightmap['m_MinMaxPatchHeights'] = bounds
heightmap['m_PrecomputedError'] = errors
obj.save_typetree(data)
OUTPUT.write_bytes(obj.assets_file.save())
meta = Path(str(OUTPUT) + '.meta')
if meta.exists():
    meta.write_text(re.sub(r'  userData:.*', '  userData: GameplayTerrainFootprint:pending', meta.read_text()))
# Update the placed Terrain origin on later rebakes after moving the reference.
scene_now = SCENE.read_text()
match = re.search(r'(--- !u!4 &2100001001\n.*?m_LocalPosition: )\{[^}]+\}', scene_now, re.S)
if match:
    position = f'{{x: {low[0]:.8f}, y: {base:.8f}, z: {low[2]:.8f}}}'
    SCENE.write_text(scene_now[:match.start()] + match[1] + position + scene_now[match.end():])
# Read the native asset back and check every height, plus the world position.
check = next(o for o in UnityPy.load(str(OUTPUT)).objects if o.type.name == 'TerrainData').read_typetree()
assert check['m_Heightmap']['m_Heights'] == encoded.flatten().tolist()
roundtrip = np.array(check['m_Heightmap']['m_Heights']).reshape(y.shape)/32767*vertical_size+base
max_error = float(np.max(abs(roundtrip[coverage]-y[coverage])))
assert max_error < 0.001
Path('Tools/GameplayTerrain/bake-report.txt').write_text(
    f'Source: Scenes_Gameplay/TerrainMock/Terrain (ProBuilder)\n'
    f'World origin: {low[0]:.8f}, {base:.8f}, {low[2]:.8f}\n'
    f'Terrain size: {high[0]-low[0]:.8f}, {vertical_size:.8f}, {high[2]-low[2]:.8f}\n'
    f'Heightmap: {RESOLUTION} x {RESOLUTION}\n'
    f'Source triangles: {len(triangles)}\n'
    f'Height range: {y[coverage].min():.8f} to {y[coverage].max():.8f}\n'
    f'Max sample quantization error: {max_error:.8f} m\n'
    f'Covered cells: {int(mask.sum())} / {mask.size}\n'
    f'Native asset version: {obj.assets_file.unity_version}\n'
    'Footprint is finalized through TerrainData.SetHoles on first Unity Editor import.\n'
    'Unity Editor compilation and visual verification have not been run.\n')
print(Path('Tools/GameplayTerrain/bake-report.txt').read_text())
