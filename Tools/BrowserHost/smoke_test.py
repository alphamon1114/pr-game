"""Windows integration check against the real, independently running Chromium host."""
import ctypes, http.server, json, mmap, os, pathlib, queue, struct, subprocess, threading, time

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / 'Logs' / 'WebHostQA'
OUT.mkdir(parents=True, exist_ok=True)

class Fixture(http.server.BaseHTTPRequestHandler):
    def log_message(self, *_): pass
    def do_GET(self):
        body = ('''<!doctype html><meta charset="utf-8"><title>Browser fixture</title>
        <style>body{margin:0;background:#e9edf2;font:24px sans-serif}input{position:absolute;left:20px;top:100px;width:400px;height:45px}button{position:absolute;left:20px;top:180px;width:200px;height:50px}main{height:2200px}a{position:absolute;left:20px;top:260px}</style>
        <main><h1>Live Chromium</h1><input id="q"><button onclick="document.title='Clicked'">Click me</button><a href="/second">Next page</a></main>'''
        if self.path != '/second' else '<!doctype html><title>Second page</title><h1>Second page</h1>')
        data=body.encode('utf-8');self.send_response(200);self.send_header('Content-Type','text/html; charset=utf-8');self.send_header('Content-Length',str(len(data)));self.end_headers();self.wfile.write(data)

server=http.server.ThreadingHTTPServer(('127.0.0.1',0),Fixture)
threading.Thread(target=server.serve_forever,daemon=True).start()
events=queue.Queue();records=[]
host=subprocess.Popen([str(ROOT/'BrowserRuntime/Windows/PrGame.BrowserHost.exe'),str(os.getpid()),str(OUT/'Cache')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=open(OUT/'stderr.log','w'),encoding='utf-8',creationflags=subprocess.CREATE_NO_WINDOW)
def read():
    for line in host.stdout:
        if line.startswith('PRWEB:'):
            event=json.loads(line[6:]);events.put(event);records.append(event)
threading.Thread(target=read,daemon=True).start()
def send(kind,**kwargs):
    host.stdin.write(json.dumps(dict(type=kind,id='qa',**kwargs),ensure_ascii=True)+'\n');host.stdin.flush()
def wait(predicate,timeout=25):
    end=time.monotonic()+timeout
    while time.monotonic()<end:
        try:event=events.get(timeout=.2)
        except queue.Empty:
            if host.poll() is not None:raise RuntimeError('Host exited: '+str(host.returncode))
            continue
        if event['kind'] in ('fatal','error'):raise RuntimeError(event)
        if predicate(event):return event
    raise TimeoutError('No expected browser event; recent='+str(records[-8:]))
def evaluate(script):
    token=str(time.time_ns());send('evaluate',text=script,request=token)
    return wait(lambda e:e['kind']=='evaluation' and e.get('request')==token)['text']
name='PRGameWebQA-'+str(os.getpid());frame=mmap.mmap(-1,32+1920*1200*4,tagname=name)
kernel=ctypes.windll.kernel32;kernel.CreateMutexW.restype=ctypes.c_void_p
mutex=kernel.CreateMutexW(None,False,name+'-gate')
try:
    wait(lambda e:e['kind']=='ready')
    send('create',map=name,mutex=name+'-gate',width=960,height=600)
    wait(lambda e:e['kind']=='created')
    send('navigate',url=f'http://127.0.0.1:{server.server_port}/fixture')
    wait(lambda e:e['kind']=='title' and e.get('text')=='Browser fixture')
    time.sleep(.3)
    assert evaluate('document.title')=='Browser fixture'
    send('focus',flag=True);send('down',x=100,y=125,button=0,count=1);send('up',x=100,y=125,button=0,count=1)
    time.sleep(.2)
    assert evaluate('document.activeElement.id')=='q'
    send('composition',text='한');time.sleep(.1)
    assert evaluate('document.querySelector("#q").value')=='한'
    send('text',text='한글 검색 테스트');time.sleep(.2)
    value=evaluate('document.querySelector("#q").value')
    assert value=='한글 검색 테스트',repr(value)
    send('down',x=80,y=205,button=0,count=1);send('up',x=80,y=205,button=0,count=1)
    wait(lambda e:e['kind']=='title' and e.get('text')=='Clicked')
    send('wheel',x=700,y=350,dy=-480);time.sleep(.4)
    assert float(evaluate('window.scrollY'))>100
    send('navigate',url=f'http://127.0.0.1:{server.server_port}/second')
    wait(lambda e:e['kind']=='title' and e.get('text')=='Second page')
    send('back');wait(lambda e:e['kind']=='title' and e.get('text') in ('Clicked','Browser fixture'))
    send('resize',width=800,height=500);time.sleep(.5)
    seq,w,h=struct.unpack_from('<iii',frame,0);assert seq>0 and (w,h)==(800,500),(seq,w,h)
    # Verify a real public HTTPS page, not only a fixture server.
    send('navigate',url='https://example.com/')
    wait(lambda e:e['kind']=='title' and e.get('text')=='Example Domain',45)
    assert 'domain' in evaluate('document.body.innerText').lower()
    time.sleep(.5);seq,w,h=struct.unpack_from('<iii',frame,0)
    (OUT/'live-frame.bgra').write_bytes(frame[32:32+w*h*4]);(OUT/'frame-size.json').write_text(json.dumps([w,h]))
    print('PASS: live HTTPS; real Chromium pixels; pointer click; scroll; Korean composition/commit; native back; resize',flush=True)
finally:
    try:send('quit');host.wait(timeout=8)
    except Exception:host.kill()
    (OUT/'events.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
    frame.close();kernel.CloseHandle(ctypes.c_void_p(mutex));server.shutdown()
