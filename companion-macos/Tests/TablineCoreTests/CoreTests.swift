import XCTest
@testable import TablineCore

final class CoreTests: XCTestCase {
    func testFramingAndUTF8() throws {
        let packet=try NativeFraming.encode(["title":"Café — مرحبا", "ok":true])
        let count=try NativeFraming.length(packet.prefix(4))
        XCTAssertEqual(count,packet.count-4)
        let decoded=try XCTUnwrap(JSONSerialization.jsonObject(with:packet.dropFirst(4)) as? [String:Any])
        XCTAssertEqual(decoded["title"] as? String,"Café — مرحبا")
        XCTAssertThrowsError(try NativeFraming.length(Data([0,0,0,0])))
        XCTAssertThrowsError(try NativeFraming.length(Data([255,255,255,255])))
        XCTAssertThrowsError(try NativeFraming.length(Data([1,2])))
    }
    func testIDsAndPNGValidation() {
        XCTAssertTrue(NativeFraming.extensionID(String(repeating:"a",count:32)))
        XCTAssertFalse(NativeFraming.extensionID("../anything"))
        XCTAssertFalse(NativeFraming.extensionID(String(repeating:"q",count:32)))
        XCTAssertThrowsError(try NativeFraming.png("data:image/png;base64,aGVsbG8="))
    }
    func testCacheIsBoundedAndLRU() {
        let cache=PreviewCache(limit:10,countLimit:2)
        cache.put("a",Data(repeating:1,count:5));cache.put("b",Data(repeating:2,count:5))
        XCTAssertNotNil(cache.get("a"));cache.put("c",Data(repeating:3,count:5))
        XCTAssertNil(cache.get("b"));XCTAssertEqual(cache.bytes,10)
        cache.put("a",Data(repeating:4,count:30));XCTAssertEqual(cache.bytes,5)
        cache.clear();XCTAssertEqual(cache.bytes,0)
    }
    func testResponsiveGeometryAndHitMapping() {
        for width:CGFloat in [320,640,1024,1366,1920] {
            var layout=RopeLayout(width:width,height:360,count:9)
            for index in [0,4,8] {
                layout.offset=CGFloat(index)*layout.pitch
                let r=layout.rect(index)
                XCTAssertEqual(r.midX,width/2,accuracy:0.01)
                XCTAssertTrue(layout.viewport.contains(CGPoint(x:r.midX,y:r.midY)))
                XCTAssertEqual(layout.local(CGPoint(x:r.midX,y:r.minY+4.5*r.width/376),index:index).x,188,accuracy:0.01)
                XCTAssertEqual(layout.roll(index),0,accuracy:0.001)
            }
            for direction in [-1,1] { let r=layout.arrow(direction);XCTAssertEqual(r.width,44);XCTAssertGreaterThanOrEqual(r.minX,0);XCTAssertLessThanOrEqual(r.maxX,width) }
            XCTAssertEqual(layout.next(-1,target:0),0)
            XCTAssertEqual(layout.next(1,target:layout.maximum),layout.maximum)
            XCTAssertEqual(layout.next(1,target:layout.pitch*2.2),layout.pitch*3,accuracy:0.01)
        }
    }
}
