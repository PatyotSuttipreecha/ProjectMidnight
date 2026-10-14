from pathlib import Path
import re, uuid

ROOT = Path(__file__).resolve().parents[2]
def guid(path): return re.search(r'guid: (\w+)', Path(str(path)+'.meta').read_text()).group(1)
def ensure_dir(path):
    path.mkdir(parents=True, exist_ok=True)
    meta=Path(str(path)+'.meta')
    if not meta.exists(): meta.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8')
def asset(path, script, fields):
    if path.exists(): raise RuntimeError(f'Will not overwrite {path}')
    text=f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: {path.stem}
  m_EditorClassIdentifier:
'''
    text+=''.join(f'  {key}: {value}\n' for key,value in fields.items())
    path.write_text(text, encoding='utf-8')
    key=uuid.uuid4().hex
    Path(str(path)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {key}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8')
    return key

for part in ['Assets/Data/Enemies', 'Assets/Data/Enemies/TheLost', 'Assets/Data/Enemies/TheLost/Weapons']: ensure_dir(ROOT/part)
weapon_script=guid(ROOT/'Assets/Script/EnemySO/EnemyWeaponSO.cs')
enemy_script=guid(ROOT/'Assets/Script/EnemySO/EnemyDataSO.cs')
modules={}
for name,kind,low,high,prep,windup,recovery,reach,arc in [
    ('Machete',0,15,22,.45,.5,.85,2.8,110),
    ('Log',1,12,18,.45,.5,.85,2.8,110),
    ('HeavyAxe',2,25,35,1,.65,1.3,3,80),
    ('Stone',3,8,12,.55,.4,.8,10,90),
    ('Torch',4,10,15,.45,.5,.85,2.8,110),
]:
    fields=dict(loadout=kind, heldModel='{fileID: 0}',localPosition='{x: 0, y: 0, z: 0}',localEulerAngles='{x: 0, y: 0, z: 0}',localScale='{x: 1, y: 1, z: 1}',
        attackAnimation='TheLost_Throw' if kind==3 else 'TheLost_HeavyAxe' if kind==2 else 'TheLost_Sweep', range=reach,hitArc=arc,minDamage=low,maxDamage=high,preparation=prep,windup=windup,recovery=recovery,
        armDropChance=.35,otherDropChance=.05,disarmRollInterval=.2,droppedLifetime=12,projectileModel='{fileID: 0}',projectileSpeed=14,projectileGravity=9.81,projectileRadius=.12,projectileLifetime=5,
        projectileObstacles='\n    serializedVersion: 2\n    m_Bits: 4294967295', burnDuration=3,burnDamagePerSecond=2,burnEffect='{fileID: 0}')
    modules[name]=asset(ROOT/f'Assets/Data/Enemies/TheLost/Weapons/Data_Enemies_TheLost_Weapon_{name}.asset',weapon_script,fields)
for name in ['Unarmed',*modules]:
    reference='{fileID: 0}' if name=='Unarmed' else f'{{fileID: 11400000, guid: {modules[name]}, type: 2}}'
    fields=dict(useSharedSettings=1,useLostCombat=1,startingWeapon=reference,detectAnticipation=.4,baseHitArc=90,baseRecovery=.8,baseAttackAnimation='zombie_attack2_1',restraintDuration=.4,restraintSpeedMultiplier=.65,playerHitProtection=.75,
        startingHealth=100,walkSpeed=.5,chaseSpeed=2.5,closeApproachSpeed=.5,chaseSlowdownDistance=4,speedChangeRate=5,chaseRepathInterval=.25,
        detectionRadius=12,fieldOfView=100,obstacleMask='\n    serializedVersion: 2\n    m_Bits: 136',canHear=1,hearingMultiplier=1,walkHearingRadius=3,runHearingRadius=8,gunshotHearingRadius=25,
        noiseSearchDuration=3,noiseTravelTimeout=15,lostSightCooldown=5,waitTimeAtPoint=2,useApproachDecisions=1,approachDecisionInterval=2.5,approachWeights='{x: 1, y: 0, z: 0.4}',
        flankAngle=30,pressureSpeedMultiplier=.8,burstSpeedMultiplier=1.2,coordinateAttacks=1,maxConcurrentAttackers=1,engagementDistance=6,pressureRingRadius=1.8,pressureOrbitSpeed=.6,pressureOrbitStep=25,pressureWaitTimeout=15,pressureRetreatCooldown=8,attackTurnTimeout=4,attackTurnRest=1,
        acceleration=8,turnSpeed=120,stoppingDistance=.8,spreadChaseTargets=0,chaseApproachRadius=1.5,varyAvoidancePriority=1,separateNearbyEnemies=1,separationRadius=1,separationSpeed=.75,
        minAttack=8,maxAttack=12,attackRange=2,attackPreparationTime=.35,attackWindup=.5,attackCooldown=2,eyeHeight=1,playerTargetHeight=1,
        useRagdoll=0,enableHitReaction=1,hitStaggerDuration=.3,hitKnockbackDistance=.15,hitKnockbackDuration=.12,corpseLifetime=8,lootDropChance=0)
    asset(ROOT/f'Assets/Data/Enemies/TheLost/Data_Enemies_TheLost_{name}.asset',enemy_script,fields)
print('Created 5 weapon modules and 6 prototype enemy profiles; visual models unassigned, world vision mask requires setup.')
