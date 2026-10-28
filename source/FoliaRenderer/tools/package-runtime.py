"""Assemble the offscreen renderer without launching it. Node build must already exist."""
from pathlib import Path
import argparse,hashlib,json,shutil,urllib.request,zipfile

ROOT=Path(__file__).resolve().parents[1]
def checksum(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/folia')
    parser.add_argument('--cache',type=Path,default=ROOT/'.cache')
    args=parser.parse_args();output=args.output.resolve();cache=args.cache.resolve()
    if not (ROOT/'dist/index.html').is_file():parser.error('Run npm ci --ignore-scripts and npm run build first.')
    meta=json.loads((ROOT/'ELECTRON.json').read_text(encoding='utf-8'))
    upstream=json.loads((ROOT/'UPSTREAM.json').read_text(encoding='utf-8'))
    styles=json.loads((ROOT/'styles.json').read_text(encoding='utf-8'))
    assert len(styles)==15 and len({style['id'] for style in styles})==15
    cache.mkdir(parents=True,exist_ok=True);archive=cache/Path(meta['source']).name
    if not archive.exists() or checksum(archive)!=meta['sha256']:
        print('Downloading pinned Electron runtime '+meta['version'],flush=True)
        with urllib.request.urlopen(meta['source'],timeout=120) as response,archive.open('wb') as target:shutil.copyfileobj(response,target)
    if checksum(archive)!=meta['sha256']:raise RuntimeError('Electron download checksum mismatch')
    runtime=output/'Runtime';runtime.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(archive) as bundle:
        for item in bundle.infolist():
            rel=Path(item.filename)
            if rel.is_absolute() or '..' in rel.parts:raise RuntimeError('Unsafe archive path')
            if item.is_dir():continue
            if rel.parts[0]=='locales' and rel.name not in {'en-US.pak','zh-CN.pak'}:continue
            target=runtime/('PeakFoliaRenderer.exe' if rel.name=='electron.exe' else rel)
            assert target.resolve().is_relative_to(runtime.resolve())
            target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(bundle.read(item))
    app=output/'App';app.mkdir(parents=True,exist_ok=True)
    for name in ['main.cjs','preload.cjs','LICENSE','UPSTREAM.json','ELECTRON.json','styles.json','README.md']:shutil.copy2(ROOT/name,app/name)
    stage=app/'dist'
    if stage.exists():
        # This is our generated output, never an installed game/user folder.
        assert stage.resolve()==(output/'App/dist').absolute() and stage.parent.resolve()==app.resolve()
        assert json.loads((app/'package.json').read_text(encoding='utf-8'))['name']=='peak-folia-renderer'
        shutil.rmtree(stage)
    shutil.copytree(ROOT/'dist',stage)
    package=json.loads((ROOT/'package.json').read_text(encoding='utf-8'))
    (app/'package.json').write_text(json.dumps({key:package[key] for key in ['name','version','main','license']},indent=2),encoding='utf-8')
    notices=[];seen=set()
    for path in sorted((ROOT/'node_modules').rglob('package.json')):
        try:data=json.loads(path.read_text(encoding='utf-8'))
        except (OSError,ValueError):continue
        identity=(data.get('name',''),data.get('version',''))
        if not all(identity) or identity in seen:continue
        seen.add(identity);texts=[]
        for file in sorted(path.parent.iterdir()):
            if file.is_file() and file.name.lower().split('.')[0] in {'license','licence','notice','copying','copyright'}:texts.append(file.name+'\n'+file.read_text(encoding='utf-8',errors='replace'))
        notices.append('\n'+'='*72+'\n'+identity[0]+' '+identity[1]+'\nLicense: '+str(data.get('license','See package source'))+'\n'+'\n'.join(texts))
    (app/'THIRD-PARTY-NOTICES.txt').write_text('Folia frontend dependencies. Rebuild inputs are pinned in package-lock.json.\n'+'\n'.join(notices),encoding='utf-8')
    (output/'BUILD.json').write_text(json.dumps({'adapterVersion':package['version'],'upstream':upstream,'electron':meta,'styles':[style['id'] for style in styles],'runtimeTested':False},indent=2),encoding='utf-8')
    print(json.dumps({'output':str(output),'styleCount':len(styles),'dependencyNotices':len(seen),'runtimeExecuted':False}),flush=True)
if __name__=='__main__':main()
