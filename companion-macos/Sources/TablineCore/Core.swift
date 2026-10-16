import Foundation
import CoreGraphics

public enum CompanionError: LocalizedError {
    case message(String)
    public var errorDescription: String? { if case let .message(text) = self { return text }; return nil }
}

/// Chrome Native Messaging: UTF-8 JSON, prefixed with a little-endian UInt32.
public struct NativeFraming {
    public static let inputLimit = 64 * 1024 * 1024
    public static let outputLimit = 1024 * 1024
    public static func length(_ data: Data) throws -> Int {
        guard data.count == 4 else { throw CompanionError.message("Invalid native message header.") }
        let bytes = Array(data)
        let size = Int(UInt32(bytes[0]) | UInt32(bytes[1]) << 8 | UInt32(bytes[2]) << 16 | UInt32(bytes[3]) << 24)
        guard size > 0, size <= inputLimit else { throw CompanionError.message("Native message exceeds the size limit.") }
        return size
    }
    public static func encode(_ object: [String: Any]) throws -> Data {
        let body = try JSONSerialization.data(withJSONObject: object, options: [.sortedKeys])
        guard body.count <= outputLimit else { throw CompanionError.message("Native response exceeds the size limit.") }
        let n = UInt32(body.count)
        var result = Data([UInt8(n & 255), UInt8((n >> 8) & 255), UInt8((n >> 16) & 255), UInt8((n >> 24) & 255)])
        result.append(body); return result
    }
    public static func extensionID(_ value: String) -> Bool {
        value.count == 32 && value.utf8.allSatisfy { $0 >= 97 && $0 <= 112 }
    }
    public static func png(_ value: String) throws -> Data {
        let prefix = "data:image/png;base64,"
        guard value.hasPrefix(prefix), value.utf8.count <= 48 * 1024 * 1024,
              let bytes = Data(base64Encoded: String(value.dropFirst(prefix.count))),
              bytes.starts(with: [137,80,78,71,13,10,26,10]) else {
            throw CompanionError.message("Clipboard input must be a valid PNG screenshot within the size limit.")
        }
        return bytes
    }
}

/// Byte-bounded compressed preview cache, not a screenshot history.
public final class PreviewCache {
    private var entries: [String: Data] = [:]
    private var order: [String] = []
    public let limit: Int
    public let countLimit: Int
    public private(set) var bytes = 0
    public init(limit: Int = 1024 * 1024, countLimit: Int = 32) { self.limit = limit; self.countLimit = countLimit }
    public func get(_ key: String) -> Data? {
        guard let value = entries[key] else { return nil }
        order.removeAll { $0 == key }; order.append(key); return value
    }
    public func put(_ key: String, _ value: Data) {
        if let old = entries.removeValue(forKey: key) { bytes -= old.count }
        order.removeAll { $0 == key }
        guard value.count <= limit else { return }
        entries[key] = value; order.append(key); bytes += value.count
        while bytes > limit || entries.count > countLimit {
            guard let first = order.first else { break }
            order.removeFirst(); if let removed = entries.removeValue(forKey: first) { bytes -= removed.count }
        }
    }
    public func clear() { entries.removeAll(); order.removeAll(); bytes = 0 }
}

public struct RopeLayout {
    public var width: CGFloat
    public var height: CGFloat
    public var count: Int
    public var offset: CGFloat
    public init(width: CGFloat, height: CGFloat, count: Int, offset: CGFloat = 0) {
        self.width = width; self.height = height; self.count = count; self.offset = offset
    }
    public var compact: Bool { width < 360 }
    public var gap: CGFloat { min(24, max(12, width * 0.025)) }
    public var cardWidth: CGFloat {
        [300, max(72, width - 12), max(240, (width - 152) / 5), max(72, (height - 18 - sag - (compact && count > 1 ? 56 : 0)) * 376 / 318)].min()!
    }
    public var pitch: CGFloat { cardWidth + gap }
    public var sag: CGFloat { min(26, max(14, width * 0.016)) }
    public var maximum: CGFloat { CGFloat(max(0, count - 1)) * pitch }
    public var viewport: CGRect { let inset: CGFloat = count > 1 && !compact ? 52 : 0; return CGRect(x: inset, y: 0, width: max(1, width - 2 * inset), height: height) }
    public func cable(_ x: CGFloat) -> CGFloat { let half = max(1, width/2), u = min(1, max(-1, (x-half)/half)); return 14 + sag * (1-u*u) }
    public func slope(_ x: CGFloat) -> CGFloat { let half = max(1, width/2); return -2*sag*min(1,max(-1,(x-half)/half))/half }
    public func rect(_ index: Int) -> CGRect { let x=(width-cardWidth)/2+CGFloat(index)*pitch-offset; return CGRect(x:x,y:cable(x+cardWidth/2)+19*cardWidth/376,width:cardWidth,height:285*cardWidth/376) }
    public func roll(_ index: Int) -> CGFloat { let r=rect(index); return min(1.8,max(-1.8,atan(slope(r.midX))*90 / .pi)) }
    public func local(_ point: CGPoint, index: Int) -> CGPoint {
        let r=rect(index), k=cardWidth/376, theta = -roll(index)*CGFloat.pi/180
        let pivot=CGPoint(x:r.midX,y:r.minY+4.5*k), dx=point.x-pivot.x, dy=point.y-pivot.y
        return CGPoint(x:188+(dx*cos(theta)-dy*sin(theta))/k,y:4.5+(dx*sin(theta)+dy*cos(theta))/k)
    }
    public func arrow(_ direction: Int) -> CGRect { let center=14+sag+(compact ? 304*cardWidth/376+34 : 161.5*cardWidth/376); return CGRect(x:direction < 0 ? 4 : width-48,y:center-22,width:44,height:44) }
    public func next(_ direction: Int, target: CGFloat) -> CGFloat {
        var position=target/pitch; if abs(position-position.rounded()) < 0.003 { position=position.rounded() }
        let index=direction > 0 ? floor(position)+1 : ceil(position)-1
        return min(maximum,max(0,index*pitch))
    }
}
