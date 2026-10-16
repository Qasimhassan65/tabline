import Foundation
import TablineCore

final class NativeHost {
    private let writeLock=NSLock()
    private var input:FileHandle { .standardInput }
    func start(receive:@escaping ([String:Any])->Void,closed:@escaping ()->Void) {
        DispatchQueue.global(qos:.userInitiated).async { [self] in
            do {
                while let header=try readExactly(4,allowEOF:true) {
                    let size=try NativeFraming.length(header)
                    guard let body=try readExactly(size,allowEOF:false),
                          let request=try JSONSerialization.jsonObject(with:body) as? [String:Any],
                          request["requestId"] is NSNumber,request["action"] is String else {
                        throw CompanionError.message("Malformed native request.")
                    }
                    DispatchQueue.main.async { receive(request) }
                }
            } catch { Self.log(error.localizedDescription) }
            DispatchQueue.main.async { closed() }
        }
    }
    private func readExactly(_ count:Int,allowEOF:Bool)throws->Data? {
        var result=Data();result.reserveCapacity(count)
        while result.count<count {
            guard let bytes=try input.read(upToCount:count-result.count),!bytes.isEmpty else {
                if result.isEmpty&&allowEOF{return nil}
                throw CompanionError.message("Truncated native message.")
            }
            result.append(bytes)
        }
        return result
    }
    func send(_ object:[String:Any]) {
        do {let packet=try NativeFraming.encode(object);writeLock.lock();defer{writeLock.unlock()};try FileHandle.standardOutput.write(contentsOf:packet)}
        catch {Self.log(error.localizedDescription)}
    }
    static func log(_ text:String) {try? FileHandle.standardError.write(contentsOf:Data(("Tabline: "+text+"\n").utf8))}
}
