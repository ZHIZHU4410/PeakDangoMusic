"""Bounded native-platform probes. Never loads user account data or prints signed URLs."""
from pathlib import Path
import subprocess,json,os,argparse
from concurrent.futures import ThreadPoolExecutor,TimeoutError
p=argparse.ArgumentParser();p.add_argument('--live',action='store_true');args=p.parse_args()
root=Path(__file__).resolve().parents[2];runtime=root/'artifacts/muonium'
child=subprocess.Popen([str(runtime/'Runtime/bin/java.exe'),'-Djava.awt.headless=true','-cp',str(runtime/'*'),'PeakMuoniumBridge',str(os.getpid())],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,text=True,encoding='utf-8')
pool=ThreadPoolExecutor(1)
def call(op,source,**fields):
 child.stdin.write(json.dumps(dict(id=1,op=op,source=source,**fields))+'\n');child.stdin.flush()
 line=pool.submit(child.stdout.readline).result(40);r=json.loads(line)
 assert r['ok'],r.get('error')
 return r['data']
try:
 call('restore','ncm',credentials={})
 for source in ['youtube-native','bilibili-native','kugou']:
  r=call('logout',source);assert r['loggedIn'] is False
  assert 'credentials' in r
  r=call('account',source);assert r['loggedIn'] is False
 print('Native anonymous account/logout + DPAPI snapshot checks: 9 passed',flush=True)
 if args.live:
  for source in ['youtube-native','bilibili-native','kugou']:
   rows=call('search',source,query='Faded')['tracks']
   print(source,'search rows',len(rows),flush=True)
   if rows:assert all(k in rows[0] for k in ['id','title','source','cover'])
finally:
 child.kill();child.wait(timeout=5);pool.shutdown(wait=True,cancel_futures=True)
