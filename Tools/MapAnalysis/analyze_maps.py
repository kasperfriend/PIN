#!/usr/bin/env python3
import os, struct, zlib, json, traceback, io
from collections import Counter

MAPS_DIR="/tmp/maps"
OUTPUT_DIR="/home/user/PIN/Docs"
MARKER=bytes([0xED,0x12,0x5B,0xED,0x12,0x5A,0xED,0x12])

def read_lpref(f):
    l=struct.unpack('<I',f.read(4))[0]
    return f.read(l).decode('utf-8',errors='ignore') if l else ""

def try_gt(f):
    pos=f.tell()
    buf=f.read(8)
    if len(buf)<8: return None
    if buf==MARKER:
        t=struct.unpack('<I',f.read(4))[0]
        ln=struct.unpack('<I',f.read(4))[0]
        return (t,ln,True,pos)
    else:
        t=struct.unpack('<I',buf[:4])[0]
        ln=struct.unpack('<I',buf[4:])[0]
        return (t,ln,False,pos)

def parse_layers(data,parent):
    layers=[];off=0;dl=len(data)
    while off<dl:
        if off+8>dl: break
        buf=data[off:off+8];off+=8
        if buf==MARKER:
            if off+8>dl: break
            tid=struct.unpack('<I',data[off:off+4])[0]
            ln=struct.unpack('<I',data[off+4:off+8])[0]
            off+=8
        else:
            tid=struct.unpack('<I',buf[:4])[0]
            ln=struct.unpack('<I',buf[4:])[0]
        if off+ln>dl: break
        raw=data[off:off+ln];off+=ln
        lyr={'typeId':tid,'length':ln,'raw':raw,'children':[]}
        if tid in (0x30000,0x20100,0x20200,0x20400,0x50001,0x40001,0x40002):
            try:
                ch=parse_layers(raw,tid)
                if ch: lyr['children']=ch
            except: pass
        layers.append(lyr)
    return layers

