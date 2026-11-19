from pathlib import Path
import json,hashlib,urllib.request,zipfile,subprocess,shutil,argparse,os
root=Path(__file__).resolve().parent
parser=argparse.ArgumentParser();parser.add_argument('--jdk',default=os.environ.get('JAVA_HOME') or str(Path(shutil.which('javac') or 'javac').resolve().parent.parent));args=parser.parse_args()
output=root.parents[1]/'artifacts/muonium';output.mkdir(parents=True,exist_ok=True)
cache=root.parents[1]/'artifacts/muonium-deps';cache.mkdir(exist_ok=True)
deps=json.loads((root/'dependencies.json').read_text())
def fetch(url,target):
 if not target.exists():
  with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'PeakDangoMusic-build'}),timeout=60) as response:target.write_bytes(response.read())
 return target
provenance=[]
for dep in deps['libraries']:
 jar=fetch(dep['url'],cache/dep['name'])
 actual=hashlib.sha256(jar.read_bytes()).hexdigest();assert actual==dep['sha256']
 shutil.copy2(jar,output/jar.name)
 with zipfile.ZipFile(jar) as z:
  notices=[n for n in z.namelist() if n.upper().startswith('META-INF/') and ('LICENSE' in n.upper() or 'NOTICE' in n.upper())]
  for n in notices:
   if not n.endswith('/'):(output/(dep['name']+'-'+Path(n).name+'.txt')).write_bytes(z.read(n))
 provenance.append(dict(url=dep['url'],sha256=actual))
jre=deps['jre'];archive=fetch(jre['url'],cache/'jre.zip')
expected=jre['sha256']
actual=hashlib.sha256(archive.read_bytes()).hexdigest();assert actual==expected
if not (output/'Runtime/bin/javaw.exe').is_file():
 with zipfile.ZipFile(archive) as z:
  prefix=z.namelist()[0].split('/')[0]+'/'
  for entry in z.infolist():
   if not entry.filename.startswith(prefix) or entry.is_dir():continue
   dest=output/'Runtime'/entry.filename[len(prefix):];dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(z.read(entry))
provenance.append(dict(url=jre['url'],sha256=actual))
classes=cache/'classes';classes.mkdir(exist_ok=True)
sources=list((root/'vendor').glob('*.java'))+list((root/'src').glob('*.java'))+list((root/'platforms').glob('*.java'))
rsp=cache/'javac.rsp';rsp.write_text('\n'.join('"'+s.as_posix()+'"' for s in sources),encoding='utf-8')
subprocess.run([str(Path(args.jdk)/'bin/javac.exe'),'-encoding','UTF-8','--release','21','-cp',str(output/'*'),'-d',str(classes),'@'+str(rsp)],check=True)
subprocess.run([str(Path(args.jdk)/'bin/jar.exe'),'--create','--file',str(output/'PeakMuoniumBridge.jar'),'-C',str(classes),'.'],check=True)
subprocess.run([str(Path(args.jdk)/'bin/jar.exe'),'--update','--file',str(output/'PeakMuoniumBridge.jar'),'-C',str(root/'resources'),'.'],check=True)
shutil.copy2(root.parents[1]/'docs/MUONIUM-APACHE-2.0.txt',output/'MUONIUM-APACHE-2.0.txt')
(output/'PROVENANCE.json').write_text(json.dumps(provenance,indent=2))
print(output)
