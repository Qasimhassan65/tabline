"""Native host handshake check; no screen, clipboard or personal content captured."""
import json,struct,subprocess,sys,threading
process=subprocess.Popen([sys.argv[1],'chrome-extension://'+'a'*32+'/'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
watchdog=threading.Timer(25,process.kill);watchdog.start()
try:
    def request(action,seq):
        body=json.dumps({'requestId':seq,'action':action}).encode()
        process.stdin.write(struct.pack('<I',len(body))+body);process.stdin.flush()
        header=process.stdout.read(4)
        assert len(header)==4,'Host exited or timed out before replying'
        size=struct.unpack('<I',header)[0];assert 0<size<=1024*1024
        response=json.loads(process.stdout.read(size));assert response['requestId']==seq
        return response
    ping=request('ping',1)
    assert ping['ok'] and ping['platform']=='mac' and ping['protocolVersion']==1
    assert 'screenRecording' in ping['permissions']
    assert request('stats',2)['frameBytes']==0
    assert request('unsupported-for-test',3)['ok'] is False
    process.stdin.close();process.wait(timeout=10)
    assert process.returncode==0,process.stderr.read().decode()
    print('PASS: packaged Mac host ping, capability/permission reporting, stats, error response and EOF shutdown')
finally:
    watchdog.cancel()
    if process.poll() is None:process.kill()