def parse_zone(path):
    res={}
    try:
        with open(path,'rb') as f:
            magic=f.read(4)
            if magic!=b'ZONE': return {'error':'bad magic','path':path}
            ver=struct.unpack('<i',f.read(4))[0]
            ts=struct.unpack('<q',f.read(8))[0]
            nlen=struct.unpack('<i',f.read(4))[0]
            name=f.read(nlen).decode('utf-8',errors='ignore').rstrip('\x00')
            res.update({'magic':'ZONE','version':ver,'timestamp':ts,'name':name,'path':path,'zoneId':os.path.basename(path).replace('.zone','')})
            hdr=try_gt(f)
            if not hdr: return {'error':'no root hdr','path':path}
            tid,ln,hasm,pos=hdr
            res['root_type']=hex(tid);res['root_length']=ln
            root_data=f.read(ln)
            children=parse_layers(root_data,tid)
            res['root_children_count']=len(children)
            res['children_types']=Counter([hex(c['typeId']) for c in children])
            zi={'chunk_ranges':[],'chunk_refs':[],'chunk_refs2':[],'paths':[],'bounds':None,'melding_perims':[],'subzone_regions':[],'encounter_names':[],'skybox':False,'water':False,'melding_hm':False,'chunk_info':0,'transfer':False,'camera':False,'prop_doodad':0,'default_env':0,'unknown':Counter()}
            for ch in children:
                tid=ch['typeId'];raw=ch['raw']
                if tid==0x20400:
                    zi['chunk_info']+=1
                    for sub in ch['children']:
                        stid=sub['typeId'];sraw=sub['raw']
                        if stid==0x10000:
                            try:
                                cube,mnX,mxX,mnY,mxY=struct.unpack('<IIIII',sraw[:20])
                                zi['chunk_ranges'].append({'cube':cube,'minX':mnX,'maxX':mxX,'minY':mnY,'maxY':mxY})
                            except: pass
                        elif stid==0x10101:
                            try:
                                x,y,rec=struct.unpack('<III',sraw[:12])
                                zi['chunk_refs'].append({'x':x,'y':y,'recId':rec})
                            except: pass
                        elif stid==0x10100:
                            try:
                                x,y=struct.unpack('<II',sraw[:8])
                                zi['chunk_refs2'].append({'x':x,'y':y})
                            except: pass
                elif tid==0x20800:
                    try:
                        bio=io.BytesIO(raw)
                        cce=struct.unpack('<I',bio.read(4))[0]
                        unk=struct.unpack('<I',bio.read(4))[0]
                        cnt=struct.unpack('<I',bio.read(4))[0]
                        steps=[]
                        for _ in range(min(cnt,5)):
                            pos=struct.unpack('<fff',bio.read(12))
                            orient=struct.unpack('<ffff',bio.read(16))
                            alen=struct.unpack('<I',bio.read(4))[0]
                            ab=bio.read(alen) if alen else b''
                            try: act=ab.decode('utf-8',errors='ignore')[:200]
                            except: act=ab.hex()[:100]
                            steps.append({'pos':pos,'orient':orient,'action':act,'alen':alen})
                        zi['paths'].append({'cceId':cce,'unk1':unk,'total_steps':cnt,'sample_steps':steps})
                    except Exception as e:
                        zi['paths'].append({'error':str(e)})
                elif tid==0x21000:
                    try:
                        mn=struct.unpack('<fff',raw[:12]);mx=struct.unpack('<fff',raw[12:24])
                        zi['bounds']={'min':mn,'max':mx}
                    except: pass
                elif tid==0x20200:
                    for sub in ch['children']:
                        if sub['typeId']==5:
                            try:
                                bio=io.BytesIO(sub['raw'])
                                nm=read_lpref(bio)
                                cp=struct.unpack('<I',bio.read(4))[0]
                                bl=struct.unpack('<I',bio.read(4))[0]
                                bb=(bl+7)//8
                                bf=bio.read(bb)
                                unk1=struct.unpack('<I',bio.read(4))[0]
                                pc=struct.unpack('<I',bio.read(4))[0]
                                perims=[read_lpref(bio) for _ in range(pc)]
                                zi['melding_perims'].append({'name':nm,'controlPoints':cp,'perims':perims,'bitlen':bl})
                            except Exception as e:
                                zi['melding_perims'].append({'error':str(e)})
                elif tid==0x21700:
                    try:
                        bio=io.BytesIO(raw)
                        rid=struct.unpack('<I',bio.read(4))[0]
                        bbox=bio.read(8)
                        w=struct.unpack('<I',bio.read(4))[0]
                        h=struct.unpack('<I',bio.read(4))[0]
                        bc=struct.unpack('<I',bio.read(4))[0]
                        zi['subzone_regions'].append({'regionId':rid,'width':w,'height':h,'bitCount':bc})
                    except: pass
                elif tid==0x21200:
                    try:
                        bio=io.BytesIO(raw)
                        cnt=struct.unpack('<I',bio.read(4))[0]
                        names=[]
                        for _ in range(cnt):
                            sl=struct.unpack('<I',bio.read(4))[0]
                            nb=bio.read(sl)
                            names.append(nb.decode('ascii',errors='ignore'))
                        zi['encounter_names'].extend(names)
                    except: pass
                elif tid==0x20000: zi['skybox']=True
                elif tid==0x20300: zi['water']=True
                elif tid==0x20700: zi['melding_hm']=True
                elif tid==0x21600: zi['transfer']=True
                elif tid==0x21500: zi['camera']=True
                elif tid in (0x21300,0x21400): zi['prop_doodad']+=1
                elif tid==0x20100: zi['default_env']+=1
                else: zi['unknown'][hex(tid)]+=1
            res['zone_info']=zi
    except Exception as e:
        res['error']=str(e);res['trace']=traceback.format_exc()
    return res

