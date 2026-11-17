"""Exercise the actual packaged Muonium services, with no account credentials or audio output."""
from pathlib import Path
import json,subprocess,os,argparse
parser=argparse.ArgumentParser();parser.add_argument('--live',action='store_true');args=parser.parse_args()
root=Path(__file__).resolve().parents[2];runtime=root/'artifacts/muonium'
child=subprocess.Popen([str(runtime/'Runtime/bin/java.exe'),'-Djava.awt.headless=true','-cp',str(runtime/'*'),'PeakMuoniumBridge',str(os.getpid())],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8')
sequence=0
def call(op,source='ncm',**fields):
 global sequence
 sequence+=1;child.stdin.write(json.dumps(dict(id=sequence,op=op,source=source,**fields))+'\n');child.stdin.flush()
 result=json.loads(child.stdout.readline());assert result['id']==sequence
 return result
try:
 assert call('hello')['data']['protocol']==1
 assert call('restore',credentials={})['ok']
 rejected=call('unsupported');assert not rejected['ok']
 assert 'credentials' not in rejected
 print('RPC offline checks passed: 4',flush=True)
 if args.live:
  for source in ['ncm','qq']:
   result=call('account',source);assert result['ok'] and result['data']['loggedIn'] is False
   qr=call('qr',source);assert qr['ok'] and len(qr['data']['image'])>100
   print(source,'account + QR passed',flush=True)
   discover=call('discover',source);print(source,'recommendations',len(discover.get('data',{}).get('playlists',[])),flush=True)
   search=call('search',source,query='Faded',page=1);rows=search.get('data',{}).get('tracks',[])
   print(source,'search',len(rows),'ok',search['ok'],flush=True)
   if rows:
    resolved=call('resolve',source,trackId=rows[0]['id']);data=resolved.get('data',{})
    print(source,'audio URL',bool(data.get('url')),'lyric rows',len(data.get('lines',[])),flush=True)
finally:
 child.stdin.close()
 try:child.wait(timeout=5)
 except subprocess.TimeoutExpired:child.kill();child.wait(timeout=5)