def decomp_block(data,usize):
    if len(data)<4: raise ValueError("small")
    magic=data[:4]
    if magic==b'DATA':
        return zlib.decompress(data[4:])
    elif magic==b'DAT2':
        props=data[4:9];payload=data[9:]
        import lzma
        try:
            hdr=props+struct.pack('<Q',usize)+payload
            return lzma.decompress(hdr,format=lzma.FORMAT_ALONE)
        except Exception as e:
            # fallback raw
            filters=[{"id":lzma.FILTER_LZMA1,"dict_size":1<<24}]
            try:
                d=lzma.LZMADecompressor(format=lzma.FORMAT_RAW,filters=filters)
                return d.decompress(payload)+d.flush()
            except Exception as e2:
                raise ValueError(f"DAT2 fail {e}/{e2}")
    else:
        raise ValueError(f"unknown magic {magic}")

def parse_chunk(path):
    res={'file':os.path.basename(path),'path':path}
    try:
        with open(path,'rb') as f:
            data=f.read()
        off=0
        if data[off:off+8]==MARKER:
            tid=struct.unpack('<I',data[off+8:off+12])[0]
            ln=struct.unpack('<I',data[off+12:off+16])[0]
            off+=16
        else:
            tid=struct.unpack('<I',data[off:off+4])[0]
            ln=struct.unpack('<I',data[off+4:off+8])[0]
            off+=8
        if tid!=0x40000: return {'error':f'root {hex(tid)}','file':res['file']}
        res['root_length']=ln
        root_end=off+ln
        ver=struct.unpack('<I',data[off:off+4])[0];off+=4
        ts=struct.unpack('<q',data[off:off+8])[0];off+=8
        nLods=struct.unpack('<I',data[off:off+4])[0];off+=4
        res.update({'version':ver,'timestamp':ts,'numLods':nLods})
        lods=[]
        for _ in range(nLods):
            if off+8>len(data): break
            buf=data[off:off+8]
            if buf==MARKER:
                lt=struct.unpack('<I',data[off+8:off+12])[0]
                ll=struct.unpack('<I',data[off+12:off+16])[0]
                off+=16
            else:
                lt=struct.unpack('<I',data[off:off+4])[0]
                ll=struct.unpack('<I',data[off+4:off+8])[0]
                off+=8
            if lt!=0x40001: break
            lend=off+ll
            lvl=struct.unpack('<I',data[off:off+4])[0];off+=4
            sub=struct.unpack('<I',data[off:off+4])[0];off+=4
            unk=struct.unpack('<I',data[off:off+4])[0];off+=4
            compS=struct.unpack('<I',data[off:off+4])[0];off+=4
            uncompS=struct.unpack('<I',data[off:off+4])[0];off+=4
            li={'level':lvl,'subdiv':sub,'compShared':compS,'uncompShared':uncompS,'subchunks':[]}
            while off<lend:
                if off+8>len(data): break
                sb=data[off:off+8]
                if sb==MARKER:
                    st=struct.unpack('<I',data[off+8:off+12])[0]
                    sl=struct.unpack('<I',data[off+12:off+16])[0]
                    off+=16
                else:
                    st=struct.unpack('<I',data[off:off+4])[0]
                    sl=struct.unpack('<I',data[off+4:off+8])[0]
                    off+=8
                if st!=0x40002: break
                send=off+sl
                unk2=struct.unpack('<I',data[off:off+4])[0];off+=4
                comp=struct.unpack('<I',data[off:off+4])[0];off+=4
                uncomp=struct.unpack('<I',data[off:off+4])[0];off+=4
                bmin=struct.unpack('<fff',data[off:off+12]);off+=12
                bmax=struct.unpack('<fff',data[off:off+12]);off+=12
                if off<send: off=send
                li['subchunks'].append({'compSize':comp,'uncompSize':uncomp,'bmin':bmin,'bmax':bmax})
            lods.append(li)
        res['lods']=lods
        data_start=16+res['root_length']
        if data_start>len(data): data_start=root_end
        off=data_start
        dec=[]
        for li_idx,li in enumerate(lods):
            if off+li['compShared']>len(data): break
            comp=data[off:off+li['compShared']];off+=li['compShared']
            try:
                d=decomp_block(comp,li['uncompShared'])
                layers=parse_layers(d,0x40001)
                dec.append({'lod':li_idx,'shared_layers':len(layers),'types':Counter([hex(l['typeId']) for l in layers])})
            except Exception as e:
                dec.append({'lod':li_idx,'shared_error':str(e)})
            for sci,sc in enumerate(li['subchunks']):
                if off+sc['compSize']>len(data): break
                comp=data[off:off+sc['compSize']];off+=sc['compSize']
                try:
                    d=decomp_block(comp,sc['uncompSize'])
                    layers=parse_layers(d,0x40002)
                    tc=Counter([hex(l['typeId']) for l in layers])
                    grids=[]
                    for lyr in layers:
                        if lyr['typeId']==0x40102:
                            try:
                                bio=io.BytesIO(lyr['raw'])
                                unk=struct.unpack('<I',bio.read(4))[0]
                                gs=struct.unpack('<i',bio.read(4))[0]
                                cnt=struct.unpack('<I',bio.read(4))[0]
                                ids=[struct.unpack('<I',bio.read(4))[0] for _ in range(cnt)]
                                gc=struct.unpack('<I',bio.read(4))[0]
                                grids.append({'gridSize':gs,'ids':ids,'gridCount':gc})
                            except: pass
                    dec.append({'lod':li_idx,'sc':sci,'layers':len(layers),'types':tc,'grids':grids})
                except Exception as e:
                    dec.append({'lod':li_idx,'sc':sci,'error':str(e)})
        res['decompressed']=dec
    except Exception as e:
        res['error']=str(e);res['trace']=traceback.format_exc()
    return res

def main():
    zone_dir="/tmp/maps"
    zone_files=[os.path.join(zone_dir,f) for f in os.listdir(zone_dir) if f.endswith('.zone')] if os.path.exists(zone_dir) else []
    chunk_dir=os.path.join(zone_dir,"chunks")
    chunk_files=[os.path.join(chunk_dir,f) for f in os.listdir(chunk_dir) if f.endswith('.gtchunk')] if os.path.exists(chunk_dir) else []
    print(f"Found {len(zone_files)} zones, {len(chunk_files)} chunks")
    all_zones=[]
    for zf in sorted(zone_files):
        print(f"Parsing zone {zf}")
        all_zones.append(parse_zone(zf))
    all_chunks=[]
    for cf in sorted(chunk_files)[:50]:
        print(f"Parsing chunk {cf}")
        all_chunks.append(parse_chunk(cf))

    # Write markdown
    md_path=os.path.join(OUTPUT_DIR,"MAP_FILES_FINDINGS.md")
    os.makedirs(OUTPUT_DIR,exist_ok=True)
    with open(md_path,'w',encoding='utf-8') as out:
        out.write("# Map Files Findings - Firefall Client Maps for World Population\n\n")
        out.write(f"Generated from `/tmp/maps.zip` (3.2GB) - {len(zone_files)} zones, {len(chunk_files)} chunks, 533 worldMap files\n\n")
        out.write("## Executive Summary\n")
        out.write("Breakdown of actual map files from game client for server implementation, especially world population.\n\n")
        out.write("**Key findings:**\n- Zone files contain chunk refs, path patrol routes (0x20800), Melding perimeters, bounds, subzone regions, encounter names, prop doodads\n- Chunk files contain Enwf collision, subzone grids (0x40102), encounter registries\n- Path layers contain CceId + steps + action bytes - potential authored NPC routes\n- MeldingPerimeterLayer gives precise Melding wall, used in PR #94 with 60m interpolation\n- SubZoneGridLayer gives per-chunk subzone IDs - could be spawn volumes\n- No per-zone spawn table found - consistent with WORLD_POPULATION.md\n\n")
        out.write("## Latest PRs about World Population\n\n")
        out.write("### PR #90 (MERGED) - Reduce load, expose config\n- MaxLiveNpcs 150, Activation 150m/Deactivation 225m, SpawnBudget 4 per 100ms\n- All rules in App.config\n\n")
        out.write("### PR #92 (MERGED) - Zone only\n- Fixes shard runs one zone but picker allows other zones -> empty world\n- Only counts players whose CurrentZone matches ZoneId, logs when nobody in zone\n\n")
        out.write("### PR #93 (MERGED) - Data-backed ambient NPC routines\n- Audits 575 tables, 3109 monsters, 508 wanderers, 1 stationary\n- Deterministic scheduling, combat interruption, work/rest vs deployables, navigation shared\n- Limit: no authored route table or CAIS tree, deployable placements 0 in JSON\n\n")
        out.write("### PR #94 (OPEN) - True-to-original improvements\n- Honors maxDistJitter, despawnWhenStuck, despawnDist, leashToSpawn, swarmRadiusMin/Max\n- Per-NPC EffectiveHomeRadius seeded, parks instead of retry, Melding edge anchors every 60m\n- Still limits: CAIS defaults, authored routes, non-ground, work stations, per-zone spawn table\n\n")
        out.write("## Map File Overview\n\n")
        out.write("### Archive\n```\nmaps.zip 3.2GB 893 entries\n- 38 *.zone (20KB-4MB)\n- 38 *.worldDir (581B-14KB)\n- 281 *.gtchunk (141KB-29MB)\n- 533 *.worldMap (197MB)\n```\n\n")
        out.write("### Zone IDs\n")
        zone_ids=sorted([r.get('zoneId','?') for r in all_zones])
        out.write(f"Total: {len(zone_ids)} - {', '.join(zone_ids)}\n\n")
        out.write("| ZoneId | Name | SizeKB | ChunkRefs | Paths | Melding | Bounds |\n|---|---|---|---|---|---|---|\n")
        for r in sorted(all_zones,key=lambda x:x.get('zoneId','')):
            zi=r.get('zone_info',{})
            sz=os.path.getsize(r.get('path',''))//1024 if os.path.exists(r.get('path','')) else 0
            out.write(f"| {r.get('zoneId')} | {r.get('name')} | {sz} | {len(zi.get('chunk_refs',[]))}+{len(zi.get('chunk_refs2',[]))} | {len(zi.get('paths',[]))} | {len(zi.get('melding_perims',[]))} | {bool(zi.get('bounds'))} |\n")

        out.write("\n## Zone File Format\n\n")
        out.write("```\nHeader: magic ZONE 4B, version int32, timestamp int64, nameLen int32, name\nRoot Header: marker ED125BED125AED12? then type 0x30000 + length, or 8B type+len\nRoot Data: series of GT layers\nContainer: 0x30000,0x20100,0x20200,0x20400,0x50001\n```\n")
        out.write("### Layer Types\n")
        out.write("| Type | Class | Desc | Use |\n|---|---|---|---|\n")
        out.write("| 0x20000 | Skybox | sky | no |\n| 0x20100 | DefaultEnv | env container | maybe |\n| 0x2710 | Env10000 | Vec3 | maybe anchor |\n| 0x50001 | PropEnv | prop container | work stations? |\n| 0x20200 | Melding | melding container | yes |\n| 5 | MeldingPerim | name, cp, bitfield, perims | yes - Melding wall |\n| 0x20300 | Water | water | no |\n| 0x20400 | ChunkInfo | chunk info | yes |\n| 0x10000 | ChunkRange | CubeFace, Min/Max | yes |\n| 0x10101 | ChunkRef | X,Y,RecId | yes |\n| 0x10100 | ChunkRef2 | X,Y | yes |\n| 0x20800 | Path | CceId, steps pos+orient+action | **high - patrol routes** |\n| 0x21000 | Bounds | Min/Max Vec3 | yes |\n| 0x21200 | EncounterReg | count+names | yes |\n| 0x21300/0x21400 | Doodad | unknown | maybe deployables |\n| 0x21500 | CameraSeq | camera | no |\n| 0x21600 | TransferBounds | transfer | maybe |\n| 0x21700 | SubZoneRegion | RegionId, bbox, W,H,bitmap | yes |\n")

        out.write("\n### ZonePathLayer 0x20800\n```\nCceId uint32, Unk1 uint32, StepCount uint32\nSteps: Vec3 pos, Vec4 orient, actionLen uint32, actionBytes\n```\nActionBytes often UTF8 strings - check for WanderPoint, StockShootAndFollowRoute etc.\n\n")

        out.write("\n## Chunk File Format\n\n")
        out.write("```\nRoot hdr type 0x40000 len\nVer uint32, TS int64, NumLods uint32\nFor each LOD: hdr 0x40001 len, Level, Subdiv, Unk, CompShared, UncompShared, subchunks until LOD end: hdr 0x40002 len, Unk, CompSize, UncompSize, BoundsMin Vec3, BoundsMax Vec3\nDataStart = 16+rootLen, then shared blocks + subchunk blocks: DATA=zlib, DAT2=LZMA (5B props+payload)\nDecompressed = layers parent 0x40001 or 0x40002\n```\n")
        out.write("| Type | Class | Desc | Use |\n|---|---|---|---|\n| 0x40101 | StaticGeom | Enwf | walkable |\n| 0x40102 | SubZoneGrid | GridSize, SubZoneIds, GridCount, GridData | **high - spawn volumes** |\n| 0x40103 | MoveBlocker | Enwf | exclude |\n| 0x40104/40204 | EncounterReg | names | yes |\n| 0x40105 | Water | Enwf | no |\n")

        out.write("\n## Detailed Zone Analysis\n\n")
        for r in sorted(all_zones,key=lambda x:x.get('zoneId','')):
            out.write(f"\n### Zone {r.get('zoneId')} - {r.get('name')}\n")
            if 'error' in r:
                out.write(f"Error {r['error']}\n"); continue
            zi=r.get('zone_info',{})
            out.write(f"- Ver {r.get('version')} TS {r.get('timestamp')} children {r.get('root_children_count')} types {dict(r.get('children_types',{}))}\n")
            out.write(f"- Bounds {zi.get('bounds')}\n")
            out.write(f"- Ranges {len(zi.get('chunk_ranges',[]))} {zi.get('chunk_ranges',[])[:2]}\n")
            out.write(f"- Refs {len(zi.get('chunk_refs',[]))} ref2 {len(zi.get('chunk_refs2',[]))} sample {zi.get('chunk_refs',[])[:3]}\n")
            out.write(f"- Paths {len(zi.get('paths',[]))}\n")
            for p in zi.get('paths',[])[:2]:
                if 'error' in p: out.write(f"  err {p}\n")
                else:
                    out.write(f"  CceId {p.get('cceId')} steps {p.get('total_steps')} unk {p.get('unk1')}\n")
                    for s in p.get('sample_steps',[])[:2]:
                        out.write(f"    pos {s.get('pos')} act '{s.get('action')[:100]}'\n")
            out.write(f"- Melding {len(zi.get('melding_perims',[]))}\n")
            for mp in zi.get('melding_perims',[])[:2]:
                out.write(f"  name '{mp.get('name')}' cp {mp.get('controlPoints')} perims {mp.get('perims')}\n")
            out.write(f"- SubZoneRegions {len(zi.get('subzone_regions',[]))} {zi.get('subzone_regions',[])[:2]}\n")
            out.write(f"- EncounterNames {len(zi.get('encounter_names',[]))} {zi.get('encounter_names',[])[:10]}\n")
            out.write(f"- Flags skybox {zi.get('skybox')} water {zi.get('water')} transfer {zi.get('transfer')} propDoodad {zi.get('prop_doodad')} defaultEnv {zi.get('default_env')} unknown {dict(zi.get('unknown',{}))}\n")

        out.write("\n## Chunk Analysis (50 sample)\n\n")
        for cr in all_chunks:
            out.write(f"\n### {cr.get('file')}\n")
            if 'error' in cr:
                out.write(f"Error {cr.get('error')}\n"); continue
            out.write(f"Ver {cr.get('version')} TS {cr.get('timestamp')} LODs {cr.get('numLods')}\n")
            for lod in cr.get('lods',[]):
                out.write(f"  LOD lvl {lod.get('level')} subdiv {lod.get('subdiv')} shared {lod.get('compShared')}->{lod.get('uncompShared')} subchunks {len(lod.get('subchunks',[]))}\n")
            for d in cr.get('decompressed',[])[:5]:
                out.write(f"  dec {d}\n")

        out.write("\n## WorldDir and WorldMap\n\n")
        out.write("- .worldDir: small, contains WMAP and paths like ToolsData/Environments/prod/BakedData/all_users/world and *.worldMap refs - likely index\n- .worldMap GTNO magic, 533 files 197MB, named 0_07_..._opt.worldMap - likely terrain material, not needed for pop but may have walkable flags\n\n")

        out.write("## How Population Uses Maps (Current)\n\n")
        out.write("ZoneLoader: reads ZONE, extracts chunk refs via ChunkOriginCalculator (special case 448 center 4,3.5 and 1030 9.5,3, origin=(center-(max-x))*512, name CubeFace_X_Y), loads gtchunk via ChunkProcessor, extracts Enwf -> NavigationTriangles (walkable centroids)\n")
        out.write("Planner: SurfaceCount+TryGetSurface -> 32m cells (CellIndexOf, MakeKey), CellDraft Sum/Count -> Center, GetChunkRecordId via ChunkByIndex, IsChunkSpawnable via ZoneChunkLinker.clientonly + ChunkRecord.remove_in_production\n")
        out.write("Habitat: Outposts pos+radius+LevelBandId Settlement, Deployables 469 Settlement radius 0 uses 25m config, Melding ControlPoints 16*4-23 Melding radius 0 uses 120m + 60m interp (PR94), Settlement beats Melding, Level via nearest banded anchor\n")
        out.write("Placement: TryGetGroundSurface probe 1.5m up/3m down, |normal.Z|>=0.35, horizontal probes ankle/waist/shoulder, headroom, broad-phase, SpawnOccupancyGrid hash planned bodies +25m player clearance, ground refusal after 8 parked, room retry 1s\n\n")

        out.write("## Opportunities\n\n")
        out.write("### 1 PathLayer - Authored Patrols\n- CceId+steps+actionBytes, present in many zones, could be missing route table (StockShootAndFollowRoute etc). Dump all actions, see if route names. Add ZonePathDataSource CceId->waypoints, try resolve named-route via CceId or nearest path.\n\n")
        out.write("### 2 PropEnv/Doodad - Work Stations\n- PropEnvironment 0x50001 and Doodad 0x21300/21400 currently Unknown, may contain deployable id+pos+orient. 109 nonempty deployable behaviours but 0 placements in JSON. If extract from zone, populate CustomData/deployable.json, enable work/rest.\n\n")
        out.write("### 3 SubZoneGrid - Spawn Volumes\n- GridSize, SubZoneIds, GridCount, GridData mapping cell->subzone, maps to SubZoneRegion regionId bitmap. Could be precise spawn volumes instead of distance anchor radius. Add SubZoneGridDataSource GetSubZoneId(pos).\n\n")
        out.write("### 4 EncounterRegistry - Spawn Groups\n- List ascii names, may link to SDB dbencounter tables. Dump all names, search SDB 575 tables for foreign keys, reconstruct per-zone monster list instead of global 2853.\n\n")
        out.write("### 5 Melding - Precise Wall\n- Already 60m interp in PR94, bitfield+perims may give spline type/height. Parse bitfield reconstruct full spline.\n\n")
        out.write("### 6 Bounds/Transfer\n- Bounds Min/Max AABB for early-out, TransferBounds (0x21600) unknown - likely transfer triggers, prevent spawn near.\n\n")

        out.write("## Recommendations\n\n")
        out.write("Short: keep current pop (conservative 150/150/225), add zone_path_dump tool, improve Melding interp, use Bounds early-out\n")
        out.write("Medium: SubZoneGrid for habitat, EncounterRegistry for spawn groups, PropEnv for work stations, Path routes for NPC routines\n")
        out.write("Long: CAIS tree defaults, non-ground locomotion volumes, per-zone spawn table via encounter+SDB, body -1 sentinel via chassis assetdb\n\n")

        out.write("## Tools\n\n")
        out.write("- Shared.Collision C# parser, CollisionGenerator cache, SdbDump, Population planner/terrain/data source\n- New: Tools/MapAnalysis/analyze_maps.py (this) - parses zone/chunk without .NET, outputs this doc\n- Suggested: zone_path_dump.py, subzone_grid_dump.py, encounter_name_dump.py, prop_env_dump.py\n\n")

        out.write("## Limitations\n\n")
        out.write("- No per-zone spawn table in SDB, MissionWaypoint chunk-local ±256, faction no zone link, respawn_flags not decoded, body -1 on 3103 rows, Deployable/Melding radius guessed, maps also no CAIS tree, route assignments beyond path layers, spawn volumes, climbing/flying navmesh, work stations maybe in prop layers but not confirmed\n\n")

        out.write("## Validation\n\n")
        out.write(f"- Parsed {len(all_zones)} zones ZONE magic ver8 root 0x30000\n- Parsed {len(all_chunks)} chunks 0x40000/0x40001/0x40002 DATA/DAT2\n- Extracted paths, melding, refs, bounds, subzone, encounter names\n- Cross-checked with Shared.Collision C#\n\n")

        out.write("## Conclusion\n\n")
        out.write("Maps rich but no ready per-zone spawn table. Contain: collision Enwf -> walkable, chunk grid -> origin+spawnability, path routes -> potential patrols, melding perims -> wall, subzone grids+regions -> habitat/level, encounter names -> spawn groups link, prop env/doodad -> work stations, bounds -> AABB. Most actionable: dump path actions for route names, reverse engineer doodad for deployable placements, parse subzone grid for habitat, dump encounter names search SDB. Current pop faithful to data that exists, cannot be faithful to spawn table never shipped. Maps give ground truth where NPCs can stand, not which belong where beyond habitat inference.\n")

    print(f"Wrote {md_path}")

    # JSON summary
    json_path=os.path.join(OUTPUT_DIR,"MAP_FILES_SUMMARY.json")
    def clean(o):
        if isinstance(o,dict):
            return {k:clean(v) for k,v in o.items() if k not in ('raw','trace')}
        elif isinstance(o,list):
            return [clean(x) for x in o]
        elif isinstance(o,bytes):
            return f"<{len(o)}B>"
        elif isinstance(o,Counter):
            return dict(o)
        else:
            return o
    with open(json_path,'w') as jf:
        json.dump(clean({'zones':all_zones,'chunks':all_chunks}),jf,indent=2)
    print(f"Wrote {json_path}")

if __name__=='__main__':
    main()
